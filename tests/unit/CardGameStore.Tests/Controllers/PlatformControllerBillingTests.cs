// =============================================================================
// PlatformControllerBillingTests.cs — PATCH /api/platform/tenants/{id}/billing,
// na parte que a tela passou a mandar: a data da primeira cobrança.
//
// O campo existia na API desde o RB-01, mas nenhuma tela o enviava. Quando a
// lista de lojas ganhou o <input type="date">, a data passou a chegar como
// "2026-10-05": sem fuso, DateTimeKind.Unspecified — e o Npgsql recusa gravar
// isso numa coluna timestamp with time zone. O InMemory destes testes não
// reproduz a recusa; o que eles travam é a normalização que evita chegar lá.
// =============================================================================

using CardGameStore.Controllers;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CardGameStore.Tests.Controllers;

public class PlatformControllerBillingTests
{
    private static readonly DateTime DataAtual = new(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);

    private static CatalogDbContext CreateCatalogDb() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PlatformController CreateController(CatalogDbContext catalog, bool comBilling = false)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQLAdmin"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Database:TenantCredentialKey"] = new string('k', 32),
                ["Multitenancy:RootDomain"] = "3esysten.com.br",
            })
            .Build();

        return new PlatformController(
            catalog,
            new Mock<ITenantProvisioningService>().Object,
            NullLogger<PlatformController>.Instance,
            new Mock<IServiceScopeFactory>().Object,
            new MemoryCache(new MemoryCacheOptions()),
            new TenantDatabaseAdmin(config, new TenantDatabaseCredentials(config)),
            config,
            comBilling
                ? new PlatformBillingService(catalog, NullLogger<PlatformBillingService>.Instance, config: config)
                : null);
    }

    private static async Task<Guid> SeedTenantAsync(CatalogDbContext db)
    {
        var tenant = new Tenant
        {
            Slug = "loja-a", SchemaName = "tenant_loja_a", Status = TenantStatus.Active,
            PlanName = "Rio", MonthlyPrice = 269m, BillingStartsOn = DataAtual,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private static UpdateTenantBillingRequest Pedido(DateTime? primeiraCobranca) => new()
    {
        PlanName = "Rio", PaymentStatus = "Pago", EnabledModules = ["fiscal"], BillingStartsOn = primeiraCobranca,
    };

    [Fact]
    public async Task UpdateBilling_DataDigitadaSemFuso_GravaSoODiaEmUtc()
    {
        await using var db = CreateCatalogDb();
        var id = await SeedTenantAsync(db);

        var result = await CreateController(db).UpdateBilling(id,
            Pedido(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified)));

        result.Should().BeOfType<OkObjectResult>();
        var gravada = (await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == id)).BillingStartsOn!.Value;
        gravada.Should().Be(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc));
        gravada.Kind.Should().Be(DateTimeKind.Utc, "timestamptz no Npgsql só aceita DateTime em UTC");
    }

    [Fact]
    public async Task UpdateBilling_DataComHorario_DescartaAHora()
    {
        await using var db = CreateCatalogDb();
        var id = await SeedTenantAsync(db);

        await CreateController(db).UpdateBilling(id,
            Pedido(new DateTime(2026, 10, 5, 15, 42, 0, DateTimeKind.Utc)));

        (await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == id)).BillingStartsOn
            .Should().Be(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
                "o dia desta data vira o dia de vencimento de toda mensalidade; hora não significa nada ali");
    }

    [Fact]
    public async Task UpdateBilling_SemPrimeiraCobranca_PreservaADataAtual()
    {
        await using var db = CreateCatalogDb();
        var id = await SeedTenantAsync(db);

        await CreateController(db).UpdateBilling(id, Pedido(primeiraCobranca: null));

        (await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == id)).BillingStartsOn
            .Should().Be(DataAtual, "a tela manda o PATCH ao mexer em plano ou pagamento sem mandar a data");
    }

    // ── "Pago" com dívida em aberto ──────────────────────────────────────────
    // O bug: marcar "Pago" na lista gravava só o status da loja. Nenhuma cobrança
    // era baixada, e a régua suspendia a loja de novo na rodada seguinte — com
    // e-mail de suspensão pro lojista a cada volta.

    private static async Task<(Guid TenantId, List<Guid> Cobrancas)> SeedLojaDevendoAsync(CatalogDbContext db, int cobrancas = 1)
    {
        var id = await SeedTenantAsync(db);
        var hoje = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var ids = new List<Guid>();
        for (var i = 1; i <= cobrancas; i++)
        {
            var cobranca = new TenantCharge
            {
                TenantId = id, Kind = TenantChargeKind.Mensalidade, Amount = 269m,
                ReferenceMonth = hoje.AddMonths(-i), DueDate = hoje.AddDays(-20 * i),
            };
            db.TenantCharges.Add(cobranca);
            ids.Add(cobranca.Id);
        }
        await db.SaveChangesAsync();
        return (id, ids);
    }

    [Fact]
    public async Task UpdateBilling_PagoComCobrancaEmAberto_RecusaEDevolveALista()
    {
        await using var db = CreateCatalogDb();
        var (id, _) = await SeedLojaDevendoAsync(db, cobrancas: 2);

        var resultado = await CreateController(db, comBilling: true)
            .UpdateBilling(id, Pedido(primeiraCobranca: null));

        resultado.Should().BeOfType<ConflictObjectResult>();
        var corpo = ((ConflictObjectResult)resultado).Value!;
        corpo.GetType().GetProperty("ErrorCode")!.GetValue(corpo).Should().Be("cobrancas_em_aberto");
        corpo.GetType().GetProperty("Total")!.GetValue(corpo).Should().Be(538m);

        (await db.TenantCharges.AsNoTracking().ToListAsync())
            .Should().OnlyContain(c => c.PaidAt == null, "recusar não pode baixar nada pela metade");
    }

    [Fact]
    public async Task UpdateBilling_PagoConfirmandoABaixa_QuitaAsCobrancas()
    {
        await using var db = CreateCatalogDb();
        var (id, atrasadas) = await SeedLojaDevendoAsync(db, cobrancas: 2);

        var pedido = Pedido(primeiraCobranca: null);
        pedido.DarBaixaNasCobrancas = true;

        var resultado = await CreateController(db, comBilling: true).UpdateBilling(id, pedido);

        resultado.Should().BeOfType<OkObjectResult>();
        // Só as que estavam em aberto. A mensalidade do mês corrente, que o
        // recálculo das condições cria em seguida, nasce a vencer e não é paga
        // por tabela — quitar atrasado não adianta o mês que está começando.
        (await db.TenantCharges.AsNoTracking().Where(c => atrasadas.Contains(c.Id)).ToListAsync())
            .Should().OnlyContain(c => c.PaidAt != null);
        (await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == id)).PaymentStatus
            .Should().Be(TenantPaymentStatus.Pago);
    }

    [Fact]
    public async Task UpdateBilling_PagoSemDivida_ContinuaPassandoDireto()
    {
        await using var db = CreateCatalogDb();
        var id = await SeedTenantAsync(db);

        var resultado = await CreateController(db, comBilling: true).UpdateBilling(id, Pedido(primeiraCobranca: null));

        resultado.Should().BeOfType<OkObjectResult>("loja sem cobrança em aberto não precisa de confirmação");
    }
}
