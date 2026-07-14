// =============================================================================
// CrediariosControllerTests.cs — Testes do endpoint de pagamento de crediário,
// com foco na idempotência (retry/duplo clique não pode debitar duas vezes).
// Chama o controller real contra Postgres de teste (TestDbFactory).
// =============================================================================

using System.Security.Claims;
using Xunit;
using CardGameStore.Controllers;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CardGameStore.Tests.Controllers;

public class CrediariosControllerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb(string name) => TestDbFactory.Create(name);

    private static CrediariosController CreateController(AppDbContext db, Guid adminId)
    {
        var emailMock = new Mock<IEmailService>();

        // InterSyncService só é usado pelos endpoints de Pix — RegistrarPagamento
        // nunca o toca, então null! evita montar a cadeia de dependências dele.
        var controller = new CrediariosController(
            db, emailMock.Object, null!, NullLogger<CrediariosController>.Instance);

        var claims = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", adminId.ToString())], "test"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claims },
        };
        return controller;
    }

    private static async Task<(Crediario crediario, Guid adminId)> SeedAsync(AppDbContext db)
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Cliente Teste",
            Email        = "cliente@test.com",
            PasswordHash = "hash",
            Role         = UserRole.Customer,
        };
        var adminId   = Guid.NewGuid();
        var crediario = new Crediario
        {
            Id               = Guid.NewGuid(),
            UserId           = user.Id,
            User             = user,
            ValorEmCentavos  = 10000, // R$ 100,00
            DataVencimento   = DateTime.UtcNow.AddDays(30),
            AbertoPorAdminId = adminId,
        };

        db.Users.Add(user);
        db.Crediarios.Add(crediario);
        await db.SaveChangesAsync();

        return (crediario, adminId);
    }

    // ── Idempotência ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RegistrarPagamento_MesmaChaveIdempotencia_NaoDebitaDuasVezes()
    {
        var db = CreateDb(nameof(RegistrarPagamento_MesmaChaveIdempotencia_NaoDebitaDuasVezes));
        var (crediario, adminId) = await SeedAsync(db);
        var controller = CreateController(db, adminId);

        var request = new RegistrarPagamentoRequest
        {
            ValorEmCentavos = 5000,
            FormaPagamento  = PaymentMethod.Pix,
            IdempotencyKey  = Guid.NewGuid(),
        };

        // 1ª chamada — registra normalmente
        var r1 = await controller.RegistrarPagamento(crediario.Id, request);
        r1.Result.Should().BeOfType<OkObjectResult>();

        // 2ª chamada idêntica (retry) — 200, mas sem debitar de novo
        var r2 = await controller.RegistrarPagamento(crediario.Id, request);
        r2.Result.Should().BeOfType<OkObjectResult>(
            "retry com a mesma chave deve devolver o estado atual, não um erro");

        var atual = await db.Crediarios.Include(c => c.Pagamentos)
            .FirstAsync(c => c.Id == crediario.Id);
        atual.ValorPagoEmCentavos.Should().Be(5000, "o retry não pode debitar de novo");
        atual.Pagamentos.Should().HaveCount(1);
    }

    [Fact]
    public async Task RegistrarPagamento_RetryAposQuitacao_Retorna200ENaoDebita()
    {
        var db = CreateDb(nameof(RegistrarPagamento_RetryAposQuitacao_Retorna200ENaoDebita));
        var (crediario, adminId) = await SeedAsync(db);
        var controller = CreateController(db, adminId);

        var request = new RegistrarPagamentoRequest
        {
            ValorEmCentavos = 10000, // quita o crediário inteiro
            FormaPagamento  = PaymentMethod.Dinheiro,
            IdempotencyKey  = Guid.NewGuid(),
        };

        var r1 = await controller.RegistrarPagamento(crediario.Id, request);
        r1.Result.Should().BeOfType<OkObjectResult>();

        // Retry do pagamento que quitou: deve ser 200 (idempotente), não o 400
        // de "crediário já está quitado".
        var r2 = await controller.RegistrarPagamento(crediario.Id, request);
        r2.Result.Should().BeOfType<OkObjectResult>(
            "retry da quitação deve ser tratado como replay, não como novo pagamento inválido");

        var atual = await db.Crediarios.Include(c => c.Pagamentos)
            .FirstAsync(c => c.Id == crediario.Id);
        atual.Status.Should().Be(CrediariosStatus.Pago);
        atual.ValorPagoEmCentavos.Should().Be(10000);
        atual.Pagamentos.Should().HaveCount(1);
    }

    [Fact]
    public async Task RegistrarPagamento_SemChave_RegistraCadaChamada()
    {
        var db = CreateDb(nameof(RegistrarPagamento_SemChave_RegistraCadaChamada));
        var (crediario, adminId) = await SeedAsync(db);
        var controller = CreateController(db, adminId);

        // Sem chave (clientes antigos / fluxos internos): comportamento original,
        // cada chamada registra um pagamento novo.
        var r1 = await controller.RegistrarPagamento(crediario.Id, new RegistrarPagamentoRequest
        {
            ValorEmCentavos = 3000,
            FormaPagamento  = PaymentMethod.Dinheiro,
        });
        var r2 = await controller.RegistrarPagamento(crediario.Id, new RegistrarPagamentoRequest
        {
            ValorEmCentavos = 3000,
            FormaPagamento  = PaymentMethod.Dinheiro,
        });

        r1.Result.Should().BeOfType<OkObjectResult>();
        r2.Result.Should().BeOfType<OkObjectResult>();

        var atual = await db.Crediarios.Include(c => c.Pagamentos)
            .FirstAsync(c => c.Id == crediario.Id);
        atual.ValorPagoEmCentavos.Should().Be(6000);
        atual.Pagamentos.Should().HaveCount(2);
    }
}
