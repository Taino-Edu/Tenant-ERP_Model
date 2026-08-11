using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

/// <summary>
/// Executa campanhas de prospecção sem contato automático. A saída é sempre
/// uma pesquisa persistida e uma fila de candidatos para revisão humana.
/// </summary>
public sealed class ProspectingBotBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProspectingBotBackgroundService> _logger;

    public ProspectingBotBackgroundService(
        IServiceScopeFactory scopeFactory, ILogger<ProspectingBotBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessOneAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha geral no ciclo do bot de prospecção");
            }
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var now = DateTime.UtcNow;
        var today = now.Date;

        var dueCampaign = await db.ProspectingCampaigns
            .Where(c => c.Status == ProspectingCampaignStatus.Active && c.NextRunAt <= now &&
                c.Runs.Count(r => r.CreatedAt >= today) < c.DailyRunBudget &&
                !c.Runs.Any(r => r.Status == ProspectingCampaignRunStatus.Queued ||
                                 r.Status == ProspectingCampaignRunStatus.Running))
            .OrderBy(c => c.NextRunAt)
            .FirstOrDefaultAsync(ct);
        if (dueCampaign is not null)
        {
            db.ProspectingCampaignRuns.Add(new ProspectingCampaignRun
            {
                CampaignId = dueCampaign.Id,
                NextAttemptAt = now,
            });
            // Avança antes da rede: se o processo reiniciar, não cria várias
            // execuções agendadas para o mesmo vencimento.
            dueCampaign.NextRunAt = now.AddHours(dueCampaign.IntervalHours);
            dueCampaign.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }

        var queuedId = await db.ProspectingCampaignRuns.AsNoTracking()
            .Where(r => r.Status == ProspectingCampaignRunStatus.Queued && r.NextAttemptAt <= now)
            .OrderBy(r => r.CreatedAt)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (queuedId == Guid.Empty) return false;

        var claimed = await db.ProspectingCampaignRuns
            .Where(r => r.Id == queuedId && r.Status == ProspectingCampaignRunStatus.Queued)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, ProspectingCampaignRunStatus.Running)
                .SetProperty(r => r.StartedAt, now)
                .SetProperty(r => r.AttemptCount, r => r.AttemptCount + 1), ct);
        if (claimed == 0) return true;

        db.ChangeTracker.Clear();
        var run = await db.ProspectingCampaignRuns.Include(r => r.Campaign)
            .FirstAsync(r => r.Id == queuedId, ct);
        try
        {
            var prospecting = scope.ServiceProvider.GetRequiredService<IProspectingService>();
            var result = await prospecting.SearchAsync(run.Campaign.Category, run.Campaign.City, forceRefresh: true);
            var prioritized = result.Candidates
                .Where(c => c.Status is "New" or "Selected")
                .Take(run.Campaign.MaxCandidatesPerRun)
                .ToList();

            run.SearchId = result.Id;
            run.DiscoveredCount = Math.Min(result.ResultCount, run.Campaign.MaxCandidatesPerRun);
            run.NewCount = prioritized.Count(c => c.FirstSeenAt >= (run.StartedAt ?? now));
            run.Status = ProspectingCampaignRunStatus.Completed;
            run.CompletedAt = DateTime.UtcNow;
            run.Campaign.LastRunAt = run.CompletedAt;
            run.Campaign.LastError = null;
        }
        catch (Exception ex)
        {
            run.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            run.Campaign.LastError = run.Error;
            // AttemptCount inclui a execução inicial; MaxRetryAttempts representa
            // quantas novas tentativas ainda podem ser agendadas depois dela.
            if (CanRetry(run.AttemptCount, run.Campaign.MaxRetryAttempts))
            {
                var retryDelay = CalculateRetryDelay(run.AttemptCount);
                run.Status = ProspectingCampaignRunStatus.Queued;
                run.NextAttemptAt = DateTime.UtcNow.Add(retryDelay);
                run.StartedAt = null;
                _logger.LogWarning(ex,
                    "Campanha {CampaignId} falhou na tentativa {Attempt}; nova tentativa em {Delay} min",
                    run.CampaignId, run.AttemptCount, retryDelay.TotalMinutes);
            }
            else
            {
                run.Status = ProspectingCampaignRunStatus.Failed;
                run.CompletedAt = DateTime.UtcNow;
                run.Campaign.LastRunAt = run.CompletedAt;
                _logger.LogWarning(ex, "Campanha de prospecção {CampaignId} falhou definitivamente", run.CampaignId);
            }
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    internal static TimeSpan CalculateRetryDelay(int attemptCount) =>
        TimeSpan.FromMinutes(5 * Math.Pow(3, Math.Max(0, attemptCount - 1)));

    internal static bool CanRetry(int attemptCount, int maxRetryAttempts) =>
        attemptCount <= maxRetryAttempts;
}
