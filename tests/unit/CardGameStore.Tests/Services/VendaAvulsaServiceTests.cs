// =============================================================================
// VendaAvulsaServiceTests.cs — Testes unitários do VendaAvulsaService
// Vendas avulsas vivem 100% no PostgreSQL (MongoDB foi removido do sistema) —
// SQLite in-memory é usado por suportar ExecuteUpdateAsync (bulk update), que o
// EF InMemory provider não implementa.
// Executar: dotnet test  (na pasta tests/unit/CardGameStore.Tests)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CardGameStore.Tests.Services;

public class VendaAvulsaServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb(string name) => TestDbFactory.Create(name);

    private static VendaAvulsaService CreateService(AppDbContext db) =>
        new(db, NullLogger<VendaAvulsaService>.Instance, new Mock<IServiceScopeFactory>().Object,
            new Mock<ITenantContext>().Object);

    private static async Task<Product> SeedProductAsync(AppDbContext db,
        string name = "Produto Teste", int priceInCents = 1500, int stock = 10, bool isActive = true)
    {
        var product = new Product
        {
            Id            = Guid.NewGuid(),
            Name          = name,
            Category      = "Geral",
            PriceInCents  = priceInCents,
            StockQuantity = stock,
            MinimumStock  = 2,
            IsActive      = isActive,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static async Task<User> SeedUserAsync(AppDbContext db,
        string name = "Cliente Teste", int pointsBalance = 0, int balanceInCents = 0, DateTime? pointsExpiresAt = null)
    {
        var user = new User
        {
            Id              = Guid.NewGuid(),
            Name            = name,
            PasswordHash    = "hash",
            Role            = UserRole.Customer,
            PointsBalance   = pointsBalance,
            BalanceInCents  = balanceInCents,
            PointsExpiresAt = pointsExpiresAt,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Seta explicitamente o toggle de pontos (SiteConfig é singleton — uma
    /// linha só). Sem chamar isso, o serviço trata "sem linha" como ativo (default true).</summary>
    private static async Task SetPontosAtivoAsync(AppDbContext db, bool ativo)
    {
        db.SiteConfigs.Add(new SiteConfig { PontosFidelidadeAtivo = ativo });
        await db.SaveChangesAsync();
    }

    private static readonly Guid AdminId   = Guid.NewGuid();
    private const string AdminName = "Admin Teste";

    // ── Registro bem-sucedido ─────────────────────────────────────────────────

    [Fact]
    public async Task Register_Sucesso_DeveDecrementarEstoqueERetornarDto()
    {
        var db = CreateDb(nameof(Register_Sucesso_DeveDecrementarEstoqueERetornarDto));
        var product = await SeedProductAsync(db, priceInCents: 2000, stock: 5);
        var service  = CreateService(db);
        var request  = new VendaAvulsaRequest
        {
            PaymentMethod = PaymentMethod.Pix,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 2 }],
        };

        var resultado = await service.RegisterAsync(request, AdminId, AdminName);

        resultado.TotalInReais.Should().Be(40.00m); // 2 × R$ 20,00
        resultado.PaymentMethod.Should().Be(PaymentMethod.Pix);
        resultado.Items.Should().ContainSingle();
        resultado.Items[0].Quantity.Should().Be(2);
        resultado.Items[0].SubtotalInReais.Should().Be(40.00m);

        db.ChangeTracker.Clear();
        var estoque = (await db.Products.FindAsync(product.Id))!.StockQuantity;
        estoque.Should().Be(3); // 5 - 2
    }

    [Fact]
    public async Task Register_ComClientName_DevePreservarNomeNoDto()
    {
        var db = CreateDb(nameof(Register_ComClientName_DevePreservarNomeNoDto));
        var product = await SeedProductAsync(db);
        var service   = CreateService(db);
        var resultado = await service.RegisterAsync(new VendaAvulsaRequest
        {
            ClientName    = "  João Silva  ",
            PaymentMethod = PaymentMethod.Dinheiro,
            Items         = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 1 }],
        }, AdminId, AdminName);

        resultado.ClientName.Should().Be("João Silva"); // trimado
    }

    [Fact]
    public async Task Register_SemClientName_DeveSetarNuloNoDto()
    {
        var db = CreateDb(nameof(Register_SemClientName_DeveSetarNuloNoDto));
        var product = await SeedProductAsync(db);
        var service   = CreateService(db);
        var resultado = await service.RegisterAsync(new VendaAvulsaRequest
        {
            ClientName    = null,
            PaymentMethod = PaymentMethod.CartaoCredito,
            Items         = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 1 }],
        }, AdminId, AdminName);

        resultado.ClientName.Should().BeNull();
    }

    [Fact]
    public async Task Register_MultiplosProdutos_DeveSomarTotalCorretamente()
    {
        var db = CreateDb(nameof(Register_MultiplosProdutos_DeveSomarTotalCorretamente));
        var p1 = await SeedProductAsync(db, "Produto A", priceInCents: 1000, stock: 5);
        var p2 = await SeedProductAsync(db, "Produto B", priceInCents: 500,  stock: 5);
        var service   = CreateService(db);
        var resultado = await service.RegisterAsync(new VendaAvulsaRequest
        {
            PaymentMethod = PaymentMethod.CartaoDebito,
            Items =
            [
                new VendaAvulsaItemRequest { ProductId = p1.Id, Quantity = 3 },
                new VendaAvulsaItemRequest { ProductId = p2.Id, Quantity = 2 },
            ],
        }, AdminId, AdminName);

        resultado.TotalInReais.Should().Be(40.00m); // 3×10 + 2×5
        resultado.Items.Should().HaveCount(2);

        db.ChangeTracker.Clear();
        (await db.Products.FindAsync(p1.Id))!.StockQuantity.Should().Be(2);
        (await db.Products.FindAsync(p2.Id))!.StockQuantity.Should().Be(3);
    }

    [Fact]
    public async Task Register_DevePersistirVendaNoPostgres()
    {
        var db = CreateDb(nameof(Register_DevePersistirVendaNoPostgres));
        var product = await SeedProductAsync(db);
        var service = CreateService(db);

        var resultado = await service.RegisterAsync(new VendaAvulsaRequest
        {
            PaymentMethod = PaymentMethod.Pix,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 1 }],
        }, AdminId, AdminName);

        db.ChangeTracker.Clear();
        // Items é serializado como JSONB (não é uma navegação de FK de verdade) —
        // vem junto na query normal, sem precisar de Include.
        var venda = await db.VendasAvulsas.FirstOrDefaultAsync(v => v.Id == resultado.Id);
        venda.Should().NotBeNull();
        venda!.SoldByAdminId.Should().Be(AdminId);
        venda.SoldByAdminName.Should().Be(AdminName);
        venda.PaymentMethod.Should().Be(PaymentMethod.Pix);
        venda.Items.Should().ContainSingle();
    }

    // ── Validações ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ProdutoNaoEncontrado_DeveLancarExcecao()
    {
        var db = CreateDb(nameof(Register_ProdutoNaoEncontrado_DeveLancarExcecao));
        var service = CreateService(db);

        var act = async () => await service.RegisterAsync(new VendaAvulsaRequest
        {
            PaymentMethod = PaymentMethod.Pix,
            Items = [new VendaAvulsaItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }],
        }, AdminId, AdminName);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado ou inativo*");
    }

    [Fact]
    public async Task Register_ProdutoInativo_DeveLancarExcecao()
    {
        var db = CreateDb(nameof(Register_ProdutoInativo_DeveLancarExcecao));
        var product = await SeedProductAsync(db, isActive: false);
        var service = CreateService(db);

        var act = async () => await service.RegisterAsync(new VendaAvulsaRequest
        {
            PaymentMethod = PaymentMethod.Pix,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 1 }],
        }, AdminId, AdminName);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado ou inativo*");
    }

    [Fact]
    public async Task Register_EstoqueInsuficiente_DeveLancarExcecao()
    {
        var db = CreateDb(nameof(Register_EstoqueInsuficiente_DeveLancarExcecao));
        var product = await SeedProductAsync(db, stock: 2);
        var service = CreateService(db);

        var act = async () => await service.RegisterAsync(new VendaAvulsaRequest
        {
            PaymentMethod = PaymentMethod.Pix,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 5 }],
        }, AdminId, AdminName);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Estoque insuficiente*");
    }

    [Fact]
    public async Task Register_EstoqueInsuficiente_NaoDeveAlterarEstoque()
    {
        var db = CreateDb(nameof(Register_EstoqueInsuficiente_NaoDeveAlterarEstoque));
        var product = await SeedProductAsync(db, stock: 3);
        var service = CreateService(db);

        try
        {
            await service.RegisterAsync(new VendaAvulsaRequest
            {
                PaymentMethod = PaymentMethod.Pix,
                Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 10 }],
            }, AdminId, AdminName);
        }
        catch (InvalidOperationException) { }

        db.ChangeTracker.Clear();
        var estoque = (await db.Products.FindAsync(product.Id))!.StockQuantity;
        estoque.Should().Be(3);
    }

    // ── Desconto ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ComDesconto10Porcento_DeveCalcularTotalComDesconto()
    {
        var db = CreateDb(nameof(Register_ComDesconto10Porcento_DeveCalcularTotalComDesconto));
        var product = await SeedProductAsync(db, priceInCents: 2000, stock: 5);
        var service = CreateService(db);

        var result = await service.RegisterAsync(new VendaAvulsaRequest
        {
            PaymentMethod   = PaymentMethod.Pix,
            DiscountPercent = 10,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 1 }],
        }, AdminId, AdminName);

        result.TotalInReais.Should().Be(18.00m,    "10% de desconto sobre R$20,00");
        result.DiscountPercent.Should().Be(10);
        result.DiscountInReais.Should().Be(2.00m,  "desconto de 10% sobre R$20,00 = R$2,00");
    }

    [Fact]
    public async Task Register_SemDesconto_TotalDeveSerioBruto()
    {
        var db = CreateDb(nameof(Register_SemDesconto_TotalDeveSerioBruto));
        var product = await SeedProductAsync(db, priceInCents: 1500, stock: 3);
        var service = CreateService(db);

        var result = await service.RegisterAsync(new VendaAvulsaRequest
        {
            PaymentMethod   = PaymentMethod.Dinheiro,
            DiscountPercent = 0,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 2 }],
        }, AdminId, AdminName);

        result.TotalInReais.Should().Be(30.00m);  // 2 × R$15,00
        result.DiscountInReais.Should().Be(0.00m);
    }

    // ── Programa de pontos — opcional por loja (SiteConfig.PontosFidelidadeAtivo) ──

    [Fact]
    public async Task Register_SemConfigDePontos_DeveGanharPontosPorDefault()
    {
        // Sem linha de SiteConfig nenhuma — o serviço trata como ativo (default true),
        // mesmo comportamento de sempre pra loja que nunca mexeu na configuração.
        var db = CreateDb(nameof(Register_SemConfigDePontos_DeveGanharPontosPorDefault));
        var product = await SeedProductAsync(db, priceInCents: 5000, stock: 5);
        var user    = await SeedUserAsync(db);
        var service = CreateService(db);

        await service.RegisterAsync(new VendaAvulsaRequest
        {
            UserId        = user.Id,
            PaymentMethod = PaymentMethod.Pix,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 1 }],
        }, AdminId, AdminName);

        db.ChangeTracker.Clear();
        (await db.Users.FindAsync(user.Id))!.PointsBalance.Should().Be(50); // R$50 → 50 pts
    }

    [Fact]
    public async Task Register_ComPontosDesativados_NaoDeveGanharPontos()
    {
        var db = CreateDb(nameof(Register_ComPontosDesativados_NaoDeveGanharPontos));
        await SetPontosAtivoAsync(db, ativo: false);
        var product = await SeedProductAsync(db, priceInCents: 5000, stock: 5);
        var user    = await SeedUserAsync(db);
        var service = CreateService(db);

        await service.RegisterAsync(new VendaAvulsaRequest
        {
            UserId        = user.Id,
            PaymentMethod = PaymentMethod.Pix,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 1 }],
        }, AdminId, AdminName);

        db.ChangeTracker.Clear();
        (await db.Users.FindAsync(user.Id))!.PointsBalance.Should().Be(0, "programa de pontos desligado — não deve acumular");
    }

    [Fact]
    public async Task Register_ComPontosAtivados_DeveGanharPontos()
    {
        var db = CreateDb(nameof(Register_ComPontosAtivados_DeveGanharPontos));
        await SetPontosAtivoAsync(db, ativo: true);
        var product = await SeedProductAsync(db, priceInCents: 3000, stock: 5);
        var user    = await SeedUserAsync(db);
        var service = CreateService(db);

        await service.RegisterAsync(new VendaAvulsaRequest
        {
            UserId        = user.Id,
            PaymentMethod = PaymentMethod.Pix,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 1 }],
        }, AdminId, AdminName);

        db.ChangeTracker.Clear();
        (await db.Users.FindAsync(user.Id))!.PointsBalance.Should().Be(30); // R$30 → 30 pts
    }

    [Fact]
    public async Task Register_PagarComPontosDesativados_DeveLancarExcecao()
    {
        var db = CreateDb(nameof(Register_PagarComPontosDesativados_DeveLancarExcecao));
        await SetPontosAtivoAsync(db, ativo: false);
        var product = await SeedProductAsync(db, priceInCents: 1000, stock: 5);
        var user    = await SeedUserAsync(db, pointsBalance: 100);
        var service = CreateService(db);

        var act = async () => await service.RegisterAsync(new VendaAvulsaRequest
        {
            UserId        = user.Id,
            PaymentMethod = PaymentMethod.Pontos,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 1 }],
        }, AdminId, AdminName);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*programa de pontos está desativado*");
    }

    [Fact]
    public async Task Register_SegundoPagamentoComPontosDesativados_DeveLancarExcecao()
    {
        var db = CreateDb(nameof(Register_SegundoPagamentoComPontosDesativados_DeveLancarExcecao));
        await SetPontosAtivoAsync(db, ativo: false);
        var product = await SeedProductAsync(db, priceInCents: 2000, stock: 5);
        var user    = await SeedUserAsync(db, pointsBalance: 100);
        var service = CreateService(db);

        var act = async () => await service.RegisterAsync(new VendaAvulsaRequest
        {
            UserId                     = user.Id,
            PaymentMethod              = PaymentMethod.Pix,
            SecondPaymentMethod        = PaymentMethod.Pontos,
            SecondPaymentAmountInCents = 500,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 1 }],
        }, AdminId, AdminName);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*programa de pontos está desativado*");
    }

    [Fact]
    public async Task Register_PagarComPontosAtivados_DeveDebitarSaldo()
    {
        var db = CreateDb(nameof(Register_PagarComPontosAtivados_DeveDebitarSaldo));
        await SetPontosAtivoAsync(db, ativo: true);
        var product = await SeedProductAsync(db, priceInCents: 1000, stock: 5);
        // Nota: o resgate debita o valor em CENTAVOS do saldo de pontos (não o
        // equivalente em "pontos" da taxa de ganho de 1pt/R$1) — comportamento
        // real já existente no serviço, não introduzido por este toggle.
        var user    = await SeedUserAsync(db, pointsBalance: 2000);
        var service = CreateService(db);

        await service.RegisterAsync(new VendaAvulsaRequest
        {
            UserId        = user.Id,
            PaymentMethod = PaymentMethod.Pontos,
            Items = [new VendaAvulsaItemRequest { ProductId = product.Id, Quantity = 1 }],
        }, AdminId, AdminName);

        db.ChangeTracker.Clear();
        (await db.Users.FindAsync(user.Id))!.PointsBalance.Should().Be(1000); // 2000 - 1000 (débito em centavos)
    }

    // ── GetByDate ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByDate_DeveRetornarVendasMapeadasComTotalCorreto()
    {
        var db = CreateDb(nameof(GetByDate_DeveRetornarVendasMapeadasComTotalCorreto));
        var targetDate = DateTime.UtcNow.Date;
        db.VendasAvulsas.Add(new VendaAvulsa
        {
            PaymentMethod   = PaymentMethod.Pix,
            ClientName      = "Comprador Dia",
            TotalInCents    = 5000,
            DiscountInCents = 500,
            DiscountPercent = 10,
            SoldAt          = targetDate.AddHours(14), // 14h UTC do dia alvo
            SoldByAdminId   = AdminId,
            SoldByAdminName = AdminName,
            Items =
            [
                new VendaAvulsaItem
                {
                    ProductId = Guid.NewGuid(), ProductName = "Produto",
                    Quantity = 1, UnitPriceInCents = 5500, SubtotalInCents = 5500,
                }
            ],
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = (await service.GetByDateAsync(targetDate)).ToList();

        result.Should().ContainSingle();
        result[0].TotalInReais.Should().Be(50.00m);         // 5000 / 100
        result[0].DiscountInReais.Should().Be(5.00m);       // 500 / 100
        result[0].ClientName.Should().Be("Comprador Dia");
        result[0].PaymentMethod.Should().Be(PaymentMethod.Pix);
    }

    [Fact]
    public async Task GetByDate_SemVendas_DeveRetornarListaVazia()
    {
        var db = CreateDb(nameof(GetByDate_SemVendas_DeveRetornarListaVazia));
        var service = CreateService(db);

        var result = (await service.GetByDateAsync(DateTime.UtcNow.Date)).ToList();

        result.Should().BeEmpty("sem vendas no período, lista deve ser vazia");
    }

    // ── GetRecent ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecent_DeveRetornarVendasMapeadasDoPostgres()
    {
        var db = CreateDb(nameof(GetRecent_DeveRetornarVendasMapeadasDoPostgres));
        db.VendasAvulsas.Add(new VendaAvulsa
        {
            PaymentMethod   = PaymentMethod.Dinheiro,
            ClientName      = "Carlos",
            TotalInCents    = 3000,
            SoldByAdminId   = AdminId,
            SoldByAdminName = AdminName,
            SoldAt          = DateTime.UtcNow,
            Items =
            [
                new VendaAvulsaItem
                {
                    ProductId = Guid.NewGuid(), ProductName = "Kit",
                    Quantity = 1, UnitPriceInCents = 3000, SubtotalInCents = 3000,
                }
            ],
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var resultado = (await service.GetRecentAsync(10)).ToList();

        resultado.Should().ContainSingle();
        resultado[0].ClientName.Should().Be("Carlos");
        resultado[0].TotalInReais.Should().Be(30.00m);
        resultado[0].PaymentMethod.Should().Be(PaymentMethod.Dinheiro);
    }
}
