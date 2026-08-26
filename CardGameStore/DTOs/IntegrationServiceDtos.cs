using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public sealed class IntegrationFinancialAnalysisRequest
{
    public DateTime? Inicio { get; init; }
    public DateTime? Fim { get; init; }
    [Range(0, 999999999999.99)] public decimal Receita { get; init; }
    [Range(0, 999999999999.99)] public decimal CustoProdutos { get; init; }
    [Range(0, 999999999999.99)] public decimal DespesasVariaveis { get; init; }
    [Range(0, 999999999999.99)] public decimal DespesasFixas { get; init; }
    [Range(0, 999999999999.99)] public decimal RecebiveisEmAberto { get; init; }
    [Range(0, 999999999999.99)] public decimal RecebiveisVencidos { get; init; }
    [Range(1, 95)] public decimal MargemAlvoPercent { get; init; } = 35;
    public IReadOnlyList<IntegrationFinancialProductRequest> Produtos { get; init; } = [];
}

public sealed class IntegrationFinancialProductRequest
{
    [Required, MaxLength(200)] public string Nome { get; init; } = string.Empty;
    [Range(0, 999999999)] public int QuantidadeVendida { get; init; }
    [Range(0, 999999999999.99)] public decimal Receita { get; init; }
    [Range(0, 999999999999.99)] public decimal Custo { get; init; }
}

public sealed record IntegrationFinancialAnalysisResponse(
    DateTime GeneratedAt,
    string FormulaVersion,
    IntegrationFinancialSummary Summary,
    IReadOnlyList<IntegrationFinancialProductAnalysis> Produtos,
    IReadOnlyList<IntegrationFinancialInsight> Insights);

public sealed record IntegrationFinancialSummary(
    decimal LucroBruto,
    decimal MargemBrutaPercent,
    decimal MarkupPercent,
    decimal MargemContribuicao,
    decimal MargemContribuicaoPercent,
    decimal ResultadoOperacional,
    decimal? PontoEquilibrioReceita,
    decimal? MargemSeguranca,
    decimal InadimplenciaPercent);

public sealed record IntegrationFinancialProductAnalysis(
    string Nome,
    int QuantidadeVendida,
    decimal PrecoMedio,
    decimal CustoMedio,
    decimal MargemPercent,
    decimal MarkupPercent,
    decimal? PrecoAlvo,
    decimal? AjustePrecoPercent);

public sealed record IntegrationFinancialInsight(
    string Code,
    string Severity,
    string Title,
    string Message,
    string Action);

public sealed record IntegrationIbptResponse(
    string Ncm,
    string Uf,
    bool Importado,
    decimal PercentualFederal,
    decimal PercentualEstadual,
    decimal PercentualMunicipal,
    decimal PercentualTotal,
    string? Fonte,
    string? Versao,
    DateTime? VigenciaInicio,
    DateTime? VigenciaFim,
    bool Vencida,
    DateTime AtualizadoEm);

/// <summary>
/// Snapshot imutavel de uma venda criada em um ERP externo. Valores monetarios
/// usam centavos para manter o contrato deterministico entre plataformas.
/// </summary>
public sealed class IntegrationFiscalEmissionRequest
{
    [Required, MaxLength(50)] public string Source { get; init; } = string.Empty;
    [Required, MaxLength(100)] public string ExternalDocumentId { get; init; } = string.Empty;
    [Required, MaxLength(100)] public string IdempotencyKey { get; init; } = string.Empty;
    [Required, MinLength(1), MaxLength(200)] public IReadOnlyList<IntegrationFiscalItemRequest> Items { get; init; } = [];
    [Required, MaxLength(40)] public string PaymentMethod { get; init; } = string.Empty;
    [MaxLength(40)] public string? SecondPaymentMethod { get; init; }
    [Range(0, int.MaxValue)] public int SecondPaymentAmountInCents { get; init; }
    [Range(0, int.MaxValue)] public int DiscountInCents { get; init; }
    [Range(0, int.MaxValue)] public int? CashReceivedInCents { get; init; }
    [Range(0, int.MaxValue)] public int ChangeInCents { get; init; }
    [MaxLength(14)] public string? CustomerCpf { get; init; }
}

public sealed class IntegrationFiscalItemRequest
{
    [Required, MaxLength(120)] public string Name { get; init; } = string.Empty;
    [Required, RegularExpression("^[0-9]{8}$")] public string Ncm { get; init; } = string.Empty;
    [Required, RegularExpression("^[0-9]{4}$")] public string Cfop { get; init; } = "5102";
    [MaxLength(3)] public string? Csosn { get; init; } = "102";
    [MaxLength(3)] public string? Cst { get; init; }
    [Range(1, int.MaxValue)] public int Quantity { get; init; }
    [Range(0, int.MaxValue)] public int UnitPriceInCents { get; init; }
    [Range(0, int.MaxValue)] public int SubtotalInCents { get; init; }
    [Range(0, 8)] public int Origin { get; init; }
    [MaxLength(7)] public string? Cest { get; init; }
    [MaxLength(14)] public string? Gtin { get; init; }
    public decimal? FederalTaxPercent { get; init; }
    public decimal? StateTaxPercent { get; init; }
    public decimal? MunicipalTaxPercent { get; init; }
    [MaxLength(120)] public string? TaxSource { get; init; }
    public DateTime? TaxValidUntil { get; init; }
    [MaxLength(3)] public string IbsCbsCst { get; init; } = "000";
    [MaxLength(6)] public string IbsCbsClassTrib { get; init; } = "000001";
}

public sealed record IntegrationFiscalNoteResponse(
    Guid Id,
    string Source,
    string ExternalDocumentId,
    string Status,
    int TotalInCents,
    int? Series,
    int? Number,
    string? AccessKey,
    string? Protocol,
    string? RejectionReason,
    DateTime? IssuedAt,
    DateTime? AuthorizedAt,
    DateTime? CancelledAt,
    DateTime CreatedAt);

public sealed class IntegrationFiscalCancelRequest
{
    [Required, MinLength(15), MaxLength(255)]
    public string Justification { get; init; } = string.Empty;
}
