// =============================================================================
// ComandaServiceTests.cs — Testes unitários do ComandaService
// Chama o serviço real com InMemory database (sem mocks de lógica)
// Executar: dotnet test  (na pasta tests/unit/CardGameStore.Tests)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Services;

public class ComandaServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    private static ComandaService CreateService(AppDbContext db) =>
        new(db, NullLogger<ComandaService>.Instance);

    private static async Task<(User user, Product product, Comanda comanda)> SeedAsync(AppDbContext db)
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Cliente Teste",
            PasswordHash = "hash",
            Role         = UserRole.Customer,
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
            Status = ComandaStatus.Aberta,
        };
        db.Users.Add(user);
        db.Products.Add(product);
        db.Comandas.Add(comanda);
        await db.SaveChangesAsync();
        return (user, product, comanda);
    }

    // ── Abrir comanda ─────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenComanda_PrimeiraVez_DeveCriarNovaComanda()
    {
        var db      = CreateDb(nameof(OpenComanda_PrimeiraVez_DeveCriarNovaComanda));
        var service = CreateService(db);
        var user    = new User { Id = Guid.NewGuid(), Name = "Ana", PasswordHash = "h", Role = UserRole.Customer };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var comanda = await service.OpenComandaAsync(user.Id, "Mesa-01");

        comanda.Should().NotBeNull();
        comanda.TableIdentifier.Should().Be("Mesa-01");
        comanda.Status.Should().Be("Aberta");
    }

    [Fact]
    public async Task OpenComanda_JaExiste_DeveRetornarMesmaComanda()
    {
        var db      = CreateDb(nameof(OpenComanda_JaExiste_DeveRetornarMesmaComanda));
        var service = CreateService(db);
        var (user, _, _) = await SeedAsync(db);

        var primeira  = await service.OpenComandaAsync(user.Id);
        var segunda   = await service.OpenComandaAsync(user.Id);

        segunda.Id.Should().Be(primeira.Id, "deve reutilizar a comanda ativa, não criar duplicata");
    }

    // ── Adicionar item ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddItem_DeveDecrementarEstoqueEAtualizarTotal()
    {
        var db      = CreateDb(nameof(AddItem_DeveDecrementarEstoqueEAtualizarTotal));
        var service = CreateService(db);
        var (user, product, _) = await SeedAsync(db);

        var resultado = await service.AddItemAsync(user.Id, new AddItemToComandaRequest
        {
            ProductId = product.Id,
            Quantity  = 2,
        });

        resultado.Items.Should().ContainSingle();
        resultado.TotalInReais.Should().Be(10.00m); // 2 × R$ 5,00

        var estoqueAtual = (await db.Products.FindAsync(product.Id))!.StockQuantity;
        estoqueAtual.Should().Be(8); // 10 - 2
    }

    [Fact]
    public async Task AddItem_SemEstoque_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(AddItem_SemEstoque_DeveLancarExcecao));
        var service = CreateService(db);
        var (user, product, _) = await SeedAsync(db);

        product.StockQuantity = 0;
        await db.SaveChangesAsync();

        var act = async () => await service.AddItemAsync(user.Id, new AddItemToComandaRequest
        {
            ProductId = product.Id,
            Quantity  = 1,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Estoque insuficiente*");
    }

    [Fact]
    public async Task AddItem_ProdutoInativo_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(AddItem_ProdutoInativo_DeveLancarExcecao));
        var service = CreateService(db);
        var (user, product, _) = await SeedAsync(db);

        product.IsActive = false;
        await db.SaveChangesAsync();

        var act = async () => await service.AddItemAsync(user.Id, new AddItemToComandaRequest
        {
            ProductId = product.Id,
            Quantity  = 1,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inativo*");
    }

    [Fact]
    public async Task AddItem_DeveAlterarStatusParaEmAndamento()
    {
        var db      = CreateDb(nameof(AddItem_DeveAlterarStatusParaEmAndamento));
        var service = CreateService(db);
        var (user, product, _) = await SeedAsync(db);

        var resultado = await service.AddItemAsync(user.Id, new AddItemToComandaRequest
        {
            ProductId = product.Id,
            Quantity  = 1,
        });

        resultado.Status.Should().Be("EmAndamento");
    }

    // ── Remover item ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveItem_DeveRestaurarEstoque()
    {
        var db      = CreateDb(nameof(RemoveItem_DeveRestaurarEstoque));
        var service = CreateService(db);
        var (user, product, comanda) = await SeedAsync(db);

        var comandaComItem = await service.AddItemAsync(user.Id, new AddItemToComandaRequest
        {
            ProductId = product.Id,
            Quantity  = 3,
        });

        var itemId = comandaComItem.Items.First().Id;
        var resultado = await service.RemoveItemAsync(comanda.Id, itemId, user.Id);

        resultado.Items.Should().BeEmpty();
        var estoqueAtual = (await db.Products.FindAsync(product.Id))!.StockQuantity;
        estoqueAtual.Should().Be(10); // estoque restaurado
    }

    [Fact]
    public async Task RemoveItem_UltimoItem_DeveVoltarStatusParaAberta()
    {
        var db      = CreateDb(nameof(RemoveItem_UltimoItem_DeveVoltarStatusParaAberta));
        var service = CreateService(db);
        var (user, product, comanda) = await SeedAsync(db);

        var comandaComItem = await service.AddItemAsync(user.Id, new AddItemToComandaRequest
        {
            ProductId = product.Id,
            Quantity  = 1,
        });

        var itemId    = comandaComItem.Items.First().Id;
        var resultado = await service.RemoveItemAsync(comanda.Id, itemId, user.Id);

        resultado.Status.Should().Be("Aberta");
    }

    // ── Cancelar comanda ──────────────────────────────────────────────────────

    [Fact]
    public async Task CancelComanda_DeveRestaurarEstoqueDeTodosOsItens()
    {
        var db      = CreateDb(nameof(CancelComanda_DeveRestaurarEstoqueDeTodosOsItens));
        var service = CreateService(db);
        var (user, product, comanda) = await SeedAsync(db);

        await service.AddItemAsync(user.Id, new AddItemToComandaRequest { ProductId = product.Id, Quantity = 2 });
        await service.AddItemAsync(user.Id, new AddItemToComandaRequest { ProductId = product.Id, Quantity = 3 });

        var adminId = Guid.NewGuid();
        var resultado = await service.CancelComandaAsync(comanda.Id, adminId);

        resultado.Status.Should().Be("Cancelada");
        var estoqueAtual = (await db.Products.FindAsync(product.Id))!.StockQuantity;
        estoqueAtual.Should().Be(10); // 10 - 5 + 5 = 10 restaurado
    }

    // ── Aplicar pontos ────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyPoints_DeveReduzirSaldoERegistrarNaComanda()
    {
        var db      = CreateDb(nameof(ApplyPoints_DeveReduzirSaldoERegistrarNaComanda));
        var service = CreateService(db);
        var (user, product, comanda) = await SeedAsync(db);

        user.PointsBalance   = 100;
        user.PointsExpiresAt = DateTime.UtcNow.AddDays(20);
        await db.SaveChangesAsync();

        await service.AddItemAsync(user.Id, new AddItemToComandaRequest { ProductId = product.Id, Quantity = 4 });
        var resultado = await service.ApplyPointsAsync(comanda.Id, user.Id, 60);

        resultado.PointsApplied.Should().Be(60);
        var saldoAtual = (await db.Users.FindAsync(user.Id))!.PointsBalance;
        saldoAtual.Should().Be(40);
    }

    [Fact]
    public async Task ApplyPoints_ComPontosExpirados_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(ApplyPoints_ComPontosExpirados_DeveLancarExcecao));
        var service = CreateService(db);
        var (user, product, comanda) = await SeedAsync(db);

        user.PointsBalance   = 100;
        user.PointsExpiresAt = DateTime.UtcNow.AddDays(-1); // expirado
        await db.SaveChangesAsync();
        await service.AddItemAsync(user.Id, new AddItemToComandaRequest { ProductId = product.Id, Quantity = 1 });

        var act = async () => await service.ApplyPointsAsync(comanda.Id, user.Id, 50);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expirados*");
    }

    [Fact]
    public async Task ApplyPoints_SaldoInsuficiente_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(ApplyPoints_SaldoInsuficiente_DeveLancarExcecao));
        var service = CreateService(db);
        var (user, product, comanda) = await SeedAsync(db);

        user.PointsBalance   = 10;
        user.PointsExpiresAt = DateTime.UtcNow.AddDays(30);
        await db.SaveChangesAsync();
        await service.AddItemAsync(user.Id, new AddItemToComandaRequest { ProductId = product.Id, Quantity = 1 });

        var act = async () => await service.ApplyPointsAsync(comanda.Id, user.Id, 50);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Saldo insuficiente*");
    }

    [Fact]
    public async Task ApplyPoints_JaAplicado_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(ApplyPoints_JaAplicado_DeveLancarExcecao));
        var service = CreateService(db);
        var (user, product, comanda) = await SeedAsync(db);

        user.PointsBalance   = 100;
        user.PointsExpiresAt = DateTime.UtcNow.AddDays(30);
        await db.SaveChangesAsync();
        await service.AddItemAsync(user.Id, new AddItemToComandaRequest { ProductId = product.Id, Quantity = 4 });

        await service.ApplyPointsAsync(comanda.Id, user.Id, 30);
        var act = async () => await service.ApplyPointsAsync(comanda.Id, user.Id, 30);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já foram aplicados*");
    }

}
