// =============================================================================
// TenantIteration.cs — Helper para hosted services (BackgroundService) rodarem
// uma rotina "uma vez por tenant ativo".
//
// Fora do pipeline HTTP não existe TenantResolutionMiddleware pra resolver o
// tenant, então todo scope novo nasce apontando pro tenant-zero (schema
// "public" — ver ITenantContext). Um background service que só faz
// CreateScope() + GetRequiredService<AppDbContext>() opera SILENCIOSAMENTE só
// no tenant-zero e nunca toca em nenhuma loja provisionada.
//
// Este helper centraliza o padrão correto (já usado à mão em
// FechamentoBackgroundService): abre um scope de catálogo, lê os tenants
// Active, e pra cada um abre um scope NOVO, chama ITenantContext.Set(...) antes
// de qualquer resolução de AppDbContext, e isola falha por tenant (um schema
// quebrado não pode travar o processamento dos outros). Usar este helper em
// vez de reescrever o loop evita reintroduzir o bug de "esqueci o Set()".
// =============================================================================

using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

public static class TenantIteration
{
    /// <summary>
    /// Executa <c>perTenant</c> uma vez para cada tenant Active do catálogo, com
    /// o ITenantContext do scope já apontado para o schema do tenant. O callback
    /// recebe o <see cref="IServiceProvider"/> já configurado para o tenant —
    /// resolva AppDbContext e demais services a partir dele. Falha de um tenant é
    /// logada e não interrompe os demais.
    /// </summary>
    public static async Task ForEachActiveTenantAsync(
        this IServiceScopeFactory scopeFactory,
        ILogger logger,
        Func<IServiceProvider, CancellationToken, Task> perTenant,
        CancellationToken ct = default)
    {
        List<TenantSlot> tenants;
        using (var catalogScope = scopeFactory.CreateScope())
        {
            var catalog = catalogScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            tenants = await catalog.Tenants
                .Where(t => t.Status == TenantStatus.Active)
                .Select(t => new TenantSlot(t.Id, t.Slug, t.SchemaName, t.EnabledModules))
                .ToListAsync(ct);
        }

        foreach (var tenant in tenants)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var scope = scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>()
                    .Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

                await perTenant(scope.ServiceProvider, ct);
            }
            catch (Exception ex)
            {
                // Um tenant com schema quebrado/migração pendente não pode
                // travar o job dos outros — loga e segue o loop.
                logger.LogError(ex,
                    "Falha ao processar tenant {Slug} ({Schema}) em job de background", tenant.Slug, tenant.SchemaName);
            }
        }
    }

    private readonly record struct TenantSlot(Guid Id, string Slug, string SchemaName, string[] EnabledModules);
}
