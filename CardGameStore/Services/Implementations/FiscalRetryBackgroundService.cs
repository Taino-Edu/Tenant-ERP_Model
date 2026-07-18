// =============================================================================
// FiscalRetryBackgroundService.cs — Contingência: reprocessa periodicamente
// notas PendenteEmissao (ex: SEFAZ estava fora do ar na tentativa original).
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class FiscalRetryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FiscalRetryBackgroundService> _logger;

    public FiscalRetryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<FiscalRetryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _scopeFactory.ForEachActiveTenantAsync(_logger, ReprocessarPendentesAsync, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no reprocessamento automático de notas fiscais pendentes");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), ct);
        }
    }

    private async Task ReprocessarPendentesAsync(IServiceProvider sp, CancellationToken ct)
    {
        // F15: módulo fiscal desativado (mas com notas residuais no schema) não deve
        // continuar retransmitindo pra SEFAZ.
        if (!sp.GetRequiredService<ITenantContext>().EnabledModules.Contains("fiscal", StringComparer.OrdinalIgnoreCase))
            return;

        var db      = sp.GetRequiredService<AppDbContext>();
        var emissao = sp.GetRequiredService<INfceEmissionService>();

        var pendentesIds = await db.NotasFiscaisEmitidas
            .Where(n => n.Status == NotaFiscalStatus.PendenteEmissao || n.Status == NotaFiscalStatus.AutorizadaContingencia)
            .OrderBy(n => n.CreatedAt)
            .Take(50) // não tenta reprocessar milhares de uma vez
            .Select(n => n.Id)
            .ToListAsync(ct);

        if (pendentesIds.Count == 0) return;

        int autorizadas = 0;
        foreach (var id in pendentesIds)
        {
            var nota = await emissao.ReprocessarAsync(id);
            if (nota.Status == NotaFiscalStatus.Autorizada) autorizadas++;
        }

        _logger.LogInformation(
            "Reprocessamento automático: {Total} pendente(s) verificada(s), {Autorizadas} autorizada(s) agora.",
            pendentesIds.Count, autorizadas);
    }
}
