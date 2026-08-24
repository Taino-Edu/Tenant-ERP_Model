// =============================================================================
// ProspectingServiceTests.cs — Testa só as partes puras/sem IA do serviço de
// prospecção (score de oportunidade e faixa de faturamento heurística). A
// busca via Overpass API/Nominatim e o enriquecimento via Gemini precisam de
// rede, não são testados aqui.
// =============================================================================

using System.Net;
using System.Text;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public void BuildOverpassQuery_IncluiNodeWayRelationESemLimiteSilencioso()
    {
        var query = ProspectingService.BuildOverpassQuery("restaurante", BboxDummy);
        query.Should().Contain("nwr[\"amenity\"=\"restaurant\"]");
        query.Should().Contain("out tags center;");
        query.Should().NotContain("out center 60");
    }

    [Fact]
    public void BuildOverpassQuery_QuandoTemAreaAdministrativa_NaoUsaBbox()
    {
        var query = ProspectingService.BuildOverpassQuery("roupas", BboxDummy, 3_600_123_456);
        query.Should().Contain("area(3600123456)->.searchArea;");
        query.Should().Contain("(area.searchArea)");
        query.Should().NotContain("(-21.2,-47.9,-21.1,-47.7)");
    }

    [Fact]
    public void BuildOverpassQuery_TodosOsNegocios_ConsultaCategoriasAmplias()
    {
        var query = ProspectingService.BuildOverpassQuery("Todos os negócios", BboxDummy);
        query.Should().Contain("shop|amenity|office|craft|tourism|leisure");
        query.Should().Contain("[\"name\"]");
    }

    [Fact]
    public async Task EnrichLeadWithAiAsync_PersisteAbordagemSemInventarFaixaFinanceira()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new CatalogDbContext(options);
        var lead = new Lead
        {
            Nome = "Loja Exemplo", Telefone = "14999990000", Origem = "landing",
            Mensagem = "Preciso organizar meu estoque.", EstimatedRevenueRange = null,
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProspectingSettings:GeminiApiKey"] = "test-key",
        }).Build();
        var service = new ProspectingService(
            new FakeHttpClientFactory(new GeminiSuccessHandler()), config,
            NullLogger<ProspectingService>.Instance, db);

        var result = await service.EnrichLeadWithAiAsync(lead.Id);

        result.Should().NotBeNull();
        result!.AbordagemSugerida.Should().Be("Apresente o controle de estoque integrado ao PDV.");
        result.EstimatedRevenueRange.Should().BeEmpty();
        lead.AbordagemSugerida.Should().Be(result.AbordagemSugerida);
        lead.EstimatedRevenueRange.Should().BeNull();
    }

    [Fact]
    public async Task EnrichLeadWithAiAsync_UsaChaveGlobalQuandoDedicadaNaoFoiConfigurada()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new CatalogDbContext(options);
        var lead = new Lead
        {
            Nome = "Loja Exemplo", Telefone = "14999990000", Origem = "prospeccao",
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GeminiSettings:ApiKey"] = "global-key",
        }).Build();
        var service = new ProspectingService(
            new FakeHttpClientFactory(new GeminiSuccessHandler()), config,
            NullLogger<ProspectingService>.Instance, db);

        var result = await service.EnrichLeadWithAiAsync(lead.Id);

        result.Should().NotBeNull();
        result!.AbordagemSugerida.Should().Contain("estoque integrado");
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class GeminiSuccessHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const string json = """
                {"candidates":[{"content":{"parts":[{"text":"{\"abordagemSugerida\":\"Apresente o controle de estoque integrado ao PDV.\"}"}]}}]}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
