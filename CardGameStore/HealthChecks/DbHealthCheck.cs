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

    public DbHealthCheck(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
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
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
