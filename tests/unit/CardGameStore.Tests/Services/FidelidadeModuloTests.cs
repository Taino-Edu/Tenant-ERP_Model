// =============================================================================
// FidelidadeModuloTests.cs — pontos e cashback só funcionam com o módulo ligado.
//
// Motivo do cartão: a decisão de produto foi tornar fidelidade OPCIONAL — um
// módulo à parte, que acrescenta ao que já existe. O mecanismo já existia
// (EnabledModules + SiteConfig.PontosFidelidadeAtivo), mas cobria só metade:
//
//   • pontos como pagamento     → tinha gate
//   • ApplyPoints               → tinha gate
//   • acúmulo de pontos         → só olhava o toggle, ignorava o módulo
//   • CASHBACK                  → não passava por gate NENHUM
//
// Ou seja: desligar "pontos" deixava o cashback funcionando e o saldo do cliente
// continuava crescendo. Estes testes fixam que desligar desliga tudo — e que
// crediário, que NÃO faz parte do programa, segue intacto.
// =============================================================================

using System.Runtime.CompilerServices;
using CardGameStore.Data;
using CardGameStore.Hubs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Services;

public class FidelidadeModuloTests
{
    private static AppDbContext CreateDb([CallerMemberName] string testName = "") =>
        TestDbFactory.Create($"{nameof(FidelidadeModuloTests)}_{testName}");

    private static ComandaService CriarServico(AppDbContext db, params string[] modulos)
    {
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.EnabledModules).Returns(modulos);

        var hub = new Mock<IHubContext<ComandaHub>>();
        hub.Setup(h => h.Clients.All).Returns(new Mock<IClientProxy>().Object);
        hub.Setup(h => h.Clients.Group(It.IsAny<string>())).Returns(new Mock<IClientProxy>().Object);

        return new ComandaService(
            db, new Mock<IEmailService>().Object, NullLogger<ComandaService>.Instance,
            new Mock<IServiceScopeFactory>().Object, hub.Object, tenant.Object);
    }

    /// <summary>Cliente com saldo nos dois programas (pontos e cashback — este
    /// último guardado em BalanceInCents), para provar que nenhum é aceito com o
    /// módulo desligado.</summary>
    private static async Task<User> SeedClienteComSaldoAsync(AppDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Name = "Cliente Fidelidade", Role = UserRole.Customer,
            IsActive = true, PointsBalance = 5_000, BalanceInCents = 5_000,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Comanda> SeedComandaAbertaAsync(AppDbContext db, User user, int totalCentavos)
    {
        var comanda = new Comanda
        {
            Id = Guid.NewGuid(), UserId = user.Id, Status = ComandaStatus.Aberta,
            TotalInCents = totalCentavos, OpenedAt = DateTime.UtcNow,
        };
        db.Comandas.Add(comanda);
        await db.SaveChangesAsync();
        return comanda;
    }

    [Theory]
    [InlineData(PaymentMethod.Pontos)]
    [InlineData(PaymentMethod.Cashback)]
    public async Task ModuloDesligado_RecusaFechamentoComFidelidade(string metodo)
    {
        // O cashback é o caso novo: antes desta mudança ele fechava a venda
        // normalmente com o módulo desligado, debitando o saldo do cliente.
        using var db = CreateDb();
        var user = await SeedClienteComSaldoAsync(db);
        var comanda = await SeedComandaAbertaAsync(db, user, 5_000);
        var service = CriarServico(db, "restaurante");

        var act = async () => await service.CloseComandaAsync(
            comanda.Id, Guid.NewGuid(), paymentMethod: metodo);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("fidelidade",
                "a mensagem precisa dizer que é o programa, não um erro genérico de pagamento");
    }

    [Theory]
    [InlineData(PaymentMethod.Pontos)]
    [InlineData(PaymentMethod.Cashback)]
    public async Task ModuloLigado_AceitaFechamentoComFidelidade(string metodo)
    {
        // O outro lado: quem contratou continua usando. "Desconectar" não pode
        // virar "quebrar para quem paga".
        using var db = CreateDb();
        var user = await SeedClienteComSaldoAsync(db);
        var comanda = await SeedComandaAbertaAsync(db, user, 2_000);
        var service = CriarServico(db, "pontos");

        var act = async () => await service.CloseComandaAsync(
            comanda.Id, Guid.NewGuid(), paymentMethod: metodo);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ModuloDesligado_NaoCreditaPontosNaVenda()
    {
        // Sem isto, o saldo continuava crescendo em toda venda de uma loja que
        // não tem o programa — dívida silenciosa com o cliente, que ninguém
        // conseguiria gastar.
        using var db = CreateDb();
        var user = await SeedClienteComSaldoAsync(db);
        var saldoAntes = user.PointsBalance;
        var comanda = await SeedComandaAbertaAsync(db, user, 10_000);
        var service = CriarServico(db, "restaurante");

        await service.CloseComandaAsync(comanda.Id, Guid.NewGuid(), PaymentMethod.Dinheiro);

        var salvo = await db.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        salvo.PointsBalance.Should().Be(saldoAntes, "loja sem o módulo não acumula saldo");
    }

    [Fact]
    public async Task ModuloDesligado_CrediarioContinuaFuncionando()
    {
        // Crediário NÃO é programa de fidelidade — é venda a prazo, e a decisão
        // do produto foi manter. Se este teste quebrar, a remoção pegou demais.
        using var db = CreateDb();
        var user = await SeedClienteComSaldoAsync(db);
        var comanda = await SeedComandaAbertaAsync(db, user, 3_000);
        var service = CriarServico(db, "restaurante");

        var act = async () => await service.CloseComandaAsync(
            comanda.Id, Guid.NewGuid(), PaymentMethod.Crediario);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ToggleDaLojaDesligado_RecusaMesmoComModuloContratado()
    {
        // Dois "sim" independentes: a plataforma habilita o módulo, o lojista
        // liga o programa. Um não substitui o outro.
        using var db = CreateDb();
        db.SiteConfigs.Add(new SiteConfig { Id = SiteConfig.SingletonId, PontosFidelidadeAtivo = false });
        var user = await SeedClienteComSaldoAsync(db);
        var comanda = await SeedComandaAbertaAsync(db, user, 2_000);
        var service = CriarServico(db, "pontos");

        var act = async () => await service.CloseComandaAsync(
            comanda.Id, Guid.NewGuid(), PaymentMethod.Cashback);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
