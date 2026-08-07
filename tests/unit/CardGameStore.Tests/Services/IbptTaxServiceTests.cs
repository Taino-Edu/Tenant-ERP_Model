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
using Microsoft.EntityFrameworkCore;
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


    /// <summary>Handler que responde como o IBPT responderia — para exercitar a
    /// construção da tabela local, e contar quantas vezes a rede foi tocada.</summary>
    private sealed class HandlerQueResponde : HttpMessageHandler
    {
        public int Chamadas { get; private set; }
        public List<string> NcmsConsultados { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Chamadas++;
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            NcmsConsultados.Add(query["codigo"] ?? "");

            const string corpo = """
                {"Codigo":"95044000","UF":"SP","EX":0,"Descricao":"Jogos",
                 "Nacional":12.5,"Estadual":18.0,"Importado":15.5,"Municipal":0.0,
                 "Tipo":"0","VigenciaInicio":"01/01/2026","VigenciaFim":"31/12/2026",
                 "Chave":"ABC123","Versao":"26.1.A","Fonte":"IBPT"}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(corpo, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>Serviço no ar, mas recusando este NCM — distinto de estar fora.</summary>
    private sealed class HandlerQueRecusa : HttpMessageHandler
    {
        public int Chamadas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Chamadas++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            });
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

    // ── IBPT-002: a rede sai do caminho do usuário ────────────────────────────

    [Fact]
    public async Task AtualizarTabelaLocal_ConsultaUmaVezPorNcmDistinto_NaoPorProduto()
    {
        // O ganho que motiva o cartão: dez produtos do mesmo NCM custam UMA
        // consulta. Antes, o custo crescia com o catálogo.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db, produtos: 10);
        using var handler = new HandlerQueResponde();

        await CreateService(db, handler).AtualizarTabelaLocalAsync();

        handler.Chamadas.Should().Be(1, "os 10 produtos compartilham o mesmo NCM");
        db.IbptTabela.Should().ContainSingle();
    }

    [Fact]
    public async Task AtualizarTabelaLocal_DuasExecucoes_NaoDuplicamALinha()
    {
        // O job roda todo dia; sem upsert, a tabela cresceria sem limite e o
        // lookup passaria a depender de qual linha vem primeiro.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db, produtos: 2);
        using var handler = new HandlerQueResponde();
        var service = CreateService(db, handler);

        await service.AtualizarTabelaLocalAsync();
        await service.AtualizarTabelaLocalAsync();

        db.IbptTabela.Should().ContainSingle("a chave é (NCM, UF, origem), não a execução");
    }

    [Fact]
    public async Task PreencherProdutoDaTabelaLocal_NaoTocaNaRede()
    {
        // O coração do cartão. Se este teste falhar, a rede voltou para dentro
        // da requisição e o 500 de produção volta com ela.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db);
        using var handlerDaCarga = new HandlerQueResponde();
        await CreateService(db, handlerDaCarga).AtualizarTabelaLocalAsync();

        var produto = await db.Products.FirstAsync();
        produto.PercentualTributosFederais = null;
        produto.TributosPreenchidosAutomaticamente = false;
        await db.SaveChangesAsync();

        using var handlerProibido = new HandlerQueEstouraTimeout();
        var preencheu = await CreateService(db, handlerProibido)
            .PreencherProdutoDaTabelaLocalAsync(produto.Id);

        preencheu.Should().BeTrue();
        handlerProibido.Chamadas.Should().Be(0, "o preenchimento tem que sair da tabela local");
        var salvo = await db.Products.FindAsync(produto.Id);
        salvo!.PercentualTributosFederais.Should().Be(12.5m);
        salvo.FonteTributos.Should().Contain("IBPT");
    }

    [Fact]
    public async Task PreencherProdutoDaTabelaLocal_NcmForaDaTabela_NaoInventaValor()
    {
        // NCM novo é situação normal — o job resolve no próximo ciclo. O que não
        // pode é preencher com valor de outro NCM nem com zero.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db);
        var produto = await db.Products.FirstAsync();

        using var handler = new HandlerQueEstouraTimeout();
        var preencheu = await CreateService(db, handler).PreencherProdutoDaTabelaLocalAsync(produto.Id);

        preencheu.Should().BeFalse();
        handler.Chamadas.Should().Be(0);
        var salvo = await db.Products.FindAsync(produto.Id);
        salvo!.PercentualTributosFederais.Should().BeNull();
    }

    [Fact]
    public async Task AplicarTabelaLocal_ComIbptForaDoAr_ContinuaFuncionando()
    {
        // A consequência prática: o IBPT cair não impede mais o lojista de
        // trabalhar. A última tabela conhecida continua valendo.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db, produtos: 3);
        using var handlerDaCarga = new HandlerQueResponde();
        await CreateService(db, handlerDaCarga).AtualizarTabelaLocalAsync();

        using var handlerForaDoAr = new HandlerQueEstouraTimeout();
        var resultado = await CreateService(db, handlerForaDoAr).AplicarTabelaLocalAsync();

        resultado.Atualizados.Should().Be(3);
        handlerForaDoAr.Chamadas.Should().Be(0);
    }

    [Fact]
    public async Task AplicarTabelaLocal_PreenchimentoManualDoContador_NaoEhSobrescrito()
    {
        // Regra que já valia no modelo antigo e não pode se perder na mudança.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db);
        using var handler = new HandlerQueResponde();
        await CreateService(db, handler).AtualizarTabelaLocalAsync();

        var produto = await db.Products.FirstAsync();
        produto.PercentualTributosFederais = 99m;
        produto.PercentualTributosEstaduais = 99m;
        produto.PercentualTributosMunicipais = 99m;
        produto.FonteTributos = "Tabela do contador";
        produto.TributosPreenchidosAutomaticamente = false;
        await db.SaveChangesAsync();

        var resultado = await CreateService(db, handler).AplicarTabelaLocalAsync();

        resultado.IgnoradosManuais.Should().Be(1);
        var salvo = await db.Products.FindAsync(produto.Id);
        salvo!.PercentualTributosFederais.Should().Be(99m, "o valor do contador tem precedência");
    }

    [Fact]
    public async Task AtualizarTabelaLocal_NcmComTamanhoErrado_ReportaEmVezDeIgnorar()
    {
        // Antes, NCM fora do formato era descartado em SILÊNCIO: o job não
        // consultava, não registrava erro, e a aplicação da tabela dizia apenas
        // "NCM ainda não está na tabela local" — para sempre, sem nada no painel
        // explicando por quê. O produto ficava sem transparência tributária e a
        // NFC-e dele nunca era emitida.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db);
        var produto = await db.Products.FirstAsync();
        produto.Ncm = "6109100";   // 7 dígitos
        await db.SaveChangesAsync();
        using var handler = new HandlerQueResponde();

        var resultado = await CreateService(db, handler).AtualizarTabelaLocalAsync();

        handler.Chamadas.Should().Be(0, "não faz sentido consultar o IBPT com NCM inválido");
        resultado.Erros.Should().ContainSingle()
            .Which.Should().Contain("7 dígito(s)").And.Contain("exige exatamente 8",
                "o painel precisa dizer qual produto e o que corrigir");

        var cfg = await db.FiscalConfigs.AsNoTracking().FirstAsync();
        cfg.IbptUltimoErro.Should().Contain("dígito(s)", "e isso tem que chegar à tela");
    }

    [Fact]
    public async Task AtualizarTabelaLocal_ServicoForaDoAr_ParaNoPrimeiroEmVezDeMartelar()
    {
        // Em homologação foram 4 timeouts idênticos por ciclo, a cada ciclo, cada
        // um até o limite — o job segurando um worker por minutos e martelando um
        // servidor que não responde. O primeiro timeout já contou tudo.
        //
        // Se a causa for bloqueio por excesso de requisição, isto também é o que
        // impede a reincidência.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db, produtos: 4);
        var produtos = await db.Products.ToListAsync();
        for (var i = 0; i < produtos.Count; i++) produtos[i].Ncm = $"9504400{i}";
        await db.SaveChangesAsync();
        using var handler = new HandlerQueEstouraTimeout();

        var resultado = await CreateService(db, handler).AtualizarTabelaLocalAsync();

        handler.Chamadas.Should().Be(1, "o primeiro timeout já diz tudo sobre os outros três");
        resultado.Erros.Should().ContainSingle()
            .Which.Should().Contain("interrompida neste ciclo");
    }

    [Fact]
    public async Task AtualizarTabelaLocal_FalhaDeUmNcmSo_NaoInterrompeOsDemais()
    {
        // O outro lado: erro do NCM (código recusado, resposta malformada) não
        // diz nada sobre os outros. Achatar os dois casos faria uma classificação
        // errada de um produto travar a tabela inteira.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db, produtos: 3);
        var produtos = await db.Products.ToListAsync();
        for (var i = 0; i < produtos.Count; i++) produtos[i].Ncm = $"9504400{i}";
        await db.SaveChangesAsync();
        using var handler = new HandlerQueRecusa();

        var resultado = await CreateService(db, handler).AtualizarTabelaLocalAsync();

        handler.Chamadas.Should().Be(3, "cada NCM merece a própria tentativa");
        resultado.Falhas.Should().Be(3);
    }

    // ── NCM que ainda não está na tabela (o caso do cadastro novo) ────────────

    [Fact]
    public async Task GarantirNcm_QuandoFaltaNaTabela_BuscaSoEleEPreenche()
    {
        // Tirar a rede da requisição não podia significar tirar o preenchimento:
        // um produto com NCM novo ficava sem transparência tributária até o job do
        // dia seguinte — e sem ela a NFC-e daquele produto não é emitida.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db);
        var produto = await db.Products.FirstAsync();
        using var handler = new HandlerQueResponde();

        var preencheu = await CreateService(db, handler).GarantirNcmNaTabelaEPreencherAsync(produto.Id);

        preencheu.Should().BeTrue();
        handler.Chamadas.Should().Be(1, "uma consulta, para um NCM só");
        var salvo = await db.Products.AsNoTracking().FirstAsync(p => p.Id == produto.Id);
        salvo.PercentualTributosFederais.Should().Be(12.5m);
    }

    [Fact]
    public async Task GarantirNcm_QuandoJaEstaNaTabela_NaoTocaNaRede()
    {
        // Segunda edição do mesmo produto não repete a consulta.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db);
        using var handlerCarga = new HandlerQueResponde();
        await CreateService(db, handlerCarga).AtualizarTabelaLocalAsync();

        var produto = await db.Products.FirstAsync();
        produto.TributosPreenchidosAutomaticamente = false;
        produto.PercentualTributosFederais = null;
        await db.SaveChangesAsync();

        using var handlerProibido = new HandlerQueEstouraTimeout();
        var preencheu = await CreateService(db, handlerProibido)
            .GarantirNcmNaTabelaEPreencherAsync(produto.Id);

        preencheu.Should().BeTrue();
        handlerProibido.Chamadas.Should().Be(0);
    }

    [Fact]
    public async Task GarantirNcm_ComIbptForaDoAr_RegistraOErroVisivelAoLojista()
    {
        // A tarefa roda fora da requisição: se a falha só for para o log, o
        // lojista vê o produto sem tributos e não tem como descobrir por quê.
        using var db = CreateDb();
        await SeedLojaComTokenAsync(db);
        var produto = await db.Products.FirstAsync();
        using var handler = new HandlerQueEstouraTimeout();

        var preencheu = await CreateService(db, handler).GarantirNcmNaTabelaEPreencherAsync(produto.Id);

        preencheu.Should().BeFalse();
        var cfg = await db.FiscalConfigs.AsNoTracking().FirstAsync();
        cfg.IbptUltimoErro.Should().NotBeNullOrWhiteSpace()
            .And.Contain("não respondeu", "o painel precisa dizer o que houve, não ficar em branco");
    }
}
