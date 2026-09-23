// =============================================================================
// PlatformBillingService.cs — O financeiro DA PLATAFORMA: o que cobramos de
// cada loja e o que efetivamente entrou.
//
// Não confundir com FinanceiroCalculoService, que é o financeiro DE DENTRO de
// uma loja e opera no schema do tenant. Este aqui só toca o catálogo (schema
// "public") e só o dono da plataforma alcança.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CardGameStore.Services.Implementations;

public class PlatformBillingService : IPlatformBillingService
{
    private readonly CatalogDbContext _catalog;
    private readonly ILogger<PlatformBillingService> _logger;
    private readonly IReferralCommissionService? _referrals;
    private readonly IPlatformPaymentGateway? _gateway;
    private readonly IConfiguration? _config;
    private readonly IPlatformBillingNotifier? _notifier;
    private readonly IMemoryCache? _cache;

    public PlatformBillingService(
        CatalogDbContext catalog,
        ILogger<PlatformBillingService> logger,
        IReferralCommissionService? referrals = null,
        IPlatformPaymentGateway? gateway = null,
        IConfiguration? config = null,
        IPlatformBillingNotifier? notifier = null,
        IMemoryCache? cache = null)
    {
        _catalog = catalog;
        _logger  = logger;
        _referrals = referrals;
        _gateway = gateway;
        _config  = config;
        _notifier = notifier;
        _cache    = cache;
    }

    /// <summary>Dias de tolerância depois do vencimento antes de suspender.
    ///
    /// Quinze é o piso do CONTRATO (cláusula 15.1: suspensão só com atraso
    /// "superior a 15 dias"), e não uma folga técnica. O padrão era 7 e
    /// suspendia loja de cliente no oitavo dia — metade do prazo contratado.
    /// Continua configurável porque é regra de negócio, mas baixar daqui é
    /// descumprir o contrato assinado.</summary>
    internal const int CarenciaPadraoDoContrato = 15;

    private int DiasDeCarencia =>
        int.TryParse(_config?["Billing:DiasDeCarenciaAposVencimento"], out var dias) && dias >= 0
            ? dias
            : CarenciaPadraoDoContrato;

    /// <summary>Reduz qualquer data ao dia 1 do mês, 00:00 UTC. Toda competência
    /// passa por aqui: sem isso, "março" gravado como dia 3 e como dia 17 viram
    /// duas competências diferentes e a unique index que impede cobrança
    /// duplicada deixa de proteger.</summary>
    private static DateTime NormalizarCompetencia(DateTime data) =>
        new(data.Year, data.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Reduz a data de baixa ao dia, 00:00 UTC.
    ///
    /// O corpo da requisição traz só a data ("2026-08-14"), que o
    /// System.Text.Json desserializa como DateTime com Kind=Unspecified — e o
    /// Npgsql RECUSA gravar Kind=Unspecified numa coluna `timestamp with time
    /// zone`, derrubando o SaveChanges com DbUpdateException. Era por isso que
    /// dar baixa em qualquer cobrança respondia 500.
    ///
    /// Truncar para o dia é o mesmo tratamento que competência e vencimento já
    /// recebem (ver NormalizarCompetencia): a baixa é um fato do dia, e guardar
    /// a hora do clique faria a mesma cobrança "mudar de dia" quando lida de
    /// outro fuso. Se a data já vier com fuso (Kind=Local/Utc), converte antes
    /// de truncar, senão uma baixa feita às 22h em São Paulo viraria o dia
    /// seguinte em UTC.</summary>
    private static DateTime? NormalizarDataPagamento(DateTime? pagoEm)
    {
        if (!pagoEm.HasValue) return null;
        var data = pagoEm.Value.Kind == DateTimeKind.Unspecified
            ? pagoEm.Value
            : pagoEm.Value.ToUniversalTime();
        return new DateTime(data.Year, data.Month, data.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    public async Task<GerarMensalidadesResultDto> GerarMensalidadesAsync(DateTime competencia)
    {
        var comp = NormalizarCompetencia(competencia);
        var resultado = new GerarMensalidadesResultDto { Competencia = comp };

        var ativas = await _catalog.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active)
            .ToListAsync();

        var ids = ativas.Select(t => t.Id).ToList();

        var descontos = (await _catalog.TenantBillingDiscounts
                .AsNoTracking()
                .Where(d => ids.Contains(d.TenantId))
                .ToListAsync())
            .ToLookup(d => d.TenantId);

        // Uma consulta só pra saber o que já existe, em vez de perguntar ao
        // banco por tenant dentro do laço. Implantação vem inteira (e não só a
        // da competência) porque o saldo das parcelas depende das já pagas.
        var existentes = (await _catalog.TenantCharges
                .AsNoTracking()
                .Where(c => ids.Contains(c.TenantId)
                         && (c.ReferenceMonth == comp || c.Kind == TenantChargeKind.Implantacao))
                .ToListAsync())
            .ToLookup(c => c.TenantId);

        var novas = new List<TenantCharge>();
        foreach (var tenant in ativas)
        {
            var faltantes = CobrancasQueFaltam(tenant, descontos[tenant.Id].ToList(), existentes[tenant.Id].ToList(), comp);
            novas.AddRange(faltantes.Novas);

            switch (faltantes.Mensalidade)
            {
                case SituacaoMensalidade.ForaDeCobranca:    resultado.ForaDeCobranca++;     break;
                case SituacaoMensalidade.JaExistia:         resultado.JaExistiam++;         break;
                case SituacaoMensalidade.ZeradaPorDesconto: resultado.ZeradasPorDesconto++; break;
            }
        }

        if (novas.Count > 0)
        {
            _catalog.TenantCharges.AddRange(novas);
            await _catalog.SaveChangesAsync();
        }

        resultado.Criadas               = novas.Count;
        resultado.ParcelasDeImplantacao = novas.Count(c => c.Kind == TenantChargeKind.Implantacao);
        resultado.TotalGerado           = novas.Sum(c => c.Amount);

        _logger.LogInformation(
            "Cobranças da competência {Competencia:yyyy-MM}: {Criadas} criadas ({Parcelas} parcelas de implantação), {JaExistiam} já existiam, {Fora} fora de cobrança (total R$ {Total}).",
            comp, resultado.Criadas, resultado.ParcelasDeImplantacao, resultado.JaExistiam, resultado.ForaDeCobranca, resultado.TotalGerado);

        return resultado;
    }

    private enum SituacaoMensalidade { ForaDeCobranca, Criada, JaExistia, ZeradaPorDesconto }

    private sealed record CobrancasFaltantes(List<TenantCharge> Novas, SituacaoMensalidade Mensalidade);

    /// <summary>O que as condições da loja mandam cobrar na competência e ainda
    /// não existe. Serve ao gerador do mês e ao recálculo depois de uma
    /// renegociação — uma regra só pros dois.</summary>
    private static CobrancasFaltantes CobrancasQueFaltam(
        Tenant tenant,
        IReadOnlyCollection<TenantBillingDiscount> descontos,
        IReadOnlyCollection<TenantCharge> existentes,
        DateTime comp)
    {
        var novas = new List<TenantCharge>();
        var situacao = SituacaoMensalidade.ForaDeCobranca;

        // BillingStartsOn é o que implementa os 15 dias grátis: antes da
        // competência dele não há vencimento, e a loja fica fora.
        var vencimento = CondicoesComerciais.VencimentoDaMensalidade(tenant, comp);
        if (tenant.MonthlyPrice > 0 && vencimento is not null)
        {
            if (existentes.Any(c => c.Kind == TenantChargeKind.Mensalidade && c.ReferenceMonth == comp))
            {
                situacao = SituacaoMensalidade.JaExistia;
            }
            else
            {
                var calculo = CondicoesComerciais.CalcularMensalidade(tenant.MonthlyPrice, descontos, comp);

                // Desconto integral não vira cobrança de R$ 0,00: o gateway não
                // aceita, e a régua já ignora valor zero. A prévia das condições
                // continua mostrando o mês como "sem cobrança".
                if (calculo.Valor <= 0)
                {
                    situacao = SituacaoMensalidade.ZeradaPorDesconto;
                }
                else
                {
                    situacao = SituacaoMensalidade.Criada;
                    novas.Add(new TenantCharge
                    {
                        TenantId        = tenant.Id,
                        Kind            = TenantChargeKind.Mensalidade,
                        // Cópia do valor vigente AGORA — ver TenantCharge.Amount.
                        Amount          = calculo.Valor,
                        GrossAmount     = calculo.Base,
                        DiscountAmount  = calculo.Desconto,
                        DiscountSummary = calculo.DescricaoDesconto,
                        ReferenceMonth  = comp,
                        DueDate         = vencimento.Value,
                        AutoGenerated   = true,
                    });
                }
            }
        }

        foreach (var parcela in ParcelasEsperadas(tenant, existentes))
        {
            // Parcela futura nasce no mês dela, não antes: o gateway emite tudo
            // que está em aberto, e o lojista receberia a implantação inteira de
            // uma vez.
            if (parcela.Competencia > comp || parcela.Valor <= 0) continue;

            // Uma implantação por competência (índice único). Parcela de outro
            // número ocupando o mês é resto de renegociação que o recálculo
            // resolve antes de chegar aqui.
            if (existentes.Any(c => c.Kind == TenantChargeKind.Implantacao
                                 && (c.InstallmentNumber == parcela.Numero || c.ReferenceMonth == parcela.Competencia)))
                continue;

            novas.Add(new TenantCharge
            {
                TenantId          = tenant.Id,
                Kind              = TenantChargeKind.Implantacao,
                Amount            = parcela.Valor,
                ReferenceMonth    = parcela.Competencia,
                DueDate           = parcela.Vencimento,
                InstallmentNumber = parcela.Numero,
                InstallmentCount  = parcela.Total,
                AutoGenerated     = true,
            });
        }

        return new CobrancasFaltantes(novas, situacao);
    }

    /// <summary>Parcelas que as condições mandam existir. Vazio quando a loja
    /// tem implantação lançada à mão (ou anterior ao parcelamento): essa é a
    /// palavra final, e gerar parcelas por cima cobraria a implantação duas vezes.
    ///
    /// Parcela paga ou editada à mão entra como valor fixo; o saldo se divide
    /// entre as outras.</summary>
    internal static IReadOnlyList<ParcelaImplantacao> ParcelasEsperadas(Tenant tenant, IEnumerable<TenantCharge> cobrancasDaLoja)
    {
        var implantacoes = cobrancasDaLoja.Where(c => c.Kind == TenantChargeKind.Implantacao).ToList();

        if (implantacoes.Any(c => c.InstallmentNumber is null)) return [];

        var fixas = implantacoes
            .Where(c => c.PaidAt != null || !c.AutoGenerated)
            .GroupBy(c => c.InstallmentNumber!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));

        return CondicoesComerciais.ParcelasDaImplantacao(
            tenant.SetupFee, tenant.SetupInstallments, tenant.SetupFirstDueDate, fixas);
    }

    public async Task<BillingResumoDto> ObterResumoAsync(DateTime competencia)
    {
        var comp  = NormalizarCompetencia(competencia);
        var hoje  = DateTime.UtcNow.Date;

        var ativas = await _catalog.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active)
            .Select(t => t.MonthlyPrice)
            .ToListAsync();

        var doMes = await _catalog.TenantCharges
            .AsNoTracking()
            .Where(c => c.ReferenceMonth == comp)
            .Select(c => new { c.Amount, c.PaidAt, c.DueDate })
            .ToListAsync();

        // Vencido acumulado varre TODAS as competências de propósito: dívida de
        // março continua sendo dívida em maio. Um resumo que só olhasse o mês
        // corrente mostraria inadimplência zero logo depois da virada, o que é
        // exatamente o oposto da verdade.
        var vencidoAcumulado = await _catalog.TenantCharges
            .AsNoTracking()
            .Where(c => c.PaidAt == null && c.DueDate < hoje)
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;

        return new BillingResumoDto
        {
            Competencia      = comp,
            MrrContratado    = ativas.Sum(),
            LojasPagantes    = ativas.Count(p => p > 0),
            LojasSemCobranca = ativas.Count(p => p <= 0),
            Faturado         = doMes.Sum(c => c.Amount),
            Recebido         = doMes.Where(c => c.PaidAt != null).Sum(c => c.Amount),
            EmAberto         = doMes.Where(c => c.PaidAt == null).Sum(c => c.Amount),
            VencidoAcumulado = vencidoAcumulado,
            QtdCobrancas     = doMes.Count,
            QtdVencidas      = doMes.Count(c => c.PaidAt == null && c.DueDate < hoje),
        };
    }

    public async Task<List<TenantChargeDto>> ListarPorCompetenciaAsync(DateTime competencia)
    {
        var comp = NormalizarCompetencia(competencia);

        return await MapearAsync(_catalog.TenantCharges
            .AsNoTracking()
            .Where(c => c.ReferenceMonth == comp)
            .OrderBy(c => c.DueDate));
    }

    public async Task<List<TenantChargeDto>> ListarPorTenantAsync(Guid tenantId)
    {
        return await MapearAsync(_catalog.TenantCharges
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.ReferenceMonth)
            .ThenBy(c => c.Kind));
    }

    public async Task<TenantChargeDto> DefinirPagamentoAsync(Guid chargeId, DateTime? pagoEm)
    {
        var cobranca = await _catalog.TenantCharges.FirstOrDefaultAsync(c => c.Id == chargeId)
            ?? throw new InvalidOperationException("Cobrança não encontrada.");

        var pagamento = NormalizarDataPagamento(pagoEm);

        // Data futura é quase sempre erro de digitação (ano errado), e uma baixa
        // com data futura envenena o relatório de recebidos sem deixar rastro.
        if (pagamento.HasValue && pagamento.Value.Date > DateTime.UtcNow.Date)
            throw new InvalidOperationException("A data de pagamento não pode ser futura.");

        var pagamentoAnterior = cobranca.PaidAt;
        cobranca.PaidAt = pagamento;
        if (_referrals is not null)
            await _referrals.SynchronizeChargeAsync(cobranca, pagamentoAnterior);
        await _catalog.SaveChangesAsync();

        // Baixa manual (pagamento por fora, Pix direto) reativa na hora do mesmo
        // jeito que a do webhook.
        await ReavaliarLojaAsync(cobranca.TenantId);

        var lista = await MapearAsync(_catalog.TenantCharges.AsNoTracking().Where(c => c.Id == chargeId));
        return lista[0];
    }

    public Task<List<TenantChargeDto>> ListarEmAbertoDaLojaAsync(Guid tenantId) =>
        MapearAsync(_catalog.TenantCharges
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.PaidAt == null && c.Amount > 0)
            .OrderBy(c => c.DueDate));

    public async Task<int> BaixarCobrancasEmAbertoAsync(Guid tenantId)
    {
        var emAberto = await _catalog.TenantCharges
            .Where(c => c.TenantId == tenantId && c.PaidAt == null && c.Amount > 0)
            .OrderBy(c => c.DueDate)
            .ToListAsync();

        if (emAberto.Count == 0) return 0;

        var hoje = NormalizarData(DateTime.UtcNow);
        foreach (var cobranca in emAberto)
        {
            cobranca.PaidAt = hoje;
            // Mesma sincronização da baixa avulsa: a comissão do parceiro sai da
            // cobrança paga, e pular isto aqui criaria baixa sem comissão.
            if (_referrals is not null)
                await _referrals.SynchronizeChargeAsync(cobranca, null);
        }

        await _catalog.SaveChangesAsync();
        _logger.LogInformation("{Quantidade} cobrança(s) da loja {TenantId} baixadas em lote pelo painel.",
            emAberto.Count, tenantId);

        await ReavaliarLojaAsync(tenantId);
        return emAberto.Count;
    }

    /// <summary>Junta com Tenant pra trazer nome/slug e calcula "vencida" no
    /// servidor — a regra de vencimento vive num lugar só.</summary>
    private async Task<List<TenantChargeDto>> MapearAsync(IQueryable<TenantCharge> query)
    {
        var hoje = DateTime.UtcNow.Date;

        return await query
            .Join(_catalog.Tenants.AsNoTracking(),
                  c => c.TenantId,
                  t => t.Id,
                  (c, t) => new TenantChargeDto
                  {
                      Id          = c.Id,
                      TenantId    = c.TenantId,
                      TenantNome  = t.DisplayName ?? t.Slug,
                      TenantSlug  = t.Slug,
                      Tipo        = c.Kind.ToString(),
                      Valor       = c.Amount,
                      Competencia = c.ReferenceMonth,
                      Vencimento  = c.DueDate,
                      PagoEm      = c.PaidAt,
                      Observacao  = c.Notes,
                      Vencida     = c.PaidAt == null && c.DueDate < hoje,
                      EmitidaNoGateway = c.ExternalChargeId != null,
                      LinkPagamento    = c.PaymentUrl,
                      Automatica        = c.AutoGenerated,
                      ValorBruto        = c.GrossAmount,
                      Desconto          = c.DiscountAmount,
                      DescricaoDesconto = c.DiscountSummary,
                      Parcela           = c.InstallmentNumber,
                      TotalParcelas     = c.InstallmentCount,
                  })
            .ToListAsync();
    }

    // ── Lançamentos manuais ─────────────────────────────────────────────────

    public async Task<TenantChargeDto> CriarCobrancaAsync(CriarCobrancaRequest request)
    {
        if (!Enum.TryParse<TenantChargeKind>(request.Tipo, ignoreCase: true, out var tipo))
            throw new InvalidOperationException("Tipo de cobrança inválido: use Implantacao ou Mensalidade.");

        if (!await _catalog.Tenants.AnyAsync(t => t.Id == request.TenantId))
            throw new InvalidOperationException("Loja não encontrada.");

        var competencia = NormalizarCompetencia(request.Competencia);
        var vencimento  = NormalizarData(request.Vencimento);

        // Checagem antes do INSERT só para dar uma mensagem que explica o
        // problema. O índice único é quem garante de fato — entre esta consulta
        // e o SaveChanges cabe outra requisição, e é ele que resolve a corrida.
        if (await _catalog.TenantCharges.AnyAsync(c =>
                c.TenantId == request.TenantId && c.Kind == tipo && c.ReferenceMonth == competencia))
            throw new InvalidOperationException(
                $"Já existe uma cobrança de {tipo} para esta loja em {competencia:MM/yyyy}. Edite a existente em vez de criar outra.");

        var cobranca = new TenantCharge
        {
            TenantId       = request.TenantId,
            Kind           = tipo,
            Amount         = request.Valor,
            ReferenceMonth = competencia,
            DueDate        = vencimento,
            Notes          = string.IsNullOrWhiteSpace(request.Observacao) ? null : request.Observacao.Trim(),
        };

        _catalog.TenantCharges.Add(cobranca);
        try
        {
            await _catalog.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // `when (await ...)` não compila em filtro de catch, então a
            // checagem vem aqui dentro. A entidade recusada precisa sair do
            // rastreamento antes da consulta: senão o EF tenta reenviar o mesmo
            // INSERT e a segunda falha esconde a primeira.
            _catalog.Entry(cobranca).State = EntityState.Detached;

            if (await ExisteAsync(request.TenantId, tipo, competencia))
                throw new InvalidOperationException(
                    $"Já existe uma cobrança de {tipo} para esta loja em {competencia:MM/yyyy}.");
            throw;
        }

        var lista = await MapearAsync(_catalog.TenantCharges.AsNoTracking().Where(c => c.Id == cobranca.Id));
        return lista[0];
    }

    public async Task<TenantChargeDto> AtualizarCobrancaAsync(Guid chargeId, AtualizarCobrancaRequest request)
    {
        // Mesma trava da emissão: uma rodada em andamento pode estar emitindo
        // exatamente esta cobrança com o valor antigo.
        await _emissaoLock.WaitAsync();
        try
        {
            var cobranca = await _catalog.TenantCharges.FirstOrDefaultAsync(c => c.Id == chargeId)
                ?? throw new InvalidOperationException("Cobrança não encontrada.");

            if (cobranca.PaidAt.HasValue)
                throw new InvalidOperationException(
                    "Cobrança paga não pode ser alterada. Reabra a cobrança, altere e dê baixa de novo — assim a comissão do parceiro é refeita junto.");

            var vencimento = NormalizarData(request.Vencimento);
            var mudaFatura = cobranca.Amount != request.Valor || cobranca.DueDate != vencimento;

            if (mudaFatura)
            {
                // A fatura que o lojista tem na mão precisa sair junto. A
                // cobrança volta a ficar sem id externo e é reemitida com o valor
                // novo na próxima rodada.
                var pendencias = new List<string>();
                if (!await CancelarNoGatewayAsync(cobranca, await SlugDaLojaAsync(cobranca.TenantId), pendencias, CancellationToken.None))
                    throw new InvalidOperationException(pendencias[0]);

                // Ajuste à mão é decisão explícita: a próxima renegociação não
                // passa por cima dele. Só observação não conta como ajuste.
                cobranca.AutoGenerated = false;
            }

            cobranca.Amount  = request.Valor;
            cobranca.DueDate = vencimento;
            cobranca.Notes   = string.IsNullOrWhiteSpace(request.Observacao) ? null : request.Observacao.Trim();

            await _catalog.SaveChangesAsync();
        }
        finally
        {
            _emissaoLock.Release();
        }

        var lista = await MapearAsync(_catalog.TenantCharges.AsNoTracking().Where(c => c.Id == chargeId));
        return lista[0];
    }

    public async Task ExcluirCobrancaAsync(Guid chargeId)
    {
        await _emissaoLock.WaitAsync();
        try
        {
            var cobranca = await _catalog.TenantCharges.FirstOrDefaultAsync(c => c.Id == chargeId)
                ?? throw new InvalidOperationException("Cobrança não encontrada.");

            if (cobranca.PaidAt.HasValue)
                throw new InvalidOperationException(
                    "Cobrança paga não pode ser excluída. Reabra antes — e considere que reabrir também desfaz a comissão gerada por ela.");

            // Cobrança que o gerador recriaria sozinho: excluir daria a impressão
            // de perdão e ela voltaria na rodada seguinte, em até 12 horas. Vale
            // também pra linha editada à mão — o que decide é se as condições da
            // loja mandam cobrar aquele mês, não quem criou a linha. Mensalidade
            // de competência passada pode sair: o gerador só cria a do mês corrente.
            var tenant = await _catalog.Tenants.AsNoTracking().FirstAsync(t => t.Id == cobranca.TenantId);
            var seriaRecriada = cobranca.Kind == TenantChargeKind.Implantacao
                ? cobranca.InstallmentNumber is not null && tenant.SetupFee > 0
                : cobranca.ReferenceMonth >= NormalizarCompetencia(DateTime.UtcNow)
                  && tenant.MonthlyPrice > 0
                  && CondicoesComerciais.VencimentoDaMensalidade(tenant, cobranca.ReferenceMonth) is not null;

            if (seriaRecriada)
                throw new InvalidOperationException(cobranca.Kind == TenantChargeKind.Implantacao
                    ? "Esta parcela vem das condições comerciais da loja e seria gerada de novo. Mude o valor ou as parcelas da implantação nas condições da loja, ou edite a parcela para fixar outro valor."
                    : "Esta mensalidade vem das condições comerciais da loja e seria gerada de novo. Para não cobrar este mês, edite a cobrança para R$ 0,00 ou cadastre um desconto de 100% nesta competência.");

            var pendencias = new List<string>();
            if (!await CancelarNoGatewayAsync(cobranca, await SlugDaLojaAsync(cobranca.TenantId), pendencias, CancellationToken.None))
                throw new InvalidOperationException(pendencias[0]);

            _catalog.TenantCharges.Remove(cobranca);
            await _catalog.SaveChangesAsync();
        }
        finally
        {
            _emissaoLock.Release();
        }
    }

    private async Task<string> SlugDaLojaAsync(Guid tenantId) =>
        await _catalog.Tenants.Where(t => t.Id == tenantId).Select(t => t.Slug).FirstOrDefaultAsync() ?? "loja";

    /// <summary>Cancela no gateway a fatura de uma cobrança emitida, antes de a
    /// cobrança local mudar ou sumir. Devolve false (com a razão em
    /// <paramref name="pendencias"/>) quando não deu — e aí a cobrança local não
    /// deve ser tocada: a fatura continua valendo na mão do lojista, e mudar só o
    /// nosso lado criaria dois valores para a mesma dívida.</summary>
    private async Task<bool> CancelarNoGatewayAsync(
        TenantCharge cobranca, string slug, List<string> pendencias, CancellationToken ct)
    {
        if (cobranca.ExternalChargeId is null) return true;

        if (_gateway is null || !_gateway.IsConfigured
            || !string.Equals(cobranca.Gateway, _gateway.Name, StringComparison.Ordinal))
        {
            pendencias.Add($"{slug}: a fatura da {Rotulo(cobranca)} já foi emitida em {cobranca.Gateway ?? "outro gateway"}, que não está configurado para cancelá-la. Nada foi alterado nela.");
            return false;
        }

        try
        {
            await _gateway.CancelarCobrancaAsync(cobranca.ExternalChargeId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha ao cancelar no gateway a cobrança {ChargeId} do tenant {Slug}", cobranca.Id, slug);
            pendencias.Add($"{slug}: não foi possível cancelar a fatura da {Rotulo(cobranca)} no gateway ({ex.Message}). Nada foi alterado nela.");
            return false;
        }

        // Gravado na hora, pelo mesmo motivo do SaveChanges por cobrança na
        // emissão: a fatura já não existe lá, e perder isto deixaria a linha
        // apontando pra uma cobrança apagada que nunca seria reemitida.
        cobranca.Gateway          = null;
        cobranca.ExternalChargeId = null;
        cobranca.PaymentUrl       = null;
        await _catalog.SaveChangesAsync(ct);
        return true;
    }

    private static string Rotulo(TenantCharge cobranca) => cobranca.Kind == TenantChargeKind.Implantacao
        ? cobranca.InstallmentNumber is { } numero ? $"implantação {numero}/{cobranca.InstallmentCount}" : "implantação"
        : $"mensalidade {cobranca.ReferenceMonth:MM/yyyy}";

    // =========================================================================
    // RENEGOCIAÇÃO
    // =========================================================================

    public async Task<SincronizacaoCobrancasResultDto> SincronizarCobrancasDaLojaAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        await _emissaoLock.WaitAsync(ct);
        try
        {
            return await SincronizarComTravaAsync(tenantId, ct);
        }
        finally
        {
            _emissaoLock.Release();
        }
    }

    /// <summary>Cobrança do jeito que as condições atuais mandam que ela seja.</summary>
    private sealed record CobrancaEsperada(
        decimal Valor, decimal? Bruto, decimal Desconto, string? Resumo,
        DateTime Vencimento, int? TotalParcelas);

    private async Task<SincronizacaoCobrancasResultDto> SincronizarComTravaAsync(Guid tenantId, CancellationToken ct)
    {
        var resultado = new SincronizacaoCobrancasResultDto();

        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Loja não encontrada.");

        var descontos = await _catalog.TenantBillingDiscounts.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(ct);

        var cobrancas = await _catalog.TenantCharges
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(ct);

        var competenciaAtual  = NormalizarCompetencia(DateTime.UtcNow);
        var parcelas          = ParcelasEsperadas(tenant, cobrancas);
        var implantacaoManual = cobrancas.Any(c => c.Kind == TenantChargeKind.Implantacao && c.InstallmentNumber is null);
        var removidas         = new HashSet<Guid>();

        // Só o que está em aberto e nasceu das condições. Paga é fato consumado;
        // manual é decisão de alguém que o recálculo não conhece.
        foreach (var cobranca in cobrancas.Where(c => c.AutoGenerated && c.PaidAt == null))
        {
            CobrancaEsperada? esperada;

            if (cobranca.Kind == TenantChargeKind.Mensalidade)
            {
                // Dívida de competência passada não se renegocia sozinha: baixar
                // o preço hoje não pode perdoar o que vencia em agosto.
                if (cobranca.ReferenceMonth < competenciaAtual) continue;

                var vencimento = CondicoesComerciais.VencimentoDaMensalidade(tenant, cobranca.ReferenceMonth);
                var calculo = CondicoesComerciais.CalcularMensalidade(tenant.MonthlyPrice, descontos, cobranca.ReferenceMonth);

                esperada = tenant.MonthlyPrice > 0 && vencimento is not null && calculo.Valor > 0
                    ? new CobrancaEsperada(calculo.Valor, calculo.Base, calculo.Desconto, calculo.DescricaoDesconto, vencimento.Value, null)
                    : null;
            }
            else
            {
                if (implantacaoManual) continue;

                // Parcela que mudou de mês é refeita, não movida: trocar a
                // competência de uma linha pode colidir no índice único com outra
                // parcela que ainda não saiu do lugar.
                var parcela = parcelas.FirstOrDefault(p => p.Numero == cobranca.InstallmentNumber);
                esperada = parcela is not null && parcela.Valor > 0 && parcela.Competencia == cobranca.ReferenceMonth
                    ? new CobrancaEsperada(parcela.Valor, null, 0m, null, parcela.Vencimento, parcela.Total)
                    : null;
            }

            if (esperada is null)
            {
                if (!await CancelarNoGatewayAsync(cobranca, tenant.Slug, resultado.Pendencias, ct)) continue;

                _catalog.TenantCharges.Remove(cobranca);
                removidas.Add(cobranca.Id);
                resultado.Removidas++;
                continue;
            }

            var mudaFatura  = cobranca.Amount != esperada.Valor || cobranca.DueDate != esperada.Vencimento;
            var mudaDetalhe = cobranca.GrossAmount != esperada.Bruto
                           || cobranca.DiscountAmount != esperada.Desconto
                           || cobranca.DiscountSummary != esperada.Resumo
                           || cobranca.InstallmentCount != esperada.TotalParcelas;

            if (!mudaFatura && !mudaDetalhe) continue;
            if (mudaFatura && !await CancelarNoGatewayAsync(cobranca, tenant.Slug, resultado.Pendencias, ct)) continue;

            cobranca.Amount           = esperada.Valor;
            cobranca.DueDate          = esperada.Vencimento;
            cobranca.GrossAmount      = esperada.Bruto;
            cobranca.DiscountAmount   = esperada.Desconto;
            cobranca.DiscountSummary  = esperada.Resumo;
            cobranca.InstallmentCount = esperada.TotalParcelas;
            resultado.Atualizadas++;
        }

        // Remoções antes das criações: uma parcela que mudou de mês libera a
        // competência que a nova pode ocupar.
        await _catalog.SaveChangesAsync(ct);

        // Loja suspensa não ganha cobrança nova — mesma regra do gerador mensal.
        if (tenant.Status == TenantStatus.Active)
        {
            var restantes = cobrancas.Where(c => !removidas.Contains(c.Id)).ToList();
            var novas = CobrancasQueFaltam(tenant, descontos, restantes, competenciaAtual).Novas;

            if (novas.Count > 0)
            {
                _catalog.TenantCharges.AddRange(novas);
                await _catalog.SaveChangesAsync(ct);
                resultado.Criadas = novas.Count;
            }
        }

        if (resultado.Criadas + resultado.Atualizadas + resultado.Removidas > 0 || resultado.Pendencias.Count > 0)
            _logger.LogInformation(
                "Condições do tenant {Slug} aplicadas: {Criadas} criadas, {Atualizadas} atualizadas, {Removidas} removidas, {Pendencias} pendências",
                tenant.Slug, resultado.Criadas, resultado.Atualizadas, resultado.Removidas, resultado.Pendencias.Count);

        return resultado;
    }

    private Task<bool> ExisteAsync(Guid tenantId, TenantChargeKind tipo, DateTime competencia) =>
        _catalog.TenantCharges.AnyAsync(c =>
            c.TenantId == tenantId && c.Kind == tipo && c.ReferenceMonth == competencia);

    /// <summary>Trunca para meia-noite UTC, mesmo tratamento de
    /// NormalizarDataPagamento: vencimento é um dia, não um instante, e guardar
    /// a hora do clique faria a data "mudar de dia" lida de outro fuso.</summary>
    private static DateTime NormalizarData(DateTime data)
    {
        var utc = data.Kind == DateTimeKind.Unspecified ? data : data.ToUniversalTime();
        return new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    // =========================================================================
    // AUTOMAÇÃO DA COBRANÇA (RB-01)
    // =========================================================================

    // Uma emissão por vez neste processo. Enquanto só existia o job de 12 em 12
    // horas, a rodada era idempotente por construção (só pega cobrança sem id
    // externo). Com o botão "Emitir no Asaas agora" passaram a existir dois
    // chamadores, e duas rodadas simultâneas leriam as MESMAS pendentes antes de
    // qualquer uma gravar o id externo: cada cobrança sairia duas vezes no
    // gateway — boleto/Pix real em dobro na mão do lojista. Em fila, a segunda
    // rodada já encontra os ids gravados e não emite nada. Semáforo em memória
    // basta pelo mesmo motivo do provisionamento: a API roda em instância única.
    private static readonly SemaphoreSlim _emissaoLock = new(1, 1);

    public async Task<EmissaoGatewayResultDto> EmitirCobrancasPendentesAsync(CancellationToken ct = default)
    {
        await _emissaoLock.WaitAsync(ct);
        try
        {
            return await EmitirPendentesComTravaAsync(null, ct);
        }
        finally
        {
            _emissaoLock.Release();
        }
    }

    public async Task<EmissaoGatewayResultDto> EmitirCobrancasPendentesDaLojaAsync(Guid tenantId, CancellationToken ct = default)
    {
        await _emissaoLock.WaitAsync(ct);
        try
        {
            return await EmitirPendentesComTravaAsync(tenantId, ct);
        }
        finally
        {
            _emissaoLock.Release();
        }
    }

    private async Task<EmissaoGatewayResultDto> EmitirPendentesComTravaAsync(Guid? tenantId, CancellationToken ct)
    {
        var resultado = new EmissaoGatewayResultDto();

        if (_gateway is null || !_gateway.IsConfigured)
        {
            resultado.Pendencias.Add("Nenhum gateway de cobrança configurado.");
            return resultado;
        }

        // Valor zero é cortesia/piloto/tenant-zero: gerar cobrança de R$ 0,00 no
        // gateway só produziria fatura confusa pro lojista.
        var pendentes = await _catalog.TenantCharges
            .Where(c => c.PaidAt == null && c.ExternalChargeId == null && c.Amount > 0)
            .Where(c => tenantId == null || c.TenantId == tenantId)
            .OrderBy(c => c.DueDate)
            .ToListAsync(ct);

        if (pendentes.Count == 0)
        {
            // Sem este número a resposta de uma rodada vazia era "0 já emitidas"
            // mesmo com tudo no gateway — e o botão do Financeiro não tinha como
            // dizer por que não mandou nada.
            resultado.JaEmitidas = await _catalog.TenantCharges
                .CountAsync(c => c.PaidAt == null && c.ExternalChargeId != null, ct);
            return resultado;
        }

        var tenantIds = pendentes.Select(c => c.TenantId).Distinct().ToList();
        var tenants = await _catalog.Tenants
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        foreach (var cobranca in pendentes)
        {
            if (!tenants.TryGetValue(cobranca.TenantId, out var tenant)) continue;

            // Loja suspensa continua sendo cobrada: suspensão é consequência da
            // inadimplência, não perdão dela. Só o tenant isento sai da régua.
            if (tenant.PaymentStatus == TenantPaymentStatus.Isento) continue;

            try
            {
                // Cliente primeiro, e persistido NA HORA. Quando isto morava
                // dentro da emissão, uma cobrança recusada descartava o id do
                // cliente recém-criado e a rodada seguinte criava outro pro
                // mesmo CNPJ — uma duplicata no gateway por retentativa. É o
                // mesmo raciocínio do SaveChanges por cobrança logo abaixo:
                // efeito que já aconteceu lá fora precisa estar gravado aqui.
                if (string.IsNullOrWhiteSpace(tenant.BillingCustomerId))
                {
                    tenant.BillingCustomerId = await _gateway.GarantirClienteAsync(tenant, ct);
                    await _catalog.SaveChangesAsync(ct);
                }

                var emitida = await _gateway.EmitirCobrancaAsync(cobranca, tenant, ct);

                cobranca.Gateway          = _gateway.Name;
                cobranca.ExternalChargeId = emitida.ExternalId;
                cobranca.PaymentUrl       = emitida.PaymentUrl;

                // Salva a cada cobrança, e não em lote no fim: a chamada ao
                // gateway já aconteceu e é irreversível. Se a rodada estourar na
                // décima loja, um SaveChanges único perderia o id externo das
                // nove primeiras — que já têm cobrança emitida lá — e a próxima
                // execução cobraria todas de novo.
                await _catalog.SaveChangesAsync(ct);
                resultado.Emitidas++;
            }
            catch (Exception ex)
            {
                // Uma loja sem CNPJ não pode impedir a cobrança das outras.
                _logger.LogError(ex, "Falha ao emitir cobrança {ChargeId} do tenant {Slug}",
                    cobranca.Id, tenant.Slug);
                resultado.Pendencias.Add($"{tenant.Slug}: {ex.Message}");
                _catalog.Entry(cobranca).State = EntityState.Unchanged;
            }
        }

        resultado.JaEmitidas = await _catalog.TenantCharges
            .CountAsync(c => c.PaidAt == null && c.ExternalChargeId != null, ct);

        return resultado;
    }

    public async Task<bool> RegistrarPagamentoExternoAsync(
        string gateway, string externalChargeId, bool paga, DateTime? pagoEm)
    {
        var cobranca = await _catalog.TenantCharges
            .FirstOrDefaultAsync(c => c.Gateway == gateway && c.ExternalChargeId == externalChargeId);

        // Não é erro: o gateway avisa sobre tudo que acontece na conta, e nem
        // toda cobrança lá é mensalidade nossa.
        if (cobranca is null) return false;

        var pagamentoAnterior = cobranca.PaidAt;

        if (paga)
        {
            // Idempotência: o Asaas reenvia webhook, e PAYMENT_CONFIRMED e
            // PAYMENT_RECEIVED chegam os dois pro mesmo Pix. Sem esta saída, a
            // segunda entrega reescreveria a data da baixa e mandaria o
            // ReferralCommissionService recalcular comissão já apurada.
            if (pagamentoAnterior is not null) return true;

            cobranca.PaidAt = NormalizarData(pagoEm ?? DateTime.UtcNow);
        }
        else
        {
            if (pagamentoAnterior is null) return true;
            cobranca.PaidAt = null;
        }

        if (_referrals is not null)
            await _referrals.SynchronizeChargeAsync(cobranca, pagamentoAnterior);

        await _catalog.SaveChangesAsync();

        _logger.LogInformation(
            "Cobrança {ChargeId} {Acao} por webhook do {Gateway}",
            cobranca.Id, paga ? "baixada" : "reaberta", gateway);

        await ReavaliarLojaAsync(cobranca.TenantId);

        return true;
    }

    public async Task<ReguaCobrancaResultDto> AplicarReguaDeCobrancaAsync(CancellationToken ct = default)
    {
        var resultado = new ReguaCobrancaResultDto();
        var limite = DateTime.UtcNow.Date.AddDays(-DiasDeCarencia);

        // Isento fica fora dos dois lados: nem suspende nem "reativa", porque
        // nunca deveria ter sido suspenso por dinheiro.
        var tenants = await _catalog.Tenants
            .Where(t => t.PaymentStatus != TenantPaymentStatus.Isento)
            .ToListAsync(ct);

        var inadimplentes = await _catalog.TenantCharges
            .Where(c => c.PaidAt == null && c.Amount > 0 && c.DueDate < limite)
            .Select(c => c.TenantId)
            .Distinct()
            .ToListAsync(ct);

        var vencidos = inadimplentes.ToHashSet();
        var mudancas = new List<(Tenant Tenant, MudancaDaRegua Mudanca)>();

        foreach (var tenant in tenants)
        {
            var mudanca = DecidirRegua(tenant, deve: vencidos.Contains(tenant.Id));
            if (mudanca == MudancaDaRegua.Suspensa)  resultado.Suspensos.Add(tenant.Slug);
            if (mudanca == MudancaDaRegua.Reativada) resultado.Reativados.Add(tenant.Slug);
            if (mudanca != MudancaDaRegua.Nenhuma)   mudancas.Add((tenant, mudanca));
        }

        if (resultado.Suspensos.Count > 0 || resultado.Reativados.Count > 0)
            _logger.LogInformation("Régua de cobrança: {Suspensos} suspensos, {Reativados} reativados",
                resultado.Suspensos.Count, resultado.Reativados.Count);

        await _catalog.SaveChangesAsync(ct);

        foreach (var (tenant, mudanca) in mudancas)
            DepoisDaMudanca(tenant, mudanca);

        return resultado;
    }

    private enum MudancaDaRegua { Nenhuma, Suspensa, Reativada }

    /// <summary>A regra da régua para uma loja, aplicada no objeto. Uma só, pro
    /// job e pra reavaliação imediata depois de um pagamento.</summary>
    private static MudancaDaRegua DecidirRegua(Tenant tenant, bool deve)
    {
        if (tenant.PaymentStatus == TenantPaymentStatus.Isento) return MudancaDaRegua.Nenhuma;

        if (deve && tenant.Status == TenantStatus.Active)
        {
            tenant.Status = TenantStatus.Suspended;
            tenant.SuspendedByBilling = true;
            tenant.PaymentStatus = TenantPaymentStatus.Atrasado;
            return MudancaDaRegua.Suspensa;
        }

        if (!deve && tenant.Status == TenantStatus.Suspended && tenant.SuspendedByBilling)
        {
            // SuspendedByBilling é o que impede a régua de reabrir uma loja que
            // o dono da plataforma suspendeu à mão por outro motivo (fim de
            // contrato, abuso). Só volta quem a própria régua derrubou.
            //
            // Essa marca era o PaymentStatus.Atrasado, e isso era um bug: marcar
            // "Pago" à mão na lista apagava a marca, e a loja não voltava nem
            // depois de o pagamento entrar.
            tenant.Status = TenantStatus.Active;
            tenant.SuspendedByBilling = false;
            tenant.PaymentStatus = TenantPaymentStatus.Pago;
            return MudancaDaRegua.Reativada;
        }

        if (!deve && tenant.PaymentStatus == TenantPaymentStatus.Atrasado)
        {
            // Quitou antes de a carência estourar: nunca chegou a ser
            // suspenso, mas o status precisa voltar pra Pago.
            tenant.PaymentStatus = TenantPaymentStatus.Pago;
        }

        return MudancaDaRegua.Nenhuma;
    }

    /// <summary>Reaplica a régua a uma loja logo depois de um pagamento (ou de um
    /// estorno), sem esperar a rodada de 12 horas. Antes disto o webhook dava
    /// baixa e a loja paga continuava fora do ar até o job passar — o lojista
    /// pagava e via "loja suspensa" por horas.</summary>
    private async Task ReavaliarLojaAsync(Guid tenantId)
    {
        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant is null) return;

        var limite = DateTime.UtcNow.Date.AddDays(-DiasDeCarencia);
        var deve = await _catalog.TenantCharges.AnyAsync(c =>
            c.TenantId == tenantId && c.PaidAt == null && c.Amount > 0 && c.DueDate < limite);

        var mudanca = DecidirRegua(tenant, deve);
        await _catalog.SaveChangesAsync();

        if (mudanca != MudancaDaRegua.Nenhuma)
        {
            _logger.LogInformation("Loja {Slug} {Mudanca} pela régua logo após pagamento/estorno",
                tenant.Slug, mudanca == MudancaDaRegua.Reativada ? "reativada" : "suspensa");
            DepoisDaMudanca(tenant, mudanca);
        }
    }

    private void DepoisDaMudanca(Tenant tenant, MudancaDaRegua mudanca)
    {
        // O TenantResolutionMiddleware guarda o status da loja por 30 segundos.
        // Sem limpar, a loja recém-reativada ainda responderia "suspensa" pra
        // quem acabou de pagar e voltou pro painel.
        _cache?.Remove($"tenant-slug:{tenant.Slug}");
        if (!string.IsNullOrWhiteSpace(tenant.CustomDomain))
            _cache?.Remove($"tenant-domain:{tenant.CustomDomain.ToLowerInvariant()}");

        if (_notifier is null) return;
        if (mudanca == MudancaDaRegua.Suspensa)  _notifier.NotificarLojaSuspensa(tenant.Id);
        if (mudanca == MudancaDaRegua.Reativada) _notifier.NotificarLojaReativada(tenant.Id);
    }
}
