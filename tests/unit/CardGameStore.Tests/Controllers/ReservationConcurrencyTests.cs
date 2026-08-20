using System.Security.Claims;
using CardGameStore.Controllers;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Tests.Controllers;

/// <summary>
/// A corrida do M4: duas homologações simultâneas da MESMA reserva.
///
/// Precisa de Postgres real e não roda no provider InMemory — o
/// <c>ExecuteUpdateAsync</c> que serializa a reivindicação simplesmente não
/// existe lá. Um teste que passasse em memória não estaria provando nada sobre
/// o que a correção faz.
/// </summary>
public sealed class ReservationConcurrencyTests
{
    [Fact]
    public async Task Homologar_DuasChamadasSimultaneas_RegistraUmaVendaSo()
    {
        await using var dbA = TestDbFactory.Create(nameof(ReservationConcurrencyTests));
        var reservaId = await SeedReservaAtivaAsync(dbA);

        // Dois contextos sobre o mesmo schema = duas requisições concorrentes.
        await using var dbB = TestDbFactory.CreateSharingSchemaOf(dbA);

        var vendas = new VendaSpy();
        var controllerA = Controller(dbA, vendas.Servico);
        var controllerB = Controller(dbB, vendas.Servico);

        var pedido = new HomologarRequest { Mode = "pdv", PaymentMethod = PaymentMethod.Dinheiro };

        // Disparadas juntas de propósito: é o intervalo entre ler o status e
        // gravar o novo que a versão antiga deixava aberto.
        var resultados = await Task.WhenAll(
            controllerA.Homologar(reservaId, pedido),
            controllerB.Homologar(reservaId, pedido));

        // O que importa não é qual ganhou, é que só uma vendeu.
        vendas.Chamadas.Should().Be(1, "duas vendas debitariam o estoque em dobro");
        resultados.OfType<OkObjectResult>().Should().HaveCount(1);
        resultados.OfType<BadRequestObjectResult>().Should().HaveCount(1);

        var reserva = await dbA.ProductReservations.AsNoTracking().SingleAsync(r => r.Id == reservaId);
        reserva.Status.Should().Be("fulfilled");
        reserva.FulfilledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Homologar_QuandoAVendaFalha_DevolveAReservaParaAtiva()
    {
        await using var db = TestDbFactory.Create($"{nameof(ReservationConcurrencyTests)}_rollback");
        var reservaId = await SeedReservaAtivaAsync(db);

        // Sem a compensação, a reserva ficaria "fulfilled" sem venda por trás —
        // estoque preso numa reserva que ninguém mais consegue homologar.
        var vendas = new VendaSpy(new InvalidOperationException("Estoque insuficiente."));

        var resultado = await Controller(db, vendas.Servico)
            .Homologar(reservaId, new HomologarRequest { Mode = "pdv", PaymentMethod = PaymentMethod.Dinheiro });

        resultado.Should().BeOfType<BadRequestObjectResult>();

        var reserva = await db.ProductReservations.AsNoTracking().SingleAsync(r => r.Id == reservaId);
        reserva.Status.Should().Be("active");
        reserva.FulfilledAt.Should().BeNull();
    }

    [Fact]
    public async Task Homologar_ReservaJaAtendida_NaoVendeDeNovo()
    {
        await using var db = TestDbFactory.Create($"{nameof(ReservationConcurrencyTests)}_repetida");
        var reservaId = await SeedReservaAtivaAsync(db, status: "fulfilled");

        var vendas = new VendaSpy();
        var resultado = await Controller(db, vendas.Servico)
            .Homologar(reservaId, new HomologarRequest { Mode = "pdv", PaymentMethod = PaymentMethod.Dinheiro });

        resultado.Should().BeOfType<BadRequestObjectResult>();
        vendas.Chamadas.Should().Be(0);
    }

    // -------------------------------------------------------------------------

    private static async Task<Guid> SeedReservaAtivaAsync(AppDbContext db, string status = "active")
    {
        var user = new User { Name = "Cliente", Role = UserRole.Customer, IsActive = true };
        var product = new Product { Name = "Produto", Category = "Geral", PriceInCents = 1000, StockQuantity = 5 };
        var reserva = new ProductReservation
        {
            UserId = user.Id,
            ProductId = product.Id,
            Quantity = 1,
            Status = status,
            ExpiresAt = DateTime.UtcNow.AddDays(2),
        };

        db.AddRange(user, product, reserva);
        await db.SaveChangesAsync();

        // O contexto que semeou fica com as entidades rastreadas; o teste lê de
        // novo depois do ExecuteUpdateAsync, que não passa pelo change tracker.
        db.ChangeTracker.Clear();
        return reserva.Id;
    }

    private static ReservationController Controller(AppDbContext db, IVendaAvulsaService vendas)
    {
        var controller = new ReservationController(db, vendas, new Mock<IComandaService>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new Claim(ClaimTypes.Name, "Admin de teste"),
                    ], "test")),
                },
            },
        };
        return controller;
    }

    /// <summary>
    /// Conta as chamadas de <c>RegisterAsync</c> com <see cref="Interlocked"/>:
    /// no teste de corrida as duas acontecem em paralelo, e é exatamente esse
    /// contador que diz se a proteção funcionou.
    /// </summary>
    private sealed class VendaSpy
    {
        private int _chamadas;

        public int Chamadas => Volatile.Read(ref _chamadas);
        public IVendaAvulsaService Servico { get; }

        public VendaSpy(Exception? falha = null)
        {
            var mock = new Mock<IVendaAvulsaService>();
            var setup = mock
                .Setup(v => v.RegisterAsync(It.IsAny<VendaAvulsaRequest>(), It.IsAny<Guid>(), It.IsAny<string>()))
                .Callback(() => Interlocked.Increment(ref _chamadas));

            if (falha is not null) setup.ThrowsAsync(falha);
            else setup.ReturnsAsync(new VendaAvulsaDto());

            Servico = mock.Object;
        }
    }
}
