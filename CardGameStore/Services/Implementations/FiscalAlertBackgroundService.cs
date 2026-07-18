// =============================================================================
// FiscalAlertBackgroundService.cs — Verifica diariamente o vencimento do
// certificado fiscal e alerta os admins em 30/15/7/1 dias (dashboard + email).
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class FiscalAlertBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FiscalAlertBackgroundService> _logger;

    public FiscalAlertBackgroundService(IServiceScopeFactory scopeFactory, ILogger<FiscalAlertBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // aguarda 2 min após startup para não competir com EnsureCreated
        await Task.Delay(TimeSpan.FromMinutes(2), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _scopeFactory.ForEachActiveTenantAsync(_logger, CheckAsync, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao checar vencimento do certificado fiscal");
            }

            await Task.Delay(TimeSpan.FromHours(12), ct);
        }
    }

    private async Task CheckAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db    = sp.GetRequiredService<AppDbContext>();
        var email = sp.GetRequiredService<IEmailService>();

        var cfg = await db.FiscalConfigs.FindAsync(new object?[] { FiscalConfig.SingletonId }, ct);
        if (cfg?.CertificadoValidade is null) return;

        var diasRestantes = (int)Math.Floor((cfg.CertificadoValidade.Value.Date - DateTime.UtcNow.Date).TotalDays);
        var limiar = FiscalAlertCalculator.ProximoLimiarParaAlertar(diasRestantes, cfg.CertificadoUltimoAlertaLimiar);
        if (limiar is null) return;

        var admins = await db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToListAsync(ct);

        foreach (var admin in admins)
        {
            db.Notifications.Add(new Notification
            {
                UserId = admin.Id,
                Title  = "Certificado fiscal vencendo",
                Body   = $"Faltam {diasRestantes} dia(s) para o certificado A1 vencer " +
                          $"({cfg.CertificadoValidade.Value:dd/MM/yyyy}). Atualize em Admin > Fiscal.",
                Link   = "/admin/fiscal",
            });

            if (!string.IsNullOrWhiteSpace(admin.Email))
                await email.SendCertificadoVencendoAsync(admin.Email, admin.Name, diasRestantes, cfg.CertificadoValidade.Value);
        }

        cfg.CertificadoUltimoAlertaLimiar = limiar.Value;
        cfg.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        _logger.LogInformation(
            "Alerta de certificado fiscal disparado — limiar {Limiar} dias, {Count} admins notificados",
            limiar, admins.Count);
    }
}
