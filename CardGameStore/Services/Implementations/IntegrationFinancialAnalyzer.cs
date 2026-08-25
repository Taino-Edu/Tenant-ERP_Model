using CardGameStore.DTOs;

namespace CardGameStore.Services.Implementations;

public static class IntegrationFinancialAnalyzer
{
    public const string FormulaVersion = "2026.08.1";

    public static IntegrationFinancialAnalysisResponse Analyze(IntegrationFinancialAnalysisRequest request)
    {
        var receita = request.Receita;
        var custo = request.CustoProdutos;
        var lucroBruto = receita - custo;
        var margemBrutaPercent = Percent(lucroBruto, receita);
        var markupPercent = Percent(lucroBruto, custo);
        var margemContribuicao = receita - custo - request.DespesasVariaveis;
        var margemContribuicaoPercent = Percent(margemContribuicao, receita);
        var resultadoOperacional = margemContribuicao - request.DespesasFixas;
        var indiceContribuicao = receita > 0 ? margemContribuicao / receita : 0;
        decimal? pontoEquilibrio = indiceContribuicao > 0
            ? Round(request.DespesasFixas / indiceContribuicao)
            : null;
        decimal? margemSeguranca = pontoEquilibrio.HasValue
            ? Round(receita - pontoEquilibrio.Value)
            : null;
        var inadimplencia = Percent(request.RecebiveisVencidos, request.RecebiveisEmAberto);

        var produtos = request.Produtos
            .Take(100)
            .Select(item => AnalyzeProduct(item, request.MargemAlvoPercent))
            .OrderBy(item => item.MargemPercent)
            .ThenByDescending(item => item.QuantidadeVendida)
            .ToArray();

        var insights = BuildInsights(request, resultadoOperacional, margemBrutaPercent,
            inadimplencia, pontoEquilibrio, produtos);

        return new IntegrationFinancialAnalysisResponse(
            DateTime.UtcNow,
            FormulaVersion,
            new IntegrationFinancialSummary(
                Round(lucroBruto), Round(margemBrutaPercent), Round(markupPercent),
                Round(margemContribuicao), Round(margemContribuicaoPercent),
                Round(resultadoOperacional), pontoEquilibrio, margemSeguranca,
                Round(inadimplencia)),
            produtos,
            insights);
    }

    private static IntegrationFinancialProductAnalysis AnalyzeProduct(
        IntegrationFinancialProductRequest item, decimal targetMargin)
    {
        var price = item.QuantidadeVendida > 0 ? item.Receita / item.QuantidadeVendida : 0;
        var cost = item.QuantidadeVendida > 0 ? item.Custo / item.QuantidadeVendida : 0;
        var margin = Percent(item.Receita - item.Custo, item.Receita);
        var markup = Percent(item.Receita - item.Custo, item.Custo);
        decimal? targetPrice = cost > 0
            ? Round(cost / (1 - targetMargin / 100))
            : null;
        decimal? adjustment = targetPrice.HasValue && price > 0
            ? Round((targetPrice.Value / price - 1) * 100)
            : null;

        return new IntegrationFinancialProductAnalysis(
            item.Nome.Trim(), item.QuantidadeVendida, Round(price), Round(cost),
            Round(margin), Round(markup), targetPrice, adjustment);
    }

    private static IReadOnlyList<IntegrationFinancialInsight> BuildInsights(
        IntegrationFinancialAnalysisRequest request,
        decimal operatingResult,
        decimal grossMarginPercent,
        decimal delinquencyPercent,
        decimal? breakEven,
        IReadOnlyList<IntegrationFinancialProductAnalysis> products)
    {
        var result = new List<IntegrationFinancialInsight>();

        if (request.Receita <= 0)
            result.Add(new("NO_REVENUE", "warning", "Período sem receita",
                "Não há faturamento suficiente para calcular tendências confiáveis.",
                "Amplie o período ou confira se todas as vendas foram registradas."));
        else if (operatingResult < 0)
            result.Add(new("OPERATING_LOSS", "critical", "Operação abaixo do ponto de equilíbrio",
                $"O resultado operacional estimado está negativo em R$ {Math.Abs(operatingResult):N2}.",
                "Revise despesas fixas e preços dos produtos com menor margem."));
        else
            result.Add(new("OPERATING_PROFIT", "success", "Operação com resultado positivo",
                $"O resultado operacional estimado é R$ {operatingResult:N2}.",
                "Preserve a margem e acompanhe o caixa realizado."));

        if (grossMarginPercent < request.MargemAlvoPercent && request.Receita > 0)
            result.Add(new("MARGIN_BELOW_TARGET", "warning", "Margem abaixo da meta",
                $"A margem bruta está em {grossMarginPercent:N1}% para uma meta de {request.MargemAlvoPercent:N1}%.",
                "Priorize reajuste, negociação de custo ou mudança do mix."));

        if (delinquencyPercent >= 10)
            result.Add(new("HIGH_DELINQUENCY", delinquencyPercent >= 25 ? "critical" : "warning",
                "Recebíveis vencidos exigem atenção",
                $"{delinquencyPercent:N1}% da carteira em aberto está vencida.",
                "Organize uma régua de cobrança e reduza novos limites de alto risco."));

        if (breakEven.HasValue && request.Receita > 0)
            result.Add(new("BREAK_EVEN", "info", "Ponto de equilíbrio calculado",
                $"A receita mínima estimada para cobrir custos e despesas é R$ {breakEven.Value:N2}.",
                "Use esse valor como piso da meta mensal."));

        var lowMargin = products.Where(item => item.CustoMedio > 0 && item.MargemPercent < request.MargemAlvoPercent)
            .Take(5).ToArray();
        if (lowMargin.Length > 0)
            result.Add(new("LOW_MARGIN_PRODUCTS", "warning", "Produtos com margem comprimida",
                $"{lowMargin.Length} produto(s) vendido(s) estão abaixo da margem alvo, incluindo {string.Join(", ", lowMargin.Take(3).Select(item => item.Nome))}.",
                "Revise primeiro os itens com maior volume e ajuste gradual de preço."));

        var topRevenue = request.Produtos.OrderByDescending(item => item.Receita).FirstOrDefault();
        if (topRevenue is not null && request.Receita > 0 && topRevenue.Receita / request.Receita >= 0.4m)
            result.Add(new("REVENUE_CONCENTRATION", "info", "Receita concentrada em um produto",
                $"{topRevenue.Nome} representa {topRevenue.Receita / request.Receita * 100:N1}% da receita informada.",
                "Proteja o estoque desse item e desenvolva alternativas de venda."));

        return result;
    }

    private static decimal Percent(decimal numerator, decimal denominator) =>
        denominator == 0 ? 0 : numerator / denominator * 100;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
