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
                await _scopeFactory.ForEachActiveTenantAsync(_logger, ReprocessarPendentesAsync, ct, requiredModule: "fiscal");
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
        var db      = sp.GetRequiredService<AppDbContext>();
        var emissao = sp.GetRequiredService<INfceEmissionService>();

        var pendentesIds = await db.NotasFiscaisEmitidas
            .Where(n => n.Status == NotaFiscalStatus.PendenteEmissao || n.Status == NotaFiscalStatus.AutorizadaContingencia)
            .OrderBy(n => n.CreatedAt)
            .Take(50) // não tenta reprocessar milhares de uma vez
            .Select(n => n.Id)
            .ToListAsync(ct);

        var estornosPendentesIds = await db.NotasFiscaisEmitidas
            .Where(n => n.Status == NotaFiscalStatus.Cancelada && n.ErpEstornadoEm == null)
            .OrderBy(n => n.CanceladoEm)
            .Take(50)
            .Select(n => n.Id)
            .ToListAsync(ct);

        if (pendentesIds.Count == 0 && estornosPendentesIds.Count == 0) return;

        await AlertarContingenciasCriticasAsync(db, ct);

        int autorizadas = 0;
        foreach (var id in pendentesIds)
        {
            var nota = await emissao.ReprocessarAsync(id);
            if (nota.Status == NotaFiscalStatus.Autorizada) autorizadas++;
        }

        foreach (var id in estornosPendentesIds)
            await emissao.ReprocessarEstornoErpAsync(id);

        _logger.LogInformation(
            "Reprocessamento automático: {Total} pendente(s) verificada(s), {Autorizadas} autorizada(s) agora.",
            pendentesIds.Count, autorizadas);
    }

    private static async Task AlertarContingenciasCriticasAsync(AppDbContext db, CancellationToken ct)
    {
        var limite = DateTime.UtcNow.AddHours(-20);
        var criticas = await db.NotasFiscaisEmitidas
            .Where(n => n.Status == NotaFiscalStatus.AutorizadaContingencia &&
                        n.DhContingencia != null && n.DhContingencia <= limite)
            .OrderBy(n => n.DhContingencia)
            .Select(n => new { n.Id, n.Numero, n.DhContingencia })
            .Take(20)
            .ToListAsync(ct);
        if (criticas.Count == 0) return;

        // Evita uma notificação nova a cada ciclo de 15 minutos.
        var dedupeDesde = DateTime.UtcNow.AddHours(-6);
        var jaAlertou = await db.Notifications.AnyAsync(
            n => n.Title == "NFC-e em contingência crítica" && n.CreatedAt >= dedupeDesde, ct);
        if (jaAlertou) return;

        var admins = await db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);
        var maisAntiga = criticas[0];
        var horas = maisAntiga.DhContingencia.HasValue
            ? (int)Math.Floor((DateTime.UtcNow - maisAntiga.DhContingencia.Value).TotalHours)
            : 20;

        foreach (var adminId in admins)
        {
            db.Notifications.Add(new Notification
            {
                UserId = adminId,
                Title = "NFC-e em contingência crítica",
                Body = $"{criticas.Count} NFC-e(s) aguardam autorização. A mais antiga está há {horas}h em contingência. Verifique antes do prazo de 24h.",
                Link = "/admin/fiscal",
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
