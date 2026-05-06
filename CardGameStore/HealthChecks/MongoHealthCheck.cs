using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;

namespace CardGameStore.HealthChecks;

/// <summary>
/// Verifica conectividade com o MongoDB.
/// Retorna Degraded (não Unhealthy) porque o Mongo é opcional —
/// o sistema continua operando sem ele (sem VendaAvulsa e cache TCG).
/// </summary>
public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly IMongoClient _mongo;

    public MongoHealthCheck(IMongoClient mongo) => _mongo = mongo;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        try
        {
            _mongo.ListDatabaseNames(cancellationToken);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "MongoDB indisponível — VendaAvulsa e cache TCG fora de operação. " + ex.Message));
        }
    }
}
