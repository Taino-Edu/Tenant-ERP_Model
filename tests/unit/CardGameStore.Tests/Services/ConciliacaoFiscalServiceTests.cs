// =============================================================================
// ConciliacaoFiscalServiceTests.cs — Venda sem documento fiscal não pode ficar
// invisível (CON-001).
//
// O aceite do plano é literal: "uma venda fechada com a opção fiscal desmarcada
// aparece no relatório no mesmo dia". Todo alerta existente parte de uma
// NotaFiscalEmitida e pergunta se ela está bem — o que nunca criou nota não
// existe para esses alertas. A conciliação inverte: parte das vendas.
// =============================================================================

using System.Runtime.CompilerServices;
using System.Text.Json;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class ConciliacaoFiscalServiceTests
{
    private static readonly DateTime Hoje = new(2026, 8, 5);

    private static AppDbContext CreateDb([CallerMemberName] string testName = "") =>
        TestDbFactory.Create($"{nameof(ConciliacaoFiscalServiceTests)}_{testName}");

    /// <summary>12:00 em Brasília do dia — bem dentro da janela, sem risco de virada.</summary>
    private static DateTime MomentoUtc(DateTime diaBr) =>
        DateTime.SpecifyKind(diaBr.Date.AddHours(15), DateTimeKind.Utc);

    /// <summary>Comanda exige UserId com FK válida — cria o cliente junto.</summary>
    private static async Task<Guid> SeedComandaFechadaAsync(AppDbContext db, int totalCentavos, DateTime? quando = null)
    {
        var user = new User { Id = Guid.NewGuid(), Name = "Cliente Conciliacao", Role = UserRole.Customer };
        db.Users.Add(user);

        var comanda = new Comanda
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Status = ComandaStatus.Fechada,
            ClosedAt = quando ?? MomentoUtc(Hoje),
            TotalInCents = totalCentavos,
            PaymentMethod = PaymentMethod.Dinheiro,
        };
        db.Comandas.Add(comanda);
        await db.SaveChangesAsync();
        return comanda.Id;
    }

    private static async Task<Guid> SeedVendaAvulsaAsync(
        AppDbContext db, int totalCentavos, bool cancelada = false)
    {
        var venda = new VendaAvulsa
        {
            Id = Guid.NewGuid(),
            SoldAt = MomentoUtc(Hoje),
            TotalInCents = totalCentavos,
            PaymentMethod = PaymentMethod.Pix,
            CanceladoEm = cancelada ? MomentoUtc(Hoje) : null,
        };
        db.VendasAvulsas.Add(venda);
        await db.SaveChangesAsync();
        return venda.Id;
    }

    private static async Task SeedNotaAsync(
        AppDbContext db, NotaFiscalOrigem origem, Guid vendaId,
        NotaFiscalStatus status, int valorCentavos, string? motivo = null)
    {
        db.NotasFiscaisEmitidas.Add(new NotaFiscalEmitida
        {
            Id = Guid.NewGuid(),
            Origem = origem,
            ComandaId = origem == NotaFiscalOrigem.Comanda ? vendaId : null,
            VendaAvulsaId = origem == NotaFiscalOrigem.VendaAvulsa ? vendaId : null,
            Status = status,
            ValorTotalEmCentavos = valorCentavos,
            MotivoRejeicao = motivo,
            Serie = 1,
            Numero = 100,
        });
        await db.SaveChangesAsync();
    }

    private static ConciliacaoFiscalService Servico(AppDbContext db) => new(db);

    // ── O caso que o sistema não enxergava ───────────────────────────────────

    [Fact]
    public async Task VendaFechadaSemNota_ApareceComoSemDocumento()
    {
        // É o aceite literal do CON-001.
        using var db = CreateDb();
        var comandaId = await SeedComandaFechadaAsync(db, 15_000);

        var resultado = await Servico(db).ConciliarAsync(Hoje, Hoje);

        var venda = resultado.Vendas.Should().ContainSingle().Subject;
        venda.VendaId.Should().Be(comandaId);
        venda.Situacao.Should().Be(SituacaoFiscalVenda.SemDocumento);
        venda.NotaId.Should().BeNull();
        venda.ExigeAtencao.Should().BeTrue();
        resultado.Pendencias.Should().ContainSingle();
        resultado.ValorSemDocumento.Should().Be(150.00m);
    }

    [Fact]
    public async Task ValorSemDocumento_SomaSoAsVendasSemNota()
    {
        using var db = CreateDb();
        await SeedComandaFechadaAsync(db, 10_000);                       // sem nota
        var comAutorizada = await SeedComandaFechadaAsync(db, 20_000);
        await SeedNotaAsync(db, NotaFiscalOrigem.Comanda, comAutorizada, NotaFiscalStatus.Autorizada, 20_000);

        var resultado = await Servico(db).ConciliarAsync(Hoje, Hoje);

        resultado.TotalVendas.Should().Be(2);
        resultado.ValorTotalVendas.Should().Be(300.00m);
        resultado.ValorSemDocumento.Should().Be(100.00m, "só a venda sem nota entra nesse total");
    }

    // ── Classificação por estado da nota ─────────────────────────────────────

    [Theory]
    [InlineData(NotaFiscalStatus.Autorizada,             SituacaoFiscalVenda.Autorizada,     false)]
    [InlineData(NotaFiscalStatus.AutorizadaContingencia, SituacaoFiscalVenda.EmContingencia, true)]
    [InlineData(NotaFiscalStatus.PendenteEmissao,        SituacaoFiscalVenda.Pendente,       true)]
    [InlineData(NotaFiscalStatus.Rejeitada,              SituacaoFiscalVenda.Rejeitada,      true)]
    [InlineData(NotaFiscalStatus.Cancelada,              SituacaoFiscalVenda.NotaCancelada,  false)]
    public async Task EstadoDaNota_DefineASituacaoEaNecessidadeDeAcao(
        NotaFiscalStatus status, SituacaoFiscalVenda esperada, bool exigeAtencao)
    {
        using var db = CreateDb();
        var comandaId = await SeedComandaFechadaAsync(db, 5_000);
        await SeedNotaAsync(db, NotaFiscalOrigem.Comanda, comandaId, status, 5_000);

        var venda = (await Servico(db).ConciliarAsync(Hoje, Hoje)).Vendas.Should().ContainSingle().Subject;

        venda.Situacao.Should().Be(esperada);
        venda.ExigeAtencao.Should().Be(exigeAtencao);
    }

    [Fact]
    public async Task Rejeitada_TrazOMotivoParaOOperadorAgir()
    {
        using var db = CreateDb();
        var comandaId = await SeedComandaFechadaAsync(db, 5_000);
        await SeedNotaAsync(db, NotaFiscalOrigem.Comanda, comandaId,
            NotaFiscalStatus.Rejeitada, 5_000, motivo: "Rejeicao 611: cEAN invalido");

        var venda = (await Servico(db).ConciliarAsync(Hoje, Hoje)).Vendas.Single();

        venda.MotivoRejeicao.Should().Contain("611");
    }

    // ── Divergência de valor ─────────────────────────────────────────────────

    [Fact]
    public async Task VendaEditadaDepoisDaEmissao_ApareceComoDivergencia()
    {
        // A nota ficou com o valor antigo e ninguém é avisado hoje. Divergência
        // de centavo pra cima já conta.
        using var db = CreateDb();
        var comandaId = await SeedComandaFechadaAsync(db, 12_000);
        await SeedNotaAsync(db, NotaFiscalOrigem.Comanda, comandaId, NotaFiscalStatus.Autorizada, 10_000);

        var venda = (await Servico(db).ConciliarAsync(Hoje, Hoje)).Vendas.Single();

        venda.ValorVenda.Should().Be(120.00m);
        venda.ValorNota.Should().Be(100.00m);
        venda.ValorDivergente.Should().BeTrue();
        venda.ExigeAtencao.Should().BeTrue("autorizada, mas com valor diferente da venda");
    }

    [Fact]
    public async Task ValoresIguais_NaoAcusamDivergencia()
    {
        using var db = CreateDb();
        var comandaId = await SeedComandaFechadaAsync(db, 9_999);
        await SeedNotaAsync(db, NotaFiscalOrigem.Comanda, comandaId, NotaFiscalStatus.Autorizada, 9_999);

        var venda = (await Servico(db).ConciliarAsync(Hoje, Hoje)).Vendas.Single();

        venda.ValorDivergente.Should().BeFalse();
        venda.ExigeAtencao.Should().BeFalse();
    }

    // ── Venda cancelada ──────────────────────────────────────────────────────

    [Fact]
    public async Task VendaAvulsaCancelada_NaoEhCobradaComoDocumentoFaltante()
    {
        // Aparece no relatório para o total bater, mas cobrar documento de venda
        // cancelada geraria ruído diário.
        using var db = CreateDb();
        await SeedVendaAvulsaAsync(db, 8_000, cancelada: true);

        var resultado = await Servico(db).ConciliarAsync(Hoje, Hoje);

        var venda = resultado.Vendas.Should().ContainSingle().Subject;
        venda.Situacao.Should().Be(SituacaoFiscalVenda.VendaCancelada);
        venda.ExigeAtencao.Should().BeFalse();
        resultado.Pendencias.Should().BeEmpty();
    }

    // ── Universo e período ───────────────────────────────────────────────────

    [Fact]
    public async Task ComandasEVendasAvulsas_EntramNoMesmoRelatorio()
    {
        using var db = CreateDb();
        await SeedComandaFechadaAsync(db, 10_000);
        await SeedVendaAvulsaAsync(db, 5_000);

        var resultado = await Servico(db).ConciliarAsync(Hoje, Hoje);

        resultado.TotalVendas.Should().Be(2);
        resultado.Vendas.Select(v => v.Origem).Should().BeEquivalentTo(new[] { "Comanda", "VendaAvulsa" });
        resultado.ValorTotalVendas.Should().Be(150.00m);
    }

    [Fact]
    public async Task VendaForaDoPeriodo_NaoEntra()
    {
        using var db = CreateDb();
        await SeedComandaFechadaAsync(db, 10_000, quando: MomentoUtc(Hoje.AddDays(-5)));
        await SeedComandaFechadaAsync(db, 20_000);

        var resultado = await Servico(db).ConciliarAsync(Hoje, Hoje);

        resultado.TotalVendas.Should().Be(1);
        resultado.ValorTotalVendas.Should().Be(200.00m);
    }

    [Fact]
    public async Task ComandaAberta_NaoEhVendaTributavel()
    {
        using var db = CreateDb();
        var user = new User { Id = Guid.NewGuid(), Name = "Cliente Aberto", Role = UserRole.Customer };
        db.Users.Add(user);
        db.Comandas.Add(new Comanda
        {
            Id = Guid.NewGuid(), UserId = user.Id, Status = ComandaStatus.Aberta,
            TotalInCents = 10_000, PaymentMethod = PaymentMethod.Dinheiro,
        });
        await db.SaveChangesAsync();

        (await Servico(db).ConciliarAsync(Hoje, Hoje)).TotalVendas.Should().Be(0);
    }

    // ── Resumo ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resumo_TrazTodasAsSituacoesInclusiveZeradas()
    {
        // "SemDocumento: 0" é informação — some se o dicionário só trouxer o que
        // ocorreu, e a tela perde a capacidade de dizer "está tudo certo".
        using var db = CreateDb();
        var comandaId = await SeedComandaFechadaAsync(db, 10_000);
        await SeedNotaAsync(db, NotaFiscalOrigem.Comanda, comandaId, NotaFiscalStatus.Autorizada, 10_000);

        var resultado = await Servico(db).ConciliarAsync(Hoje, Hoje);

        resultado.PorSituacao.Should().HaveCount(Enum.GetValues<SituacaoFiscalVenda>().Length);
        resultado.PorSituacao[nameof(SituacaoFiscalVenda.Autorizada)].Quantidade.Should().Be(1);
        resultado.PorSituacao[nameof(SituacaoFiscalVenda.SemDocumento)].Quantidade.Should().Be(0);
        resultado.QuantidadePendencias.Should().Be(0);
    }

    [Fact]
    public async Task PeriodoSemVendas_NaoQuebra()
    {
        using var db = CreateDb();

        var resultado = await Servico(db).ConciliarAsync(Hoje, Hoje);

        resultado.TotalVendas.Should().Be(0);
        resultado.ValorTotalVendas.Should().Be(0m);
        resultado.ValorSemDocumento.Should().Be(0m);
        resultado.Pendencias.Should().BeEmpty();
    }

    // ── Decisão registrada no fechamento (CON-003) ────────────────────────────

    [Fact]
    public async Task Conciliar_VendaSemDocumentoComEscolhaRegistrada_MostraQuemDecidiu()
    {
        // O caso que a seção 36.5 do plano diz ser inviável sem registro: o
        // contador recebe a venda sem documento e precisa saber se foi decisão
        // de alguém ou falha do sistema.
        using var db = CreateDb();
        var operador = new User
        {
            Id = Guid.NewGuid(), Name = "Caixa 1", Role = UserRole.Admin, IsActive = true,
        };
        db.Users.Add(operador);
        var comandaId = await SeedComandaFechadaAsync(db, 5000);
        var comanda = await db.Comandas.FindAsync(comandaId);
        comanda!.FiscalEmissaoEscolhida = false;
        comanda.FiscalDecisaoPorUserId  = operador.Id;
        comanda.FiscalDecisaoEm         = MomentoUtc(Hoje);
        await db.SaveChangesAsync();

        var resultado = await new ConciliacaoFiscalService(db).ConciliarAsync(Hoje, Hoje);

        var venda = resultado.Vendas.Single(v => v.VendaId == comandaId);
        venda.Situacao.Should().Be(SituacaoFiscalVenda.SemDocumento);
        venda.EmissaoEscolhida.Should().BeFalse();
        venda.DecididaPor.Should().Be("Caixa 1");
        venda.SemDocumentoPorEscolha.Should().BeTrue();
    }

    [Fact]
    public async Task Conciliar_VendaAntigaSemRegistro_NaoInventaDecisao()
    {
        // Nulo é "não sabemos", não "ninguém escolheu". Vendas anteriores ao
        // registro não podem aparecer como se tivessem sido decididas.
        using var db = CreateDb();
        var comandaId = await SeedComandaFechadaAsync(db, 5000);

        var resultado = await new ConciliacaoFiscalService(db).ConciliarAsync(Hoje, Hoje);

        var venda = resultado.Vendas.Single(v => v.VendaId == comandaId);
        venda.EmissaoEscolhida.Should().BeNull();
        venda.DecididaPor.Should().BeNull();
        venda.SemDocumentoPorEscolha.Should().BeFalse(
            "sem registro não dá para afirmar que alguém escolheu não emitir");
    }

    // ── Contrato JSON ────────────────────────────────────────────────────────

    [Fact]
    public void Situacao_SerializaPeloNome_NaoPeloIndice()
    {
        // O portal do contador traduz a situação indexando um mapa por NOME —
        // igual ao PorSituacao deste mesmo DTO, que sempre foi
        // Dictionary<string, ...>. Enquanto o campo saía como índice (5), a
        // busca falhava e a coluna "Situação" da Conciliação aparecia como um
        // badge VAZIO em toda linha, no desktop e no celular.
        //
        // Prende o índice também: reordenar os membros do enum trocaria a
        // situação de todas as vendas já persistidas em qualquer consumidor que
        // ainda leia número.
        var json = JsonSerializer.Serialize(SituacaoFiscalVenda.SemDocumento);

        json.Should().Be("\"SemDocumento\"");
        ((int)SituacaoFiscalVenda.Autorizada).Should().Be(0, "a ordem dos membros é contrato");
    }

    [Fact]
    public void Situacao_DentroDoDto_SerializaPeloNome()
    {
        // A serialização do valor solto pode passar e a do DTO falhar se alguém
        // trocar o tipo do campo — é o objeto inteiro que trafega na API.
        var dto = new VendaConciliadaDto(
            VendaId: Guid.NewGuid(), Origem: "VendaAvulsa", OcorridaEm: Hoje,
            ValorVenda: 100m, Situacao: SituacaoFiscalVenda.SemDocumento,
            NotaId: null, Serie: null, Numero: null, ChaveAcesso: null,
            ValorNota: null, MotivoRejeicao: null);

        var json = JsonSerializer.Serialize(dto);

        json.Should().Contain("\"SemDocumento\"");
        json.Should().NotContain("\"Situacao\":5");
    }
}
