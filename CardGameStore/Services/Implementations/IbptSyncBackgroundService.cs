using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

/// <summary>
/// IBPT-002 — o único lugar do sistema que conversa com o IBPT.
///
/// Antes, a rede estava no caminho do usuário: cadastrar produto e clicar em
/// "sincronizar" disparavam consultas HTTP com 15s de timeout cada, dentro da
/// requisição. Catálogo grande com API lenta não terminava, e o usuário via 500.
///
/// Agora este job popula a tabela local uma vez por dia, e o cadastro só lê. O
/// tempo que a integração leva deixou de ser problema de ninguém: aqui não há
/// tela esperando, e a falha de um NCM não invalida os outros nem a tabela de
/// ontem.
/// </summary>
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
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.Uf)) return;
        if (cfg.IbptUltimaSincronizacao is { } ultima && ultima > DateTime.UtcNow.AddHours(-24)) return;

        var ibpt = sp.GetRequiredService<IbptTaxService>();

        // Primeiro a rede (popula/renova a tabela local), depois a aplicação nos
        // produtos — nesta ordem, o catálogo já aproveita o que acabou de chegar.
        await ibpt.AtualizarTabelaLocalAsync(ct);
        await ibpt.AplicarTabelaLocalAsync(ct);
    }
}
