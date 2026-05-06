using CardGameStore.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CardGameStore.HealthChecks;

/// <summary>Verifica conectividade com o PostgreSQL via EF Core.</summary>
public sealed class DbHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    public DbHealthCheck(AppDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        try
        {
            var ok = await _db.Database.CanConnectAsync(cancellationToken);
            return ok
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Postgres não respondeu ao ping.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
