using CardGameStore.Data;
using Microsoft.EntityFrameworkCore;
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
