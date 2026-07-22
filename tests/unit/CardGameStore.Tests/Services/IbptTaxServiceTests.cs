using System.Net;
using System.Text;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CardGameStore.Tests.Services;

public class IbptTaxServiceTests
{
    private static EncryptionService CreateEncryption()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        return new EncryptionService(new ConfigurationBuilder().Build(), env.Object);
    }

    private static IbptTaxService CreateService(
        AppDbContext db, EncryptionService encryption, HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://apidoni.ibpt.org.br/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("ibpt")).Returns(client);
        return new IbptTaxService(db, factory.Object, encryption, NullLogger<IbptTaxService>.Instance);
    }

    private static async Task<(FiscalConfig Config, Product Product)> SeedAsync(
        AppDbContext db, EncryptionService encryption, int origem = 0)
    {
        var cfg = new FiscalConfig
        {
            Cnpj = "12345678000199", Uf = "SP", IbptAutoSyncEnabled = true,
            IbptTokenEncrypted = encryption.Encrypt("token-secreto"),
        };
        var natureza = new NaturezaOperacao
        {
            Descricao = "Venda", Cfop = "5102", Csosn = "102", OrigemMercadoria = origem, IsPadrao = true,
        };
        var produto = new Product
        {
            Name = "Produto teste", Category = "Teste", PriceInCents = 1000,
            StockQuantity = 1, Ncm = "95044000", NaturezaOperacao = natureza,
        };
        db.AddRange(cfg, natureza, produto);
        await db.SaveChangesAsync();
        return (cfg, produto);
    }

    [Theory]
    [InlineData(0, 12.34)]
    [InlineData(1, 21.45)]
    public async Task TentarSincronizarProduto_SelecionaFederalPelaOrigemEPersisteVigencia(
        int origem, decimal federalEsperado)
    {
        await using var db = TestDbFactory.Create($"{nameof(TentarSincronizarProduto_SelecionaFederalPelaOrigemEPersisteVigencia)}_{origem}");
        var encryption = CreateEncryption();
        var (_, produto) = await SeedAsync(db, encryption, origem);
        Uri? chamada = null;
        var handler = new FakeHandler(req =>
        {
            chamada = req.RequestUri;
            return Json("""
                {"Codigo":"95044000","UF":"SP","EX":0,"Descricao":"Jogos",
                 "Nacional":12.34,"Estadual":18.0,"Importado":21.45,"Municipal":0.0,
                 "Tipo":"0","VigenciaInicio":"20/06/2026","VigenciaFim":"31/12/2099",
                 "Chave":"ABC123","Versao":"26.1.L","Fonte":"IBPT/empresometro.com.br"}
                """);
        });

        var atualizado = await CreateService(db, encryption, handler)
            .TentarSincronizarProdutoAsync(produto.Id);

        atualizado.Should().BeTrue();
        await db.Entry(produto).ReloadAsync();
        produto.PercentualTributosFederais.Should().Be(federalEsperado);
        produto.PercentualTributosEstaduais.Should().Be(18m);
        produto.PercentualTributosMunicipais.Should().Be(0m);
        produto.TributosPreenchidosAutomaticamente.Should().BeTrue();
        produto.IbptVersao.Should().Be("26.1.L");
        produto.TributosVigenciaFim.Should().Be(new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        chamada!.Query.Should().Contain("codigo=95044000").And.Contain("token=token-secreto");
    }

    [Fact]
    public async Task SincronizarTodos_PreservaOverrideManualSemChamarApi()
    {
        await using var db = TestDbFactory.Create(nameof(SincronizarTodos_PreservaOverrideManualSemChamarApi));
        var encryption = CreateEncryption();
        var (_, produto) = await SeedAsync(db, encryption);
        produto.PercentualTributosFederais = 7m;
        produto.PercentualTributosEstaduais = 8m;
        produto.PercentualTributosMunicipais = 1m;
        produto.FonteTributos = "Validado pelo contador";
        produto.TributosPreenchidosAutomaticamente = false;
        await db.SaveChangesAsync();
        var chamadas = 0;
        var handler = new FakeHandler(_ => { chamadas++; return Json("{}"); });

        var resultado = await CreateService(db, encryption, handler).SincronizarTodosAsync();

        resultado.Atualizados.Should().Be(0);
        resultado.IgnoradosManuais.Should().Be(1);
        chamadas.Should().Be(0);
        produto.PercentualTributosFederais.Should().Be(7m);
    }

    [Fact]
    public async Task TentarSincronizarProduto_RespostaInvalidaNaoPersisteAlteracaoParcial()
    {
        await using var db = TestDbFactory.Create(nameof(TentarSincronizarProduto_RespostaInvalidaNaoPersisteAlteracaoParcial));
        var encryption = CreateEncryption();
        var (_, produto) = await SeedAsync(db, encryption);
        var handler = new FakeHandler(_ => Json("""
            {"Codigo":"95044000","UF":"SP","Nacional":12.34,"Estadual":18,
             "Importado":21.45,"Municipal":0,"VigenciaInicio":"data-invalida",
             "VigenciaFim":"31/12/2099","Chave":"ABC123","Versao":"26.1.L","Fonte":"IBPT"}
            """));

        var atualizado = await CreateService(db, encryption, handler)
            .TentarSincronizarProdutoAsync(produto.Id);

        atualizado.Should().BeFalse();
        await db.Entry(produto).ReloadAsync();
        produto.PercentualTributosFederais.Should().BeNull();
        produto.PercentualTributosEstaduais.Should().BeNull();
        produto.FonteTributos.Should().BeNull();
        produto.TributosPreenchidosAutomaticamente.Should().BeFalse();
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
