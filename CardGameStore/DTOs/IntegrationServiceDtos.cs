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
