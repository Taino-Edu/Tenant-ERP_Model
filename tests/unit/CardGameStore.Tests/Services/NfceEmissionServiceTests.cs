// =============================================================================
// NfceEmissionServiceTests.cs — Testes do motor de emissão de NFC-e.
// Não há como testar uma transmissão real à SEFAZ neste ambiente (sem
// certificado de homologação nem rede) — os testes aqui verificam a garantia
// central do serviço: nunca lançar exceção e sempre deixar a nota registrada
// como PendenteEmissao quando a emissão não pode ser concluída.
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

namespace CardGameStore.Tests.Services;

public class NfceEmissionServiceTests
{
    private static AppDbContext CreateDb()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static EncryptionService CreateEncryptionService()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        return new EncryptionService(config, env.Object);
    }

    private static NfceEmissionService CreateService(AppDbContext db) =>
        new(db, new Mock<IMongoDatabase>().Object, CreateEncryptionService(), NullLogger<NfceEmissionService>.Instance);

    private static async Task<Comanda> SeedComandaFechadaAsync(AppDbContext db)
    {
        var user = new User { Id = Guid.NewGuid(), Name = "Cliente Teste", Role = UserRole.Customer };
        db.Users.Add(user);

        var product = new Product { Id = Guid.NewGuid(), Name = "Booster Pack", Category = "MTG", PriceInCents = 1500, StockQuantity = 10, Ncm = "95044000" };
        db.Products.Add(product);

        var comanda = new Comanda
        {
            Id            = Guid.NewGuid(),
            UserId        = user.Id,
            Status        = ComandaStatus.Fechada,
            TotalInCents  = 1500,
            PaymentMethod = "Dinheiro",
        };
        comanda.Items.Add(new ComandaItem
        {
            ComandaId          = comanda.Id,
            ProductId          = product.Id,
            ItemNameSnapshot    = product.Name,
            UnitPriceInCents    = 1500,
            Quantity            = 1,
            SubtotalInCents     = 1500,
        });
        db.Comandas.Add(comanda);
        await db.SaveChangesAsync();
        return comanda;
    }

    [Fact]
    public async Task EmitirParaComandaAsync_SemFiscalConfig_RegistraPendenteEmissaoSemLancarExcecao()
    {
        using var db = CreateDb();
        var comanda = await SeedComandaFechadaAsync(db);
        var service = CreateService(db);

        var nota = await service.EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.PendenteEmissao);
        nota.ComandaId.Should().Be(comanda.Id);
        nota.ValorTotalEmCentavos.Should().Be(1500);
    }

    [Fact]
    public async Task EmitirParaComandaAsync_ComFiscalConfigIncompleto_RegistraPendenteEmissaoSemLancarExcecao()
    {
        using var db = CreateDb();
        var comanda = await SeedComandaFechadaAsync(db);

        // Config existe mas sem certificado/endereço — deve cair no caminho "não configurado".
        db.FiscalConfigs.Add(new FiscalConfig { Cnpj = "12345678000100" });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var nota = await service.EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.PendenteEmissao);
    }

    [Fact]
    public async Task EmitirParaComandaAsync_ComandaInexistente_NuncaLancaExcecao()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var nota = await service.EmitirParaComandaAsync(Guid.NewGuid());

        nota.Status.Should().Be(NotaFiscalStatus.PendenteEmissao);
    }

    [Fact]
    public async Task EmitirParaVendaAvulsaAsync_SemVendaCorrespondente_NuncaLancaExcecao()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var nota = await service.EmitirParaVendaAvulsaAsync(MongoDB.Bson.ObjectId.GenerateNewId().ToString());

        nota.Status.Should().Be(NotaFiscalStatus.PendenteEmissao);
        nota.Origem.Should().Be(NotaFiscalOrigem.VendaAvulsa);
    }

    [Fact]
    public async Task ReprocessarAsync_NotaAutorizada_DevolveSemTentarDeNovo()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida { Status = NotaFiscalStatus.Autorizada, ChaveAcesso = "X", Protocolo = "P" };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var resultado = await service.ReprocessarAsync(nota.Id);

        resultado.Status.Should().Be(NotaFiscalStatus.Autorizada);
        resultado.TentativasReprocessamento.Should().Be(0); // nem tentou — devolveu direto
    }

    [Fact]
    public async Task ReprocessarAsync_AcimaDoLimiteDeTentativas_NaoTentaDeNovo()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida { Status = NotaFiscalStatus.Rejeitada, TentativasReprocessamento = 10 };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var resultado = await service.ReprocessarAsync(nota.Id);

        resultado.TentativasReprocessamento.Should().Be(10); // não incrementou — nem tentou
    }

    [Fact]
    public async Task CancelarAsync_NotaNaoAutorizada_LancaExcecao()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida { Status = NotaFiscalStatus.PendenteEmissao };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Func<Task> act = () => service.CancelarAsync(nota.Id, "Motivo com mais de quinze caracteres");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Autorizada*");
    }

    [Fact]
    public async Task CancelarAsync_JustificativaCurta_LancaExcecao()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida { Status = NotaFiscalStatus.Autorizada, EmitidoEm = DateTime.UtcNow };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Func<Task> act = () => service.CancelarAsync(nota.Id, "curta demais");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*15 caracteres*");
    }

    [Fact]
    public async Task CancelarAsync_ForaDaJanelaLegal_LancaExcecao()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida
        {
            Status = NotaFiscalStatus.Autorizada,
            EmitidoEm = DateTime.UtcNow.AddHours(-2), // muito depois dos 30 min de janela
        };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Func<Task> act = () => service.CancelarAsync(nota.Id, "Motivo com mais de quinze caracteres");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*janela legal*");
    }
}
