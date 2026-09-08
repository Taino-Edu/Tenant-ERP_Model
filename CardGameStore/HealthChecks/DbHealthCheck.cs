using CardGameStore.Data;
using CardGameStore.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CardGameStore.HealthChecks;

/// <summary>Verifica conectividade com o PostgreSQL via EF Core.</summary>
public sealed class DbHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly CatalogDbContext _catalog;

    public DbHealthCheck(AppDbContext db, ITenantContext tenantContext, CatalogDbContext catalog)
    {
        _db = db;
        _tenantContext = tenantContext;
        _catalog = catalog;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        try
        {
            // Health checks executam fora do TenantResolutionMiddleware. Marcar o
            // tenant-zero é obrigatório antes de abrir a conexão: o interceptor
            // rejeita corretamente qualquer escopo sem tenant explícito.
            _tenantContext.Set(
                TenantConstants.TenantZeroId,
                TenantConstants.TenantZeroSchema,
                new[] { "fiscal" });

            // CanConnectAsync() pode retornar false sem propagar a causa e produziu
            // falso negativo em produção mesmo enquanto o mesmo DbContext executava
            // migrations e SELECTs normalmente. Execute um comando real: sucesso
            // comprova a conexão; falha preserva a exceção para o diagnóstico.
            await _db.Database.ExecuteSqlRawAsync("SELECT 1;", cancellationToken);
            var unavailable = await _catalog.Tenants.AsNoTracking()
                .CountAsync(t => t.Status == TenantStatus.Active && !t.SchemaReady, cancellationToken);
            if (unavailable > 0)
                return HealthCheckResult.Degraded(
                    $"PostgreSQL conectado; {unavailable} tenant(s) isolado(s) por falha de migration.");
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
