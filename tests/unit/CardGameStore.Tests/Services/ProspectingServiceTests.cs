// =============================================================================
// ProspectingServiceTests.cs — Testa só as partes puras/sem IA do serviço de
// prospecção (score de oportunidade e faixa de faturamento heurística). A
// busca via Overpass API/Nominatim e o enriquecimento via Gemini precisam de
// rede, não são testados aqui.
// =============================================================================

using CardGameStore.Services.Implementations;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class ProspectingServiceTests
{
    [Theory]
    [InlineData("SemSite", true, true, true, 100)]        // sem site + cadastro completo = topo
    [InlineData("ECommerce", true, true, true, 60)]       // já tem e-commerce = perde os 40 pts de presença digital
    [InlineData("SemSite", false, false, false, 40)]       // sem site + cadastro vazio
    [InlineData("SiteLegado", true, false, true, 60)]      // meio-termo
    public void CalculateOpportunityScore_CombinaPresencaDigitalECompletudeCorretamente(
        string digitalPresence, bool temTelefone, bool temHorario, bool temEndereco, int esperado)
    {
        var score = ProspectingService.CalculateOpportunityScore(temTelefone, temHorario, temEndereco, digitalPresence);
        score.Should().Be(esperado);
    }

    [Fact]
    public void CalculateOpportunityScore_DesconhecidoSemNadaPreenchido_ZeraAmbosOsFatores()
    {
        // Presença digital não reconhecida (ex: "Desconhecido") não pontua nada,
        // igual a "ECommerce" — só "SemSite"/"SiteLegado" pontuam.
        ProspectingService.CalculateOpportunityScore(false, false, false, "Desconhecido").Should().Be(0);
    }

    [Theory]
    [InlineData(false, false, false, "R$5-15k/mês (estimativa)")]
    [InlineData(true,  false, false, "R$15-40k/mês (estimativa)")]
    [InlineData(true,  true,  false, "R$40-100k/mês (estimativa)")]
    [InlineData(true,  true,  true,  "R$100k+/mês (estimativa)")]
    public void EstimateRevenueRangeHeuristic_UsaFaixasCorretasPorCompletudeDoCadastro(
        bool temTelefone, bool temHorario, bool temEndereco, string esperado)
    {
        ProspectingService.EstimateRevenueRangeHeuristic(temTelefone, temHorario, temEndereco).Should().Be(esperado);
    }

    private static readonly (double Sul, double Oeste, double Norte, double Leste) BboxDummy = (-21.2, -47.9, -21.1, -47.7);

    [Theory]
    [InlineData("roupas")]           // chave exata
    [InlineData("Roupas")]           // case-insensitive
    [InlineData("loja de roupas")]   // frase natural — palavra "roupas" bate dentro da frase
    [InlineData("  roupas  ")]       // espaços nas pontas
    public void BuildOverpassQuery_CategoriaComPalavraConhecida_UsaTagOsmExata(string categoria)
    {
        var query = ProspectingService.BuildOverpassQuery(categoria, BboxDummy);
        query.Should().Contain("[\"shop\"=\"clothes\"]");
    }

    [Fact]
    public void BuildOverpassQuery_CategoriaSemPalavraConhecida_CaiNoFallbackPorNome()
    {
        var query = ProspectingService.BuildOverpassQuery("brechó vintage raro", BboxDummy);
        query.Should().Contain("[\"name\"~\"brechó vintage raro\",i]");
    }

    [Fact]
    public void BuildOverpassQuery_MontaBboxNaOrdemQueOOverpassEspera_SulOesteNorteLeste()
    {
        // Overpass QL exige bbox como (sul,oeste,norte,leste). Inverter
        // norte/oeste faz TODA busca falhar com "n must be >= s" em produção
        // (bug real já visto: overpass-api.de retornando 400 pra qualquer
        // cidade/categoria) — este teste trava a ordem certa pra nunca mais
        // regredir silenciosamente.
        var query = ProspectingService.BuildOverpassQuery("roupas", BboxDummy);
        query.Should().Contain("(-21.2,-47.9,-21.1,-47.7)");
    }
}
