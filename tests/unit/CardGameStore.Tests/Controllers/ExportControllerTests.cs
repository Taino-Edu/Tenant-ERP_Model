// =============================================================================
// ExportControllerTests.cs — Testa a exportação self-service (produtos,
// clientes, crediário) contra Postgres real: confirma que o CSV sai com as
// linhas certas e que dado sensível de autenticação nunca vaza no export de
// clientes (hash de senha, tokens).
// =============================================================================

using System.Text;
using Xunit;
using CardGameStore.Controllers;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Moq;

namespace CardGameStore.Tests.Controllers;

public class ExportControllerTests
{
    private static AppDbContext CreateDb(string name) => TestDbFactory.Create(name);

    private static ExportController CreateController(AppDbContext db) =>
        new(db, new Mock<IAuditService>().Object);

    [Fact]
    public async Task ExportProdutos_IncluiAtivosEInativos()
    {
        var db = CreateDb(nameof(ExportProdutos_IncluiAtivosEInativos));
        db.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Ativo", Category = "Geral", PriceInCents = 1000, StockQuantity = 5, MinimumStock = 1, IsActive = true });
        db.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Inativo", Category = "Geral", PriceInCents = 2000, StockQuantity = 0, MinimumStock = 1, IsActive = false });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.ExportProdutos() as Microsoft.AspNetCore.Mvc.FileContentResult;

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("text/csv");
        var csv = Encoding.UTF8.GetString(result.FileContents, 3, result.FileContents.Length - 3);
        csv.Should().Contain("Ativo").And.Contain("Inativo");
    }

    [Fact]
    public async Task ExportClientes_NaoVazaHashDeSenhaNemTokens()
    {
        var db = CreateDb(nameof(ExportClientes_NaoVazaHashDeSenhaNemTokens));
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(), Name = "Cliente Teste", Email = "cliente@test.com",
            Role = UserRole.Customer,
            PasswordHash = "hash-secreto-que-nao-pode-vazar",
            RefreshToken = "refresh-secreto",
            PasswordResetToken = "reset-secreto",
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.ExportClientes() as Microsoft.AspNetCore.Mvc.FileContentResult;

        var csv = Encoding.UTF8.GetString(result!.FileContents, 3, result.FileContents.Length - 3);
        csv.Should().Contain("Cliente Teste");
        csv.Should().NotContain("hash-secreto-que-nao-pode-vazar");
        csv.Should().NotContain("refresh-secreto");
        csv.Should().NotContain("reset-secreto");
    }

    [Fact]
    public async Task ExportClientes_ExcluiStaffENaoSoClientes()
    {
        var db = CreateDb(nameof(ExportClientes_ExcluiStaffENaoSoClientes));
        db.Users.Add(new User { Id = Guid.NewGuid(), Name = "Cliente Real", Email = "c@test.com", Role = UserRole.Customer, PasswordHash = "h" });
        db.Users.Add(new User { Id = Guid.NewGuid(), Name = "Admin Interno", Email = "a@test.com", Role = UserRole.Admin, PasswordHash = "h" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.ExportClientes() as Microsoft.AspNetCore.Mvc.FileContentResult;

        var csv = Encoding.UTF8.GetString(result!.FileContents, 3, result.FileContents.Length - 3);
        csv.Should().Contain("Cliente Real");
        csv.Should().NotContain("Admin Interno");
    }

    [Fact]
    public async Task ExportCrediario_SoInclueEmAberto()
    {
        var db = CreateDb(nameof(ExportCrediario_SoInclueEmAberto));
        var user = new User { Id = Guid.NewGuid(), Name = "Devedor", Email = "d@test.com", Role = UserRole.Customer, PasswordHash = "h" };
        db.Users.Add(user);
        db.Crediarios.Add(new Crediario
        {
            Id = Guid.NewGuid(), UserId = user.Id, ValorEmCentavos = 5000,
            DataVencimento = DateTime.UtcNow.AddDays(10), Status = CrediariosStatus.Aberto,
            AbertoPorAdminId = Guid.NewGuid(),
        });
        db.Crediarios.Add(new Crediario
        {
            Id = Guid.NewGuid(), UserId = user.Id, ValorEmCentavos = 3000, ValorPagoEmCentavos = 3000,
            DataVencimento = DateTime.UtcNow.AddDays(-5), Status = CrediariosStatus.Pago,
            AbertoPorAdminId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.ExportCrediario() as Microsoft.AspNetCore.Mvc.FileContentResult;

        var csv = Encoding.UTF8.GetString(result!.FileContents, 3, result.FileContents.Length - 3);
        var linhas = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        linhas.Should().HaveCount(2); // cabeçalho + 1 crediário aberto (o pago fica de fora)
        csv.Should().Contain("50,00"); // valor do aberto
    }
}
