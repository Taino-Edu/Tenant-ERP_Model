// =============================================================================
// ProductServiceTests.cs — Testes unitários do ProductService
// Executar: dotnet test  (na pasta tests/unit/CardGameStore.Tests)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CardGameStore.Tests.Services;

public class ProductServiceTests
{
    // TestDbFactory: Postgres real (ExecuteUpdateAsync de AdjustStockAsync não
    // roda no EF InMemory).
    private static AppDbContext CreateDb(string name) => TestDbFactory.Create(name);

    private static ProductService CreateService(AppDbContext db) => new(
        db, new Mock<IPushService>().Object, new Mock<IEmailService>().Object, NullLogger<ProductService>.Instance);

    private static Product MakeProduct(string name = "Card Rare", int stock = 10, int min = 2, bool active = true) =>
        new()
        {
            Id            = Guid.NewGuid(),
            Name          = name,
            Category      = "MTG",
            PriceInCents  = 2500,
            StockQuantity = stock,
            MinimumStock  = min,
            IsActive      = active,
        };

    // ── Listagem ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllActive_DeveRetornarApenasAtivos()
    {
        var db      = CreateDb(nameof(GetAllActive_DeveRetornarApenasAtivos));
        var service = CreateService(db);

        db.Products.AddRange(MakeProduct("Ativo"), MakeProduct("Inativo", active: false));
        await db.SaveChangesAsync();

        var result = await service.GetAllActiveAsync();

        result.Should().ContainSingle()
              .Which.Name.Should().Be("Ativo");
    }

    [Fact]
    public async Task GetAllActive_DeveRetornarOrdenadoPorNome()
    {
        var db      = CreateDb(nameof(GetAllActive_DeveRetornarOrdenadoPorNome));
        var service = CreateService(db);

        db.Products.AddRange(MakeProduct("Zebra"), MakeProduct("Alpha"), MakeProduct("Beta"));
        await db.SaveChangesAsync();

        var result = (await service.GetAllActiveAsync()).ToList();

        result[0].Name.Should().Be("Alpha");
        result[1].Name.Should().Be("Beta");
        result[2].Name.Should().Be("Zebra");
    }

    [Fact]
    public async Task GetByCategory_DeveRetornarSomenterCategoriaFiltrada()
    {
        var db      = CreateDb(nameof(GetByCategory_DeveRetornarSomenterCategoriaFiltrada));
        var service = CreateService(db);

        var mtg     = MakeProduct("MTG Card");
        var pokemon = new Product { Id = Guid.NewGuid(), Name = "Pika", Category = "Pokemon",
                                    PriceInCents = 100, StockQuantity = 5, IsActive = true };
        db.Products.AddRange(mtg, pokemon);
        await db.SaveChangesAsync();

        var result = await service.GetByCategoryAsync("MTG");

        result.Should().ContainSingle()
              .Which.Category.Should().Be("MTG");
    }

    // ── Estoque baixo ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLowStock_DeveRetornarProdutosComEstoqueIgualOuMenorQueMinimo()
    {
        var db      = CreateDb(nameof(GetLowStock_DeveRetornarProdutosComEstoqueIgualOuMenorQueMinimo));
        var service = CreateService(db);

        db.Products.AddRange(
            MakeProduct("OK",   stock: 10, min: 2),  // OK
            MakeProduct("Low",  stock: 2,  min: 2),  // igual = alerta
            MakeProduct("Zero", stock: 0,  min: 2)   // abaixo = alerta
        );
        await db.SaveChangesAsync();

        var result = await service.GetLowStockAsync();

        result.Should().HaveCount(2);
        result.Select(p => p.Name).Should().Contain(new[] { "Low", "Zero" });
    }

    // ── Ajuste de estoque ─────────────────────────────────────────────────────

    [Fact]
    public async Task AdjustStock_AdicaoPositiva_DeveIncrementarEstoque()
    {
        var db      = CreateDb(nameof(AdjustStock_AdicaoPositiva_DeveIncrementarEstoque));
        var service = CreateService(db);
        var p       = MakeProduct(stock: 5);
        db.Products.Add(p);
        await db.SaveChangesAsync();

        var ok = await service.AdjustStockAsync(p.Id, +10);

        ok.Should().BeTrue();
        // ExecuteUpdateAsync bypassa o change tracker — limpar antes de reler do banco
        db.ChangeTracker.Clear();
        (await db.Products.FindAsync(p.Id))!.StockQuantity.Should().Be(15);
    }

    [Fact]
    public async Task AdjustStock_SubtracaoValida_DeveDecrementarEstoque()
    {
        var db      = CreateDb(nameof(AdjustStock_SubtracaoValida_DeveDecrementarEstoque));
        var service = CreateService(db);
        var p       = MakeProduct(stock: 10);
        db.Products.Add(p);
        await db.SaveChangesAsync();

        var ok = await service.AdjustStockAsync(p.Id, -4);

        ok.Should().BeTrue();
        db.ChangeTracker.Clear();
        (await db.Products.FindAsync(p.Id))!.StockQuantity.Should().Be(6);
    }

    [Fact]
    public async Task AdjustStock_DeveRejeitarSeResultadoNegativo()
    {
        var db      = CreateDb(nameof(AdjustStock_DeveRejeitarSeResultadoNegativo));
        var service = CreateService(db);
        var p       = MakeProduct(stock: 3);
        db.Products.Add(p);
        await db.SaveChangesAsync();

        var ok = await service.AdjustStockAsync(p.Id, -10);

        ok.Should().BeFalse();
        (await db.Products.FindAsync(p.Id))!.StockQuantity.Should().Be(3, "não deve alterar se vai ficar negativo");
    }

    // ── Soft delete ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Deactivate_DeveMudarIsActiveParaFalse()
    {
        var db      = CreateDb(nameof(Deactivate_DeveMudarIsActiveParaFalse));
        var service = CreateService(db);
        var p       = MakeProduct();
        db.Products.Add(p);
        await db.SaveChangesAsync();

        await service.DeactivateAsync(p.Id);

        (await db.Products.FindAsync(p.Id))!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_ProdutoInexistente_NaoDeveLancarExcecao()
    {
        var db      = CreateDb(nameof(Deactivate_ProdutoInexistente_NaoDeveLancarExcecao));
        var service = CreateService(db);

        var act = async () => await service.DeactivateAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    // ── Criar e atualizar ─────────────────────────────────────────────────────

    [Fact]
    public async Task Create_DevePersistirProduto()
    {
        var db      = CreateDb(nameof(Create_DevePersistirProduto));
        var service = CreateService(db);
        var p       = MakeProduct("Novo Produto");

        var criado = await service.CreateAsync(p);

        criado.Id.Should().NotBeEmpty();
        (await db.Products.FindAsync(criado.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Update_DeveAlterarDadosEUpdatedAt()
    {
        var db      = CreateDb(nameof(Update_DeveAlterarDadosEUpdatedAt));
        var service = CreateService(db);
        var p       = MakeProduct("Nome Original");
        db.Products.Add(p);
        await db.SaveChangesAsync();

        p.Name       = "Nome Atualizado";
        p.PriceInCents = 9999;
        var antes    = p.UpdatedAt;
        await Task.Delay(5); // garante que UpdatedAt vai mudar

        var atualizado = await service.UpdateAsync(p);

        atualizado.Name.Should().Be("Nome Atualizado");
        atualizado.UpdatedAt.Should().BeAfter(antes);
    }

    [Fact]
    public async Task Update_DevePersistirNcmENaturezaOperacao()
    {
        var db      = CreateDb(nameof(Update_DevePersistirNcmENaturezaOperacao));
        var service = CreateService(db);
        var p       = MakeProduct("Produto fiscal");
        p.Ncm       = "84747100";
        db.Products.Add(p);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var naturezaId = Guid.NewGuid();
        db.NaturezasOperacao.Add(new NaturezaOperacao
        {
            Id = naturezaId,
            Descricao = "Venda interna",
            Cfop = "5102",
            Csosn = "102",
        });
        await db.SaveChangesAsync();
        var atualizado = MakeProduct("Produto fiscal");
        atualizado.Id                 = p.Id;
        atualizado.Ncm                = "9504.40.00";
        atualizado.Cest               = "28.063.00";
        atualizado.PercentualTributosFederais = 12.34m;
        atualizado.PercentualTributosEstaduais = 18m;
        atualizado.PercentualTributosMunicipais = 0m;
        atualizado.FonteTributos      = "  Tabela contador 2026  ";
        atualizado.NaturezaOperacaoId = naturezaId;

        await service.UpdateAsync(atualizado);
        db.ChangeTracker.Clear();

        var salvo = await db.Products.FindAsync(p.Id);
        salvo!.Ncm.Should().Be("95044000");
        salvo.Cest.Should().Be("2806300");
        salvo.PercentualTributosFederais.Should().Be(12.34m);
        salvo.PercentualTributosEstaduais.Should().Be(18m);
        salvo.PercentualTributosMunicipais.Should().Be(0m);
        salvo.FonteTributos.Should().Be("Tabela contador 2026");
        salvo.NaturezaOperacaoId.Should().Be(naturezaId);
    }

    [Fact]
    public async Task Update_SemAlterarTributos_DevePreservarMetadadosAutomaticosIbpt()
    {
        var db = CreateDb(nameof(Update_SemAlterarTributos_DevePreservarMetadadosAutomaticosIbpt));
        var service = CreateService(db);
        var p = MakeProduct("Produto IBPT");
        p.Ncm = "95044000";
        p.PercentualTributosFederais = 13.45m;
        p.PercentualTributosEstaduais = 18m;
        p.PercentualTributosMunicipais = 0m;
        p.FonteTributos = "IBPT 26.1.L";
        p.TributosPreenchidosAutomaticamente = true;
        p.TributosAtualizadosEm = DateTime.UtcNow.AddDays(-1);
        p.TributosVigenciaInicio = DateTime.UtcNow.AddDays(-10);
        p.TributosVigenciaFim = DateTime.UtcNow.AddDays(10);
        p.IbptVersao = "26.1.L";
        p.IbptChave = "chave-auditoria";
        db.Products.Add(p);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var atualizado = await db.Products.AsNoTracking().SingleAsync(x => x.Id == p.Id);
        atualizado.Name = "Produto IBPT renomeado";
        await service.UpdateAsync(atualizado);
        db.ChangeTracker.Clear();

        var salvo = await db.Products.FindAsync(p.Id);
        salvo!.TributosPreenchidosAutomaticamente.Should().BeTrue();
        salvo.IbptVersao.Should().Be("26.1.L");
        salvo.IbptChave.Should().Be("chave-auditoria");
        salvo.TributosVigenciaFim.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_AoTrocarSomenteNcm_DeveInvalidarTributosAnteriores()
    {
        var db = CreateDb(nameof(Update_AoTrocarSomenteNcm_DeveInvalidarTributosAnteriores));
        var service = CreateService(db);
        var p = MakeProduct("Produto IBPT");
        p.Ncm = "95044000";
        p.PercentualTributosFederais = 13.45m;
        p.PercentualTributosEstaduais = 18m;
        p.PercentualTributosMunicipais = 0m;
        p.FonteTributos = "IBPT 26.1.L";
        p.TributosPreenchidosAutomaticamente = true;
        p.IbptVersao = "26.1.L";
        db.Products.Add(p);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var atualizado = await db.Products.AsNoTracking().SingleAsync(x => x.Id == p.Id);
        atualizado.Ncm = "84743100";
        await service.UpdateAsync(atualizado);
        db.ChangeTracker.Clear();

        var salvo = await db.Products.FindAsync(p.Id);
        salvo!.PercentualTributosFederais.Should().BeNull();
        salvo.PercentualTributosEstaduais.Should().BeNull();
        salvo.PercentualTributosMunicipais.Should().BeNull();
        salvo.FonteTributos.Should().BeNull();
        salvo.TributosPreenchidosAutomaticamente.Should().BeFalse();
        salvo.IbptVersao.Should().BeNull();
    }

    // ── Busca por ID ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ProdutoExistente_DeveRetornarProduto()
    {
        var db      = CreateDb(nameof(GetById_ProdutoExistente_DeveRetornarProduto));
        var service = CreateService(db);
        var p       = MakeProduct("Card Lendário");
        db.Products.Add(p);
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(p.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Card Lendário");
        result.Id.Should().Be(p.Id);
    }

    [Fact]
    public async Task GetById_ProdutoInexistente_DeveRetornarNull()
    {
        var db      = CreateDb(nameof(GetById_ProdutoInexistente_DeveRetornarNull));
        var service = CreateService(db);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    // ── Busca por código de barras ────────────────────────────────────────────

    [Fact]
    public async Task GetByBarcode_ProdutoExistente_DeveRetornarProduto()
    {
        var db      = CreateDb(nameof(GetByBarcode_ProdutoExistente_DeveRetornarProduto));
        var service = CreateService(db);
        var p       = MakeProduct("Produto Scanável");
        p.Barcode   = "7891234567890";
        db.Products.Add(p);
        await db.SaveChangesAsync();

        var result = await service.GetByBarcodeAsync("7891234567890");

        result.Should().NotBeNull();
        result!.Barcode.Should().Be("7891234567890");
        result.Name.Should().Be("Produto Scanável");
    }

    [Fact]
    public async Task GetByBarcode_CodigoInexistente_DeveRetornarNull()
    {
        var db      = CreateDb(nameof(GetByBarcode_CodigoInexistente_DeveRetornarNull));
        var service = CreateService(db);

        var result = await service.GetByBarcodeAsync("0000000000000");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByBarcode_ProdutoInativo_DeveRetornarNull()
    {
        // Produto inativo não deve aparecer no leitor de código de barras
        var db      = CreateDb(nameof(GetByBarcode_ProdutoInativo_DeveRetornarNull));
        var service = CreateService(db);
        var p       = MakeProduct("Produto Descontinuado", active: false);
        p.Barcode   = "9999999999999";
        db.Products.Add(p);
        await db.SaveChangesAsync();

        var result = await service.GetByBarcodeAsync("9999999999999");

        result.Should().BeNull("produto inativo não deve ser retornado");
    }
}
