using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

/// <summary>Atualiza diariamente as tabelas IBPT de cada tenant fiscal sem misturar schemas.</summary>
public sealed class IbptSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IbptSyncBackgroundService> _logger;

    public IbptSyncBackgroundService(
        IServiceScopeFactory scopeFactory, ILogger<IbptSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _scopeFactory.ForEachActiveTenantAsync(
                    _logger, SincronizarTenantAsync, ct, requiredModule: "fiscal");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha geral no ciclo de sincronização IBPT");
            }

            await Task.Delay(TimeSpan.FromHours(12), ct);
        }
    }

    private static async Task SincronizarTenantAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var cfg = await db.FiscalConfigs.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == FiscalConfig.SingletonId, ct);
        if (cfg is null || !cfg.IbptAutoSyncEnabled || !cfg.IbptConfigurado) return;
        if (cfg.IbptUltimaSincronizacao is { } ultima && ultima > DateTime.UtcNow.AddHours(-24)) return;

        await sp.GetRequiredService<IbptTaxService>().SincronizarTodosAsync(ct);
    }
}
