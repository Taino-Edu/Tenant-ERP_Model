using CardGameStore.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

/// <summary>
/// Sinaliza leads que chegaram à data de revisão. Não elimina automaticamente:
/// a decisão exige justificativa humana e fica registrada na trilha encadeada.
/// </summary>
public sealed class LeadRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeadRetentionBackgroundService> _logger;

    public LeadRetentionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<LeadRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(3), ct);
        while (!ct.IsCancellationRequested)
        {
            try { await FlagDueAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Falha na revisão de retenção dos leads"); }
            await Task.Delay(TimeSpan.FromHours(6), ct);
        }
    }

    internal async Task<int> FlagDueAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var now = DateTime.UtcNow;
        var due = await db.Leads.Where(l => l.AnonymizedAt == null && l.RetentionReviewAt <= now && l.RetentionReviewFlaggedAt == null)
            .OrderBy(l => l.RetentionReviewAt).Take(500).ToListAsync(ct);
        foreach (var lead in due)
        {
            lead.RetentionReviewFlaggedAt = now;
            await LeadPrivacyAudit.AppendAsync(db, lead.Id, "RetentionReviewDue",
                new { lead.RetentionReviewAt }, "Job de retenção", ct: ct);
        }
        if (due.Count > 0) await db.SaveChangesAsync(ct);
        return due.Count;
    }
}
