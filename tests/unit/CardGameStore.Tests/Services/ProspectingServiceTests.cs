// =============================================================================
// ProspectingServiceTests.cs — Testa só as partes puras/sem IA do serviço de
// prospecção (score de oportunidade e faixa de faturamento heurística). A
// busca via Places API e o enriquecimento via Gemini precisam de chave real,
// não são testados aqui.
// =============================================================================

using CardGameStore.Services.Implementations;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class ProspectingServiceTests
{
    [Theory]
    [InlineData("SemSite", 5.0, 500, 100)]     // sem site + nota máxima + muitas avaliações = topo
    [InlineData("ECommerce", 5.0, 500, 60)]    // já tem e-commerce = perde os 40 pts de presença digital
    [InlineData("SemSite", 0.0, 0, 45)]        // sem site + sem nota + poucas avaliações
    [InlineData("SiteLegado", 3.0, 30, 47)]    // meio-termo
    public void CalculateOpportunityScore_CombinaOsTresFatoresCorretamente(
        string digitalPresence, double rating, int reviewCount, int esperado)
    {
        var score = ProspectingService.CalculateOpportunityScore(rating, reviewCount, digitalPresence);
        score.Should().Be(esperado);
    }

    [Fact]
    public void CalculateOpportunityScore_NuncaPassaDe100NemFicaNegativo()
    {
        var scoreMax = ProspectingService.CalculateOpportunityScore(5.0, 10_000, "SemSite");
        var scoreMin = ProspectingService.CalculateOpportunityScore(0.0, 0, "ECommerce");

        scoreMax.Should().BeLessOrEqualTo(100);
        scoreMin.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void CalculateOpportunityScore_SemNotaOuReviews_NaoLancaExcecao()
    {
        var act = () => ProspectingService.CalculateOpportunityScore(null, null, "SemSite");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0,   "R$5-15k/mês (estimativa)")]
    [InlineData(19,  "R$5-15k/mês (estimativa)")]
    [InlineData(20,  "R$15-40k/mês (estimativa)")]
    [InlineData(99,  "R$15-40k/mês (estimativa)")]
    [InlineData(100, "R$40-100k/mês (estimativa)")]
    [InlineData(299, "R$40-100k/mês (estimativa)")]
    [InlineData(300, "R$100k+/mês (estimativa)")]
    [InlineData(5000,"R$100k+/mês (estimativa)")]
    public void EstimateRevenueRangeHeuristic_UsaFaixasCorretasPorNumeroDeAvaliacoes(int reviewCount, string esperado)
    {
        ProspectingService.EstimateRevenueRangeHeuristic(reviewCount).Should().Be(esperado);
    }

    [Fact]
    public void EstimateRevenueRangeHeuristic_SemReviews_UsaFaixaMaisBaixa()
    {
        ProspectingService.EstimateRevenueRangeHeuristic(null).Should().Be("R$5-15k/mês (estimativa)");
    }
}
