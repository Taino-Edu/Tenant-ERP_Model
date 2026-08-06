// =============================================================================
// IbptTaxServiceTests.cs — o IBPT lento não pode derrubar a sincronização.
//
// Defeito real, capturado no log de produção (trace 0HNNI4IMEL3EK:00000001):
//
//   POST /api/fiscal/ibpt/sincronizar
//   TaskCanceledException: The request was canceled due to the configured
//   HttpClient.Timeout of 15 seconds elapsing.
//
// A causa é sutil e vale registrar: o laço de sincronização protegia cada
// produto com `catch (Exception ex) when (ex is not OperationCanceledException)`
// — filtro escrito para deixar um cancelamento real subir. Só que
// HttpClient.Timeout lança TaskCanceledException, que HERDA de
// OperationCanceledException. Pelo tipo, timeout e cancelamento são a mesma
// coisa; o que os separa é se o token do chamador foi cancelado.
//
// Sem essa distinção, um IBPT fora do ar virava 500 "Erro interno. Tente
// novamente em instantes" — mensagem duplamente falsa: não é interno, e
// insistir não muda nada.
// =============================================================================

using System.Net;
using System.Runtime.CompilerServices;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Services;

public class IbptTaxServiceTests
{
    private static AppDbContext CreateDb([CallerMemberName] string testName = "") =>
        TestDbFactory.Create($"{nameof(IbptTaxServiceTests)}_{testName}");

    /// <summary>Handler que reproduz exatamente o estouro de HttpClient.Timeout:
    /// TaskCanceledException com TimeoutException por dentro, e o token do
    /// chamador intacto.</summary>
    private sealed class HandlerQueEstouraTimeout : HttpMessageHandler
    {
        public int Chamadas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Chamadas++;
            return Task.FromException<HttpResponseMessage>(
                new TaskCanceledException(
                    "The request was canceled due to the configured HttpClient.Timeout of 15 seconds elapsing.",
                    new TimeoutException("A task was canceled.")));
        }
    }

    private sealed class FabricaDeCliente(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("https://apidoni.ibpt.org.br/") };
    }

    private static EncryptionService CreateEncryptionService()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        return new EncryptionService(config, env.Object);
    }

    private static async Task SeedLojaComTokenAsync(AppDbContext db, int produtos = 1)
    {
        var enc = CreateEncryptionService();
        db.FiscalConfigs.Add(new FiscalConfig
        {
            Cnpj                = "12345678000195",
            Uf                  = "SP",
            IbptTokenEncrypted  = enc.Encrypt("token-de-teste"),
            IbptAutoSyncEnabled = true,
        });
        for (var i = 0; i < produtos; i++)
            db.Products.Add(new Product
            {
                Id = Guid.NewGuid(), Name = $"Produto {i + 1}", Category = "MTG",
                PriceInCents = 1000, StockQuantity = 5, Ncm = "95044000", IsActive = true,
            });
        await db.SaveChangesAsync();
    }

    private static IbptTaxService CreateService(AppDbContext db, HttpMessageHandler handler) =>
        new(db, new FabricaDeCliente(handler), CreateEncryptionService(),
            NullLogger<IbptTaxService>.Instance);

    [Fact]
    public async Task SincronizarTodos_IbptEstourandoTimeout_NaoPropagaExcecao()
    {
        // O cerne: antes isto lançava TaskCanceledException para fora e o
        // endpoint devolvia 500.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db);
        using var handler = new HandlerQueEstouraTimeout();

        var act = async () => await CreateService(db, handler).SincronizarTodosAsync();

        await act.Should().NotThrowAsync(
            "timeout de um serviço de terceiro é falha do produto, não do nosso servidor");
    }

    [Fact]
    public async Task SincronizarTodos_IbptEstourandoTimeout_RelataFalhaPorProduto()
    {
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db, produtos: 3);
        using var handler = new HandlerQueEstouraTimeout();

        var resultado = await CreateService(db, handler).SincronizarTodosAsync();

        resultado.Falhas.Should().Be(3, "cada produto registra a própria falha");
        resultado.Atualizados.Should().Be(0);
        resultado.Erros.Should().OnlyContain(m => m.Contains("não respondeu"),
            "a mensagem precisa dizer que foi o IBPT que não respondeu, não 'falha inesperada' — " +
            "senão o lojista não sabe se o problema é o token dele");
    }

    [Fact]
    public async Task SincronizarTodos_IbptLento_SegueParaOsDemaisProdutos()
    {
        // Prova que o laço não para no primeiro erro: com 3 produtos, o handler
        // precisa ter sido chamado 3 vezes.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db, produtos: 3);
        using var handler = new HandlerQueEstouraTimeout();

        await CreateService(db, handler).SincronizarTodosAsync();

        handler.Chamadas.Should().Be(3);
    }

    [Fact]
    public async Task SincronizarTodos_CancelamentoDeVerdade_Aborta()
    {
        // O outro lado da moeda: se o chamador cancelou (aba fechada, deploy
        // derrubando o processo), continuar consumindo a API de terceiro seria
        // errado. O filtro precisa distinguir os dois, e não achatar ambos.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db, produtos: 3);
        using var handler = new HandlerQueEstouraTimeout();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await CreateService(db, handler).SincronizarTodosAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.Chamadas.Should().Be(0, "nem o primeiro produto deve ser consultado");
    }
}
