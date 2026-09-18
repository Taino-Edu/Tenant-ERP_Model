// =============================================================================
// CondicoesComerciaisService.cs — O combinado com cada loja, separado da
// cobrança que ele gera.
//
// Negociar é editar isto uma vez. Daí em diante o gerador mensal cobra certo
// sozinho, e cada alteração aqui já passa pelas cobranças em aberto
// (PlatformBillingService.SincronizarCobrancasDaLojaAsync) — ninguém precisa
// lembrar de trocar o valor no mês em que o desconto acaba.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class CondicoesComerciaisService : ICondicoesComerciaisService
{
    /// <summary>Quantas competências a prévia mostra, a partir da atual.</summary>
    private const int MesesDePrevia = 12;

    private readonly CatalogDbContext _catalog;
    private readonly IPlatformBillingService _billing;

    public CondicoesComerciaisService(CatalogDbContext catalog, IPlatformBillingService billing)
    {
        _catalog = catalog;
        _billing = billing;
    }

    public async Task<CondicoesComerciaisDto> ObterAsync(Guid tenantId)
    {
        var tenant = await _catalog.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Loja não encontrada.");

        var descontos = await _catalog.TenantBillingDiscounts.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .ToListAsync();

        var cobrancas = await _catalog.TenantCharges.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .ToListAsync();

        return Montar(tenant, descontos, cobrancas, DateTime.UtcNow);
    }

    public async Task<AlteracaoCondicoesResultDto> AtualizarAsync(Guid tenantId, AtualizarCondicoesRequest request)
    {
        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Loja não encontrada.");

        var hoje = SoODia(DateTime.UtcNow);
        var inicio = request.InicioCobranca is { } i ? SoODia(i) : (DateTime?)null;
        var primeiroVencimento = request.ImplantacaoPrimeiroVencimento is { } p ? SoODia(p) : (DateTime?)null;

        var implantacoes = await _catalog.TenantCharges.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Kind == TenantChargeKind.Implantacao)
            .ToListAsync();

        var lancadaManualmente = implantacoes.Any(c => c.InstallmentNumber is null);

        if (request.Mensalidade > 0 && inicio is null)
            throw new InvalidOperationException("Defina quando a cobrança da mensalidade começa.");

        var mudouImplantacao = request.ImplantacaoValor != tenant.SetupFee
                            || request.ImplantacaoParcelas != tenant.SetupInstallments
                            || primeiroVencimento != tenant.SetupFirstDueDate;

        if (lancadaManualmente)
        {
            // Parcelas geradas por cima de uma implantação lançada à mão cobrariam
            // a mesma implantação duas vezes.
            if (request.ImplantacaoValor > 0 && mudouImplantacao)
                throw new InvalidOperationException(
                    "Esta loja já tem uma implantação lançada à mão. Para cobrá-la pelas condições (com parcelamento), exclua antes essa cobrança.");
        }
        else
        {
            if (request.ImplantacaoValor > 0 && primeiroVencimento is null)
                throw new InvalidOperationException("Defina o vencimento da primeira parcela da implantação.");

            // Só quando a data muda: condição antiga cuja primeira parcela já
            // venceu continua podendo ser salva sem mexer nela.
            if (primeiroVencimento is not null && primeiroVencimento != tenant.SetupFirstDueDate && primeiroVencimento < hoje)
                throw new InvalidOperationException("O vencimento da primeira parcela da implantação não pode ficar no passado.");

            var pagas = implantacoes.Where(c => c.PaidAt != null).ToList();
            var valorPago = pagas.Sum(c => c.Amount);

            if (request.ImplantacaoValor < valorPago)
                throw new InvalidOperationException(
                    $"Já foram pagos R$ {CondicoesComerciais.Numero(valorPago, "0.00")} desta implantação. O valor combinado não pode ficar abaixo disso.");

            var maiorParcelaPaga = pagas.Select(c => c.InstallmentNumber ?? 0).DefaultIfEmpty(0).Max();
            if (request.ImplantacaoParcelas < maiorParcelaPaga)
                throw new InvalidOperationException(
                    $"A parcela {maiorParcelaPaga} da implantação já foi paga. O parcelamento não pode ter menos parcelas que isso.");
        }

        tenant.MonthlyPrice      = request.Mensalidade;
        tenant.BillingStartsOn   = inicio;
        tenant.BillingDueDay     = request.DiaVencimento;
        tenant.SetupFee          = request.ImplantacaoValor;
        tenant.SetupInstallments = request.ImplantacaoParcelas;
        tenant.SetupFirstDueDate = primeiroVencimento;

        await _catalog.SaveChangesAsync();

        return await AplicarAsync(tenantId);
    }

    public async Task<AlteracaoCondicoesResultDto> CriarDescontoAsync(Guid tenantId, SalvarDescontoRequest request, string? autor)
    {
        if (!await _catalog.Tenants.AnyAsync(t => t.Id == tenantId))
            throw new InvalidOperationException("Loja não encontrada.");

        var desconto = new TenantBillingDiscount
        {
            TenantId  = tenantId,
            CreatedBy = string.IsNullOrWhiteSpace(autor) ? null : autor.Trim(),
        };
        PreencherDesconto(desconto, request);

        _catalog.TenantBillingDiscounts.Add(desconto);
        await _catalog.SaveChangesAsync();

        return await AplicarAsync(tenantId);
    }

    public async Task<AlteracaoCondicoesResultDto> AtualizarDescontoAsync(Guid tenantId, Guid descontoId, SalvarDescontoRequest request)
    {
        var desconto = await _catalog.TenantBillingDiscounts
            .FirstOrDefaultAsync(d => d.Id == descontoId && d.TenantId == tenantId)
            ?? throw new InvalidOperationException("Desconto não encontrado.");

        PreencherDesconto(desconto, request);
        await _catalog.SaveChangesAsync();

        return await AplicarAsync(tenantId);
    }

    public async Task<AlteracaoCondicoesResultDto> ExcluirDescontoAsync(Guid tenantId, Guid descontoId)
    {
        var desconto = await _catalog.TenantBillingDiscounts
            .FirstOrDefaultAsync(d => d.Id == descontoId && d.TenantId == tenantId)
            ?? throw new InvalidOperationException("Desconto não encontrado.");

        // Excluir não reescreve o passado: cobrança paga guarda a descrição do
        // desconto que entrou nela (TenantCharge.DiscountSummary).
        _catalog.TenantBillingDiscounts.Remove(desconto);
        await _catalog.SaveChangesAsync();

        return await AplicarAsync(tenantId);
    }

    private async Task<AlteracaoCondicoesResultDto> AplicarAsync(Guid tenantId)
    {
        // Sem o token da requisição, pelo mesmo motivo do "Emitir no Asaas":
        // fechar a aba entre o cancelamento no gateway e o SaveChanges deixaria a
        // cobrança apontando pra uma fatura que não existe mais.
        var cobrancas = await _billing.SincronizarCobrancasDaLojaAsync(tenantId, CancellationToken.None);

        return new AlteracaoCondicoesResultDto
        {
            Condicoes = await ObterAsync(tenantId),
            Cobrancas = cobrancas,
        };
    }

    private static void PreencherDesconto(TenantBillingDiscount desconto, SalvarDescontoRequest request)
    {
        if (!Enum.TryParse<TenantDiscountKind>(request.Tipo, ignoreCase: true, out var tipo))
            throw new InvalidOperationException("Tipo de desconto inválido: use Percentual ou ValorFixo.");

        var descricao = request.Descricao?.Trim();
        if (string.IsNullOrEmpty(descricao))
            throw new InvalidOperationException("Dê um nome ao desconto — é o que aparece na fatura.");

        if (request.Valor <= 0)
            throw new InvalidOperationException("O desconto precisa ser maior que zero.");

        if (tipo == TenantDiscountKind.Percentual && request.Valor > 100)
            throw new InvalidOperationException("Desconto percentual vai até 100%.");

        var inicial = CondicoesComerciais.Competencia(request.CompetenciaInicial);
        var final   = request.CompetenciaFinal is { } f ? CondicoesComerciais.Competencia(f) : (DateTime?)null;

        if (final < inicial)
            throw new InvalidOperationException("O mês final do desconto não pode ser anterior ao inicial.");

        desconto.Description = descricao;
        desconto.Kind        = tipo;
        desconto.Value       = request.Valor;
        desconto.StartMonth  = inicial;
        desconto.EndMonth    = final;
    }

    private static DateTime SoODia(DateTime data)
    {
        var utc = data.Kind == DateTimeKind.Local ? data.ToUniversalTime() : data;
        return new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    // ── Montagem da resposta e da prévia ─────────────────────────────────────

    internal static CondicoesComerciaisDto Montar(
        Tenant tenant, List<TenantBillingDiscount> descontos, List<TenantCharge> cobrancas, DateTime agora)
    {
        var competenciaAtual = CondicoesComerciais.Competencia(agora);
        var implantacoes = cobrancas.Where(c => c.Kind == TenantChargeKind.Implantacao).ToList();
        var parcelasPagas = implantacoes.Where(c => c.PaidAt != null).ToList();

        var dto = new CondicoesComerciaisDto
        {
            TenantId       = tenant.Id,
            Mensalidade    = tenant.MonthlyPrice,
            InicioCobranca = tenant.BillingStartsOn,
            DiaVencimento  = tenant.BillingDueDay,
            Implantacao    = new ImplantacaoCondicoesDto
            {
                Valor              = tenant.SetupFee,
                Parcelas           = tenant.SetupInstallments,
                PrimeiroVencimento = tenant.SetupFirstDueDate,
                ValorPago          = parcelasPagas.Sum(c => c.Amount),
                ParcelasPagas      = parcelasPagas.Count,
                LancadaManualmente = implantacoes.Any(c => c.InstallmentNumber is null),
            },
            Descontos = descontos
                .OrderBy(d => d.StartMonth).ThenBy(d => d.CreatedAt)
                .Select(d => new DescontoDto
                {
                    Id                 = d.Id,
                    Descricao          = d.Description,
                    Tipo               = d.Kind.ToString(),
                    Valor              = d.Value,
                    CompetenciaInicial = d.StartMonth,
                    CompetenciaFinal   = d.EndMonth,
                    Situacao           = d.EndMonth < competenciaAtual ? "Encerrado"
                                       : d.StartMonth > competenciaAtual ? "Futuro"
                                       : "Vigente",
                    CriadoPor          = d.CreatedBy,
                    CriadoEm           = d.CreatedAt,
                })
                .ToList(),
        };

        var parcelas = PlatformBillingService.ParcelasEsperadas(tenant, cobrancas);

        for (var i = 0; i < MesesDePrevia; i++)
        {
            var competencia = competenciaAtual.AddMonths(i);
            var itens = new List<PreviaItemDto>();

            // O que já existe aparece como está — a prévia não pode prometer um
            // valor diferente do que o lojista já recebeu na fatura.
            var mensalidade = cobrancas.FirstOrDefault(c => c.Kind == TenantChargeKind.Mensalidade && c.ReferenceMonth == competencia);
            if (mensalidade is not null)
            {
                itens.Add(DaCobranca(mensalidade, "Mensalidade"));
            }
            else if (tenant.MonthlyPrice > 0
                     && CondicoesComerciais.VencimentoDaMensalidade(tenant, competencia) is { } vencimento)
            {
                var calculo = CondicoesComerciais.CalcularMensalidade(tenant.MonthlyPrice, descontos, competencia);
                itens.Add(new PreviaItemDto
                {
                    Tipo              = TenantChargeKind.Mensalidade.ToString(),
                    Descricao         = "Mensalidade",
                    ValorBruto        = calculo.Base,
                    Desconto          = calculo.Desconto,
                    DescricaoDesconto = calculo.DescricaoDesconto,
                    Valor             = calculo.Valor,
                    Vencimento        = vencimento,
                    Situacao          = calculo.Valor > 0 ? "Prevista" : "SemCobranca",
                });
            }

            foreach (var implantacao in implantacoes.Where(c => c.ReferenceMonth == competencia))
                itens.Add(DaCobranca(implantacao, DescricaoImplantacao(implantacao.InstallmentNumber, implantacao.InstallmentCount)));

            foreach (var parcela in parcelas.Where(p => p.Competencia == competencia && p.Valor > 0))
            {
                if (implantacoes.Any(c => c.InstallmentNumber == parcela.Numero || c.ReferenceMonth == competencia)) continue;

                itens.Add(new PreviaItemDto
                {
                    Tipo       = TenantChargeKind.Implantacao.ToString(),
                    Descricao  = DescricaoImplantacao(parcela.Numero, parcela.Total),
                    Valor      = parcela.Valor,
                    Vencimento = parcela.Vencimento,
                    Situacao   = "Prevista",
                });
            }

            dto.Previa.Add(new PreviaCompetenciaDto
            {
                Competencia = competencia,
                Itens       = itens,
                Total       = itens.Where(x => x.Situacao != "SemCobranca").Sum(x => x.Valor),
            });
        }

        return dto;
    }

    private static PreviaItemDto DaCobranca(TenantCharge cobranca, string descricao) => new()
    {
        Tipo              = cobranca.Kind.ToString(),
        Descricao         = descricao,
        ValorBruto        = cobranca.GrossAmount,
        Desconto          = cobranca.DiscountAmount,
        DescricaoDesconto = cobranca.DiscountSummary,
        Valor             = cobranca.Amount,
        Vencimento        = cobranca.DueDate,
        Situacao          = cobranca.PaidAt != null ? "Paga"
                          : cobranca.ExternalChargeId != null ? "Emitida"
                          : "EmAberto",
        Manual            = !cobranca.AutoGenerated,
    };

    private static string DescricaoImplantacao(int? numero, int? total) =>
        numero is not null && total is > 1 ? $"Implantação {numero}/{total}" : "Implantação";
}
