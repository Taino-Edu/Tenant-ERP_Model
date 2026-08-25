using CardGameStore.DTOs;
using CardGameStore.Services.Implementations;
using FluentAssertions;

namespace CardGameStore.Tests.Services;

public sealed class IntegrationFinancialAnalyzerTests
{
    [Fact]
    public void Analyze_CalculatesFinancialIndicatorsAndProductPricing()
    {
        var request = new IntegrationFinancialAnalysisRequest
        {
            Receita = 10_000m,
            CustoProdutos = 6_000m,
            DespesasVariaveis = 1_000m,
            DespesasFixas = 2_000m,
            RecebiveisEmAberto = 2_000m,
            RecebiveisVencidos = 500m,
            MargemAlvoPercent = 35m,
            Produtos =
            [
                new IntegrationFinancialProductRequest
                {
                    Nome = "Produto A",
                    QuantidadeVendida = 10,
                    Receita = 800m,
                    Custo = 500m,
                },
            ],
        };

        var result = IntegrationFinancialAnalyzer.Analyze(request);

        result.FormulaVersion.Should().Be(IntegrationFinancialAnalyzer.FormulaVersion);
        result.Summary.LucroBruto.Should().Be(4_000m);
        result.Summary.MargemBrutaPercent.Should().Be(40m);
        result.Summary.MarkupPercent.Should().Be(66.67m);
        result.Summary.MargemContribuicao.Should().Be(3_000m);
        result.Summary.MargemContribuicaoPercent.Should().Be(30m);
        result.Summary.ResultadoOperacional.Should().Be(1_000m);
        result.Summary.PontoEquilibrioReceita.Should().Be(6_666.67m);
        result.Summary.MargemSeguranca.Should().Be(3_333.33m);
        result.Summary.InadimplenciaPercent.Should().Be(25m);

        result.Produtos.Should().ContainSingle();
        result.Produtos[0].PrecoMedio.Should().Be(80m);
        result.Produtos[0].CustoMedio.Should().Be(50m);
        result.Produtos[0].MargemPercent.Should().Be(37.5m);
        result.Produtos[0].MarkupPercent.Should().Be(60m);
        result.Produtos[0].PrecoAlvo.Should().Be(76.92m);
        result.Produtos[0].AjustePrecoPercent.Should().Be(-3.85m);

        result.Insights.Select(item => item.Code).Should().Contain([
            "OPERATING_PROFIT", "HIGH_DELINQUENCY", "BREAK_EVEN",
        ]);
    }

    [Fact]
    public void Analyze_WhenOperationIsBelowTarget_ReturnsActionableWarnings()
    {
        var result = IntegrationFinancialAnalyzer.Analyze(new IntegrationFinancialAnalysisRequest
        {
            Receita = 1_000m,
            CustoProdutos = 800m,
            DespesasVariaveis = 100m,
            DespesasFixas = 300m,
            MargemAlvoPercent = 35m,
            Produtos =
            [
                new IntegrationFinancialProductRequest
                {
                    Nome = "Item de margem baixa",
                    QuantidadeVendida = 5,
                    Receita = 500m,
                    Custo = 450m,
                },
            ],
        });

        result.Summary.ResultadoOperacional.Should().Be(-200m);
        result.Insights.Select(item => item.Code).Should().Contain([
            "OPERATING_LOSS", "MARGIN_BELOW_TARGET", "LOW_MARGIN_PRODUCTS", "REVENUE_CONCENTRATION",
        ]);
    }

    [Fact]
    public void Analyze_WithNoRevenue_AvoidsDivisionByZero()
    {
        var result = IntegrationFinancialAnalyzer.Analyze(new IntegrationFinancialAnalysisRequest());

        result.Summary.MargemBrutaPercent.Should().Be(0);
        result.Summary.MarkupPercent.Should().Be(0);
        result.Summary.PontoEquilibrioReceita.Should().BeNull();
        result.Insights.Should().ContainSingle(item => item.Code == "NO_REVENUE");
    }
}
