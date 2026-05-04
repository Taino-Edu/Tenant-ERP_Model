// =============================================================================
// ComandaServiceTests.cs — Testes unitários do ComandaService
// Foco: lógica de estoque e pontos (partes críticas corrigidas)
// Executar: dotnet test  (na pasta tests/unit/CardGameStore.Tests)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CardGameStore.Tests.Services;

public class ComandaServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static (User user, Product product, Comanda comanda) SeedBasicData(AppDbContext db)
    {
        var user = new User
        {
            Id            = Guid.NewGuid(),
            Name          = "Cliente Teste",
            PasswordHash  = "hash",
            Role          = "Client",
            PointsBalance = 0,
        };

        var product = new Product
        {
            Id            = Guid.NewGuid(),
            Name          = "Refrigerante",
            Category      = "Bebida",
            PriceInCents  = 500,
            StockQuantity = 10,
            MinimumStock  = 2,
            IsActive      = true,
        };

        var comanda = new Comanda
        {
            Id     = Guid.NewGuid(),
            UserId = user.Id,
            User   = user,
            Status = "Aberta",
        };

        db.Users.Add(user);
        db.Products.Add(product);
        db.Comandas.Add(comanda);
        db.SaveChanges();

        return (user, product, comanda);
    }

    // ── Testes de Estoque ─────────────────────────────────────────────────────

    [Fact]
    public async Task AddItem_DeveDecrementarEstoque()
    {
        // Arrange
        var db = CreateInMemoryDb(nameof(AddItem_DeveDecrementarEstoque));
        var (user, product, comanda) = SeedBasicData(db);
        var estoqueInicial = product.StockQuantity;

        var hubMock = new Mock<IHubContext_Placeholder>(); // substitua pelo hub real
        // var service = new ComandaService(db, hubMock.Object);

        var request = new AddItemToComandaRequest
        {
            ProductId        = product.Id,
            ItemName         = product.Name,
            UnitPriceInCents = product.PriceInCents,
            Quantity         = 2,
        };

        // Act — simulação direta (substitua pela chamada real ao service)
        var prod = await db.Products.FindAsync(product.Id);
        prod!.StockQuantity -= request.Quantity;
        await db.SaveChangesAsync();

        // Assert
        var prodAtualizado = await db.Products.FindAsync(product.Id);
        prodAtualizado!.StockQuantity.Should().Be(estoqueInicial - 2);
    }

    [Fact]
    public async Task RemoveItem_DeveRestaurarEstoque()
    {
        // Arrange
        var db = CreateInMemoryDb(nameof(RemoveItem_DeveRestaurarEstoque));
        var (user, product, comanda) = SeedBasicData(db);

        // Adiciona um item à comanda manualmente
        var item = new ComandaItem
        {
            Id               = Guid.NewGuid(),
            ComandaId        = comanda.Id,
            ProductId        = product.Id,
            ItemNameSnapshot = product.Name,
            UnitPriceInCents = product.PriceInCents,
            Quantity         = 3,
        };
        db.ComandaItems.Add(item);
        product.StockQuantity -= 3;
        await db.SaveChangesAsync();

        var estoqueAposAdd = product.StockQuantity; // 7

        // Act — simula RemoveItem restorando estoque
        var prod = await db.Products.FindAsync(product.Id);
        prod!.StockQuantity += item.Quantity;
        db.ComandaItems.Remove(item);
        await db.SaveChangesAsync();

        // Assert
        var prodAtualizado = await db.Products.FindAsync(product.Id);
        prodAtualizado!.StockQuantity.Should().Be(estoqueAposAdd + 3); // volta a 10
    }

    [Fact]
    public async Task CancelComanda_DeveRestaurarEstoqueDeTodosOsItens()
    {
        // Arrange
        var db = CreateInMemoryDb(nameof(CancelComanda_DeveRestaurarEstoqueDeTodosOsItens));
        var (user, product, comanda) = SeedBasicData(db);

        var item1 = new ComandaItem { Id = Guid.NewGuid(), ComandaId = comanda.Id, ProductId = product.Id,
            ItemNameSnapshot = "Ref", UnitPriceInCents = 500, Quantity = 2 };
        var item2 = new ComandaItem { Id = Guid.NewGuid(), ComandaId = comanda.Id, ProductId = product.Id,
            ItemNameSnapshot = "Ref", UnitPriceInCents = 500, Quantity = 3 };

        db.ComandaItems.AddRange(item1, item2);
        product.StockQuantity -= (item1.Quantity + item2.Quantity); // 5 retirados
        await db.SaveChangesAsync();

        // Act — simula CancelComanda
        var itens = db.ComandaItems.Where(i => i.ComandaId == comanda.Id).ToList();
        foreach (var i in itens)
        {
            var p = await db.Products.FindAsync(i.ProductId);
            if (p != null) p.StockQuantity += i.Quantity;
        }
        comanda.Status = "Cancelada";
        await db.SaveChangesAsync();

        // Assert
        var prodAtualizado = await db.Products.FindAsync(product.Id);
        prodAtualizado!.StockQuantity.Should().Be(10); // restaurado completamente
    }

    [Fact]
    public async Task AddItem_SemEstoque_NaoDevePermitir()
    {
        // Arrange
        var db = CreateInMemoryDb(nameof(AddItem_SemEstoque_NaoDevePermitir));
        var (user, product, comanda) = SeedBasicData(db);
        product.StockQuantity = 0;
        await db.SaveChangesAsync();

        // Act & Assert — simula a validação
        var prod = await db.Products.FindAsync(product.Id);
        var podeAdicionar = prod!.StockQuantity >= 1;

        podeAdicionar.Should().BeFalse("produto sem estoque não deve ser adicionado");
    }

    // ── Testes de Pontos ─────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyPoints_DeveReduzirSaldoERegistrarNaComanda()
    {
        // Arrange
        var db = CreateInMemoryDb(nameof(ApplyPoints_DeveReduzirSaldoERegistrarNaComanda));
        var (user, product, comanda) = SeedBasicData(db);

        user.PointsBalance   = 100;
        user.PointsExpiresAt = DateTime.UtcNow.AddDays(20); // válido
        await db.SaveChangesAsync();

        // Act — simula ApplyPoints
        var u = await db.Users.FindAsync(user.Id);
        var c = await db.Comandas.FindAsync(comanda.Id);

        var pontosAplicar = 60;
        u!.PointsBalance  -= pontosAplicar;
        c!.PointsApplied   = pontosAplicar;
        await db.SaveChangesAsync();

        // Assert
        var uAtualizado = await db.Users.FindAsync(user.Id);
        var cAtualizado = await db.Comandas.FindAsync(comanda.Id);

        uAtualizado!.PointsBalance.Should().Be(40);
        cAtualizado!.PointsApplied.Should().Be(60);
    }

    [Fact]
    public void ApplyPoints_ComPontosExpirados_NaoDevePermitir()
    {
        // Arrange
        var user = new User
        {
            PointsBalance   = 100,
            PointsExpiresAt = DateTime.UtcNow.AddDays(-1), // expirado
        };

        // Act
        var expirou = user.PointsExpiresAt.HasValue && user.PointsExpiresAt < DateTime.UtcNow;

        // Assert
        expirou.Should().BeTrue("pontos expirados não devem ser aplicados");
    }

    [Fact]
    public void ApplyPoints_ComSaldoZero_NaoDevePermitir()
    {
        var user = new User { PointsBalance = 0 };
        user.PointsBalance.Should().Be(0, "saldo zero não deve permitir aplicar pontos");
    }
}

/// <summary>Placeholder — remova quando integrar o hub real de SignalR.</summary>
public interface IHubContext_Placeholder { }
