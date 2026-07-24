using CardGameStore.HealthChecks;
using CardGameStore.Multitenancy;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CardGameStore.Tests.HealthChecks;

public sealed class DbHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_QuandoPostgresExecutaSelect_RetornaHealthy()
    {
        await using var db = TestDbFactory.Create(nameof(CheckHealthAsync_QuandoPostgresExecutaSelect_RetornaHealthy));
        var tenantContext = new TenantContext();
        var check = new DbHealthCheck(db, tenantContext);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True(tenantContext.IsExplicitlySet);
        Assert.Equal(TenantConstants.TenantZeroSchema, tenantContext.SchemaName);
    }
}
