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

    private static PlatformController CreateController(CatalogDbContext catalog)
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
            config);
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
}
