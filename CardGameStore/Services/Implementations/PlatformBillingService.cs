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

namespace CardGameStore.Services.Implementations;

public class PlatformBillingService : IPlatformBillingService
{
    private readonly CatalogDbContext _catalog;
    private readonly ILogger<PlatformBillingService> _logger;
    private readonly IReferralCommissionService? _referrals;

    public PlatformBillingService(
        CatalogDbContext catalog,
        ILogger<PlatformBillingService> logger,
        IReferralCommissionService? referrals = null)
    {
        _catalog = catalog;
        _logger  = logger;
        _referrals = referrals;
    }

    /// <summary>Reduz qualquer data ao dia 1 do mês, 00:00 UTC. Toda competência
    /// passa por aqui: sem isso, "março" gravado como dia 3 e como dia 17 viram
    /// duas competências diferentes e a unique index que impede cobrança
    /// duplicada deixa de proteger.</summary>
    private static DateTime NormalizarCompetencia(DateTime data) =>
        new(data.Year, data.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Vencimento da competência, preservando o dia que o cliente já
    /// conhece e sem estourar em mês curto: dia 31 vira 28 (ou 29) em fevereiro,
    /// 30 em abril. Sem esse clamp, gerar a competência de fevereiro pra um
    /// cliente que assinou dia 31 lançaria ArgumentOutOfRangeException e
    /// derrubaria a geração do mês INTEIRO, não só a daquele cliente.</summary>
    private static DateTime VencimentoNaCompetencia(DateTime competencia, int diaDesejado)
    {
        var ultimoDia = DateTime.DaysInMonth(competencia.Year, competencia.Month);
        var dia = Math.Min(diaDesejado, ultimoDia);
        return new DateTime(competencia.Year, competencia.Month, dia, 0, 0, 0, DateTimeKind.Utc);
    }

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
        var fimDaCompetencia = comp.AddMonths(1);

        // Elegíveis: loja ativa, com mensalidade definida, e que já entrou em
        // cobrança dentro (ou antes) desta competência. BillingStartsOn é o que
        // implementa os 15 dias grátis. Dependendo do dia da assinatura, a
        // primeira cobrança pode cair ainda nesta competência ou na seguinte.
        var elegiveis = await _catalog.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active
                     && t.MonthlyPrice > 0
                     && t.BillingStartsOn != null
                     && t.BillingStartsOn < fimDaCompetencia)
            .Select(t => new { t.Id, t.MonthlyPrice, t.BillingStartsOn })
            .ToListAsync();

        var ativasTotal = await _catalog.Tenants.CountAsync(t => t.Status == TenantStatus.Active);

        // Uma consulta só pra saber o que já existe, em vez de perguntar ao
        // banco por tenant dentro do laço.
        var jaCobrados = await _catalog.TenantCharges
            .AsNoTracking()
            .Where(c => c.Kind == TenantChargeKind.Mensalidade && c.ReferenceMonth == comp)
            .Select(c => c.TenantId)
            .ToListAsync();

        var jaCobradosSet = jaCobrados.ToHashSet();

        var novas = new List<TenantCharge>();
        foreach (var t in elegiveis)
        {
            if (jaCobradosSet.Contains(t.Id)) continue;

            novas.Add(new TenantCharge
            {
                TenantId       = t.Id,
                Kind           = TenantChargeKind.Mensalidade,
                // Cópia do preço vigente AGORA. Reajuste futuro não reescreve
                // esta linha — ver comentário em TenantCharge.Amount.
                Amount         = t.MonthlyPrice,
                ReferenceMonth = comp,
                DueDate        = VencimentoNaCompetencia(comp, t.BillingStartsOn!.Value.Day),
            });
        }

        if (novas.Count > 0)
        {
            _catalog.TenantCharges.AddRange(novas);
            await _catalog.SaveChangesAsync();
        }

        var resultado = new GerarMensalidadesResultDto
        {
            Competencia    = comp,
            Criadas        = novas.Count,
            JaExistiam     = elegiveis.Count - novas.Count,
            ForaDeCobranca = ativasTotal - elegiveis.Count,
            TotalGerado    = novas.Sum(c => c.Amount),
        };

        _logger.LogInformation(
            "Mensalidades da competência {Competencia:yyyy-MM}: {Criadas} criadas, {JaExistiam} já existiam, {Fora} fora de cobrança (total R$ {Total}).",
            comp, resultado.Criadas, resultado.JaExistiam, resultado.ForaDeCobranca, resultado.TotalGerado);

        return resultado;
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

        var lista = await MapearAsync(_catalog.TenantCharges.AsNoTracking().Where(c => c.Id == chargeId));
        return lista[0];
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
        var cobranca = await _catalog.TenantCharges.FirstOrDefaultAsync(c => c.Id == chargeId)
            ?? throw new InvalidOperationException("Cobrança não encontrada.");

        if (cobranca.PaidAt.HasValue)
            throw new InvalidOperationException(
                "Cobrança paga não pode ser alterada. Reabra a cobrança, altere e dê baixa de novo — assim a comissão do parceiro é refeita junto.");

        cobranca.Amount  = request.Valor;
        cobranca.DueDate = NormalizarData(request.Vencimento);
        cobranca.Notes   = string.IsNullOrWhiteSpace(request.Observacao) ? null : request.Observacao.Trim();

        await _catalog.SaveChangesAsync();

        var lista = await MapearAsync(_catalog.TenantCharges.AsNoTracking().Where(c => c.Id == chargeId));
        return lista[0];
    }

    public async Task ExcluirCobrancaAsync(Guid chargeId)
    {
        var cobranca = await _catalog.TenantCharges.FirstOrDefaultAsync(c => c.Id == chargeId)
            ?? throw new InvalidOperationException("Cobrança não encontrada.");

        if (cobranca.PaidAt.HasValue)
            throw new InvalidOperationException(
                "Cobrança paga não pode ser excluída. Reabra antes — e considere que reabrir também desfaz a comissão gerada por ela.");

        _catalog.TenantCharges.Remove(cobranca);
        await _catalog.SaveChangesAsync();
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
}
