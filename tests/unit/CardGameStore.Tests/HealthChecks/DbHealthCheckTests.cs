using CardGameStore.HealthChecks;
using CardGameStore.Multitenancy;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Tests.HealthChecks;

public sealed class DbHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_QuandoPostgresExecutaSelect_RetornaHealthy()
    {
        await using var db = TestDbFactory.Create(nameof(CheckHealthAsync_QuandoPostgresExecutaSelect_RetornaHealthy));
        var tenantContext = new TenantContext();
        await using var catalog = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var check = new DbHealthCheck(db, tenantContext, catalog);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True(tenantContext.IsExplicitlySet);
        Assert.Equal(TenantConstants.TenantZeroSchema, tenantContext.SchemaName);
    }

    [Fact]
    public async Task CheckHealthAsync_ComTenantIsolado_RetornaDegraded()
    {
        await using var db = TestDbFactory.Create(nameof(CheckHealthAsync_ComTenantIsolado_RetornaDegraded));
        var tenantContext = new TenantContext();
        await using var catalog = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        catalog.Tenants.Add(new Tenant
        {
            Slug = "falhou", SchemaName = "tenant_falhou", Status = TenantStatus.Active,
            SchemaReady = false,
        });
        await catalog.SaveChangesAsync();

        var result = await new DbHealthCheck(db, tenantContext, catalog)
            .CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("1 tenant", result.Description);
    }
}
