// =============================================================================
// FinanceiroCalculoServiceTests.cs — Testes unitários do FinanceiroCalculoService
// Postgres real (não EF InMemory) — os Sum() traduzidos pro SQL precisam
// rodar contra um provider relacional de verdade pra pegar o mesmo tipo de
// bug que só aparece em runtime.
// Executar: dotnet test  (na pasta tests/unit/CardGameStore.Tests)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CardGameStore.Tests.Services;

public class FinanceiroCalculoServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb(string name) => TestDbFactory.Create(name);

    private static FinanceiroCalculoService CreateService(AppDbContext db)
    {
        var vendas = new VendaAvulsaService(db, NullLogger<VendaAvulsaService>.Instance,
            new Mock<IServiceScopeFactory>().Object, new Mock<ITenantContext>().Object);
        return new FinanceiroCalculoService(db, vendas);
    }

    private static readonly Guid AdminId = Guid.NewGuid();

    // Mesmo fuso que o próprio serviço usa (America/Sao_Paulo) — "hoje" em UTC
    // puro pode cair no dia errado perto da virada (Brasília = UTC-3), gerando
    // teste instável dependendo da hora real em que roda.
    private static readonly TimeZoneInfo BrazilZone = GetBrazilZone();
    private static TimeZoneInfo GetBrazilZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }

    private static DateTime HojeBrasil() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilZone).Date;

    /// <summary>Início UTC de um dia local de Brasília — mesma conversão que o
    /// BrDateToUtcStart privado do próprio serviço faz.</summary>
    private static DateTime BrDateToUtcStart(DateTime brDate) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(brDate.Date, DateTimeKind.Unspecified), BrazilZone);

    /// <summary>Janela de "hoje" (calendário de Brasília) já convertida pros limites
    /// UTC reais que CalcularAsync espera receber — mesmo shape que AnalyticsController
    /// monta a partir de datas locais de Brasília.</summary>
    private static (DateTime ini, DateTime end, DateTime dBrIni, DateTime dBrFim) JanelaHoje()
    {
        var hoje = HojeBrasil();
        return (BrDateToUtcStart(hoje), BrDateToUtcStart(hoje.AddDays(1)), hoje, hoje);
    }

    private static async Task<Product> SeedProductAsync(AppDbContext db, string category = "Geral", int costCents = 500)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(), Name = "Produto Teste", Category = category,
            PriceInCents = 1000, CostPriceInCents = costCents, StockQuantity = 100, MinimumStock = 1, IsActive = true,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static async Task<User> SeedUserAsync(AppDbContext db, string name = "Cliente Teste")
    {
        var user = new User { Id = Guid.NewGuid(), Name = name, PasswordHash = "hash", Role = UserRole.Customer };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Comanda> SeedComandaFechadaAsync(
        AppDbContext db, Product product, int quantity, int unitPriceCents, DateTime closedAt,
        string paymentMethod = "Pix", string? secondPaymentMethod = null, int secondAmountCents = 0)
    {
        var user  = await SeedUserAsync(db);
        var total = unitPriceCents * quantity;
        var comanda = new Comanda
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Status = ComandaStatus.Fechada,
            ClosedAt = closedAt,
            PaymentMethod = paymentMethod,
            SecondPaymentMethod = secondPaymentMethod,
            SecondPaymentAmountInCents = secondAmountCents,
            TotalInCents = total,
        };
        comanda.Items.Add(new ComandaItem
        {
            ComandaId = comanda.Id, ProductId = product.Id, ItemNameSnapshot = product.Name,
            UnitPriceInCents = unitPriceCents, Quantity = quantity, SubtotalInCents = total,
            CostPriceSnapshotInCents = product.CostPriceInCents,
        });
        db.Comandas.Add(comanda);
        await db.SaveChangesAsync();
        return comanda;
    }

    // ── Cenário vazio (a regressão que motivou estes testes) ──────────────────

    [Fact]
    public async Task CalcularAsync_SemNenhumaVendaOuComanda_DevolveTudoZeradoSemLancarExcecao()
    {
        // Antes do fix, Sum() traduzido pro SQL usava (decimal) e o SQLite
        // rejeitava a query com NotSupportedException — mesmo numa loja vazia
        // (nem precisava ter dado nenhum, só de existir a query já quebrava).
        var db = CreateDb(nameof(CalcularAsync_SemNenhumaVendaOuComanda_DevolveTudoZeradoSemLancarExcecao));
        var service = CreateService(db);
        var (ini, end, dBrIni, dBrFim) = JanelaHoje();

        var act = async () => await service.CalcularAsync(ini, end, dBrIni, dBrFim);

        var dto = await act.Should().NotThrowAsync();
        dto.Subject.Receita.Should().Be(0);
        dto.Subject.Custo.Should().Be(0);
        dto.Subject.Margem.Should().Be(0);
        dto.Subject.Crediarios.Should().Be(0);
        dto.Subject.TopProdutos.Should().BeEmpty();
        dto.Subject.PagamentosPorForma.Should().BeEmpty();
    }

    // ── Cálculo com dados reais ────────────────────────────────────────────────

    [Fact]
    public async Task CalcularAsync_ComComandaFechada_CalculaReceitaCustoEMargemCorretos()
    {
        var db = CreateDb(nameof(CalcularAsync_ComComandaFechada_CalculaReceitaCustoEMargemCorretos));
        var product = await SeedProductAsync(db, costCents: 400); // custo R$4, preço R$10
        var service = CreateService(db);
        var (ini, end, dBrIni, dBrFim) = JanelaHoje();

        await SeedComandaFechadaAsync(db, product, quantity: 2, unitPriceCents: 1000, closedAt: DateTime.UtcNow);

        var dto = await service.CalcularAsync(ini, end, dBrIni, dBrFim);

        dto.ReceitaComandas.Should().Be(20.00m); // 2 × R$10
        dto.Custo.Should().Be(8.00m);            // 2 × R$4
        dto.Margem.Should().Be(12.00m);
        dto.MargemPercent.Should().Be(150.0m);   // 12/8 × 100
    }

    [Fact]
    public async Task CalcularAsync_MesclaComandaEVendaAvulsaNaMesmaReceita()
    {
        var db = CreateDb(nameof(CalcularAsync_MesclaComandaEVendaAvulsaNaMesmaReceita));
        var product = await SeedProductAsync(db, costCents: 300);
        var service = CreateService(db);
        var (ini, end, dBrIni, dBrFim) = JanelaHoje();

        await SeedComandaFechadaAsync(db, product, quantity: 1, unitPriceCents: 1000, closedAt: DateTime.UtcNow);

        db.VendasAvulsas.Add(new VendaAvulsa
        {
            PaymentMethod = "Dinheiro", TotalInCents = 500, SoldAt = DateTime.UtcNow,
            SoldByAdminId = AdminId, SoldByAdminName = "Admin",
            Items = [new VendaAvulsaItem { ProductId = product.Id, ProductName = product.Name, Quantity = 1, UnitPriceInCents = 500, SubtotalInCents = 500, UnitCostInCents = 300 }],
        });
        await db.SaveChangesAsync();

        var dto = await service.CalcularAsync(ini, end, dBrIni, dBrFim);

        dto.ReceitaComandas.Should().Be(10.00m);
        dto.ReceitaAvulsa.Should().Be(5.00m);
        dto.Receita.Should().Be(15.00m);
    }

    [Fact]
    public async Task CalcularAsync_FiltroPorFormaDePagamento_SoConsideraTransacoesDaquelaForma()
    {
        var db = CreateDb(nameof(CalcularAsync_FiltroPorFormaDePagamento_SoConsideraTransacoesDaquelaForma));
        var product = await SeedProductAsync(db);
        var service = CreateService(db);
        var (ini, end, dBrIni, dBrFim) = JanelaHoje();

        await SeedComandaFechadaAsync(db, product, quantity: 1, unitPriceCents: 1000, closedAt: DateTime.UtcNow, paymentMethod: "Pix");
        await SeedComandaFechadaAsync(db, product, quantity: 1, unitPriceCents: 2000, closedAt: DateTime.UtcNow, paymentMethod: "Dinheiro");

        var dto = await service.CalcularAsync(ini, end, dBrIni, dBrFim, filterPaymentMethod: "Pix");

        dto.ReceitaComandas.Should().Be(10.00m, "só a comanda paga em Pix deve entrar no filtro");
    }

    [Fact]
    public async Task CalcularAsync_ComandaComSplitPayment_GeraDuasTransacoesNoBreakdown()
    {
        var db = CreateDb(nameof(CalcularAsync_ComandaComSplitPayment_GeraDuasTransacoesNoBreakdown));
        var product = await SeedProductAsync(db);
        var service = CreateService(db);
        var (ini, end, dBrIni, dBrFim) = JanelaHoje();

        // Total R$20: R$15 Pix + R$5 Dinheiro (split)
        await SeedComandaFechadaAsync(db, product, quantity: 2, unitPriceCents: 1000, closedAt: DateTime.UtcNow,
            paymentMethod: "Pix", secondPaymentMethod: "Dinheiro", secondAmountCents: 500);

        var dto = await service.CalcularAsync(ini, end, dBrIni, dBrFim);

        var pix = dto.PagamentosPorForma.Single(f => f.Forma == "Pix");
        var dinheiro = dto.PagamentosPorForma.Single(f => f.Forma == "Dinheiro");
        pix.Total.Should().Be(15.00m);
        dinheiro.Total.Should().Be(5.00m);
    }

    [Fact]
    public async Task CalcularAsync_ComCrediarioAberto_SomaSaldoDevedorCorretamente()
    {
        var db = CreateDb(nameof(CalcularAsync_ComCrediarioAberto_SomaSaldoDevedorCorretamente));
        var service = CreateService(db);
        var (ini, end, dBrIni, dBrFim) = JanelaHoje();
        var user1 = await SeedUserAsync(db, "Devedor 1");
        var user2 = await SeedUserAsync(db, "Devedor 2");

        db.Crediarios.Add(new Crediario
        {
            UserId = user1.Id, ValorEmCentavos = 10000, ValorPagoEmCentavos = 3000,
            Status = CrediariosStatus.Aberto, DataAbertura = DateTime.UtcNow, DataVencimento = DateTime.UtcNow.AddDays(30),
            AbertoPorAdminId = AdminId,
        });
        db.Crediarios.Add(new Crediario
        {
            UserId = user2.Id, ValorEmCentavos = 5000, ValorPagoEmCentavos = 5000,
            Status = CrediariosStatus.Pago, DataAbertura = DateTime.UtcNow, DataVencimento = DateTime.UtcNow.AddDays(30),
            AbertoPorAdminId = AdminId,
        });
        await db.SaveChangesAsync();

        var dto = await service.CalcularAsync(ini, end, dBrIni, dBrFim);

        dto.Crediarios.Should().Be(70.00m, "só o crediário Aberto conta (100 - 30), o Pago não entra");
    }

    [Fact]
    public async Task CalcularAsync_TopProdutos_SomaComandaEAvulsaDoMesmoProduto()
    {
        var db = CreateDb(nameof(CalcularAsync_TopProdutos_SomaComandaEAvulsaDoMesmoProduto));
        var product = await SeedProductAsync(db, costCents: 400);
        var service = CreateService(db);
        var (ini, end, dBrIni, dBrFim) = JanelaHoje();

        await SeedComandaFechadaAsync(db, product, quantity: 1, unitPriceCents: 1000, closedAt: DateTime.UtcNow);
        db.VendasAvulsas.Add(new VendaAvulsa
        {
            PaymentMethod = "Pix", TotalInCents = 1000, SoldAt = DateTime.UtcNow,
            SoldByAdminId = AdminId, SoldByAdminName = "Admin",
            Items = [new VendaAvulsaItem { ProductId = product.Id, ProductName = product.Name, ProductCategory = product.Category, Quantity = 1, UnitPriceInCents = 1000, SubtotalInCents = 1000, UnitCostInCents = 400 }],
        });
        await db.SaveChangesAsync();

        var dto = await service.CalcularAsync(ini, end, dBrIni, dBrFim);

        dto.TopProdutos.Should().ContainSingle();
        var top = dto.TopProdutos[0];
        top.QtdComandas.Should().Be(1);
        top.QtdAvulsa.Should().Be(1);
        top.Receita.Should().Be(20.00m); // R$10 comanda + R$10 avulsa
    }

    // ── FecharJanelaAsync (upsert) ──────────────────────────────────────────────

    [Fact]
    public async Task FecharJanelaAsync_PrimeiraVez_CriaFechamentoNovo()
    {
        var db = CreateDb(nameof(FecharJanelaAsync_PrimeiraVez_CriaFechamentoNovo));
        var product = await SeedProductAsync(db, costCents: 400);
        var service = CreateService(db);
        var hoje = HojeBrasil();
        await SeedComandaFechadaAsync(db, product, quantity: 1, unitPriceCents: 1000, closedAt: DateTime.UtcNow);

        var fechamento = await service.FecharJanelaAsync(TipoFechamento.Dia, hoje, hoje);

        fechamento.ReceitaComandas.Should().Be(1000);
        fechamento.CustoComandas.Should().Be(400);
        fechamento.Margem.Should().Be(600);
        (await db.FechamentosPeriodo.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FecharJanelaAsync_ChamadoDeNovoNaMesmaJanela_AtualizaEmVezDeDuplicar()
    {
        var db = CreateDb(nameof(FecharJanelaAsync_ChamadoDeNovoNaMesmaJanela_AtualizaEmVezDeDuplicar));
        var product = await SeedProductAsync(db, costCents: 400);
        var service = CreateService(db);
        var hoje = HojeBrasil();
        await SeedComandaFechadaAsync(db, product, quantity: 1, unitPriceCents: 1000, closedAt: DateTime.UtcNow);

        await service.FecharJanelaAsync(TipoFechamento.Dia, hoje, hoje);

        // Mais uma venda depois do primeiro fechamento — "reabrir" deve recalcular.
        await SeedComandaFechadaAsync(db, product, quantity: 1, unitPriceCents: 2000, closedAt: DateTime.UtcNow);
        var fechamento2 = await service.FecharJanelaAsync(TipoFechamento.Dia, hoje, hoje);

        (await db.FechamentosPeriodo.CountAsync()).Should().Be(1, "upsert por (Tipo,DataInicio,DataFim) — não deve duplicar linha");
        fechamento2.ReceitaComandas.Should().Be(3000, "recalculado com as duas comandas");
    }
}
