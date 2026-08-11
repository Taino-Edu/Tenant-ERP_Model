using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public sealed class ProspectingCampaignService : IProspectingCampaignService
{
    private readonly CatalogDbContext _catalog;

    public ProspectingCampaignService(CatalogDbContext catalog) => _catalog = catalog;

    public async Task<List<ProspectingCampaignDto>> ListAsync(CancellationToken ct = default)
    {
        var campaigns = await _catalog.ProspectingCampaigns.AsNoTracking()
            .Include(c => c.Runs.OrderByDescending(r => r.CreatedAt).Take(5))
            .OrderBy(c => c.Status).ThenBy(c => c.NextRunAt)
            .ToListAsync(ct);
        return campaigns.Select(ToDto).ToList();
    }

    public async Task<ProspectingCampaignDto> CreateAsync(
        CreateProspectingCampaignRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var campaign = new ProspectingCampaign
        {
            Name = request.Name.Trim(),
            Category = request.Categoria.Trim(),
            City = request.Cidade.Trim(),
            IntervalHours = request.IntervalHours,
            MaxCandidatesPerRun = request.MaxCandidatesPerRun,
            DailyRunBudget = request.DailyRunBudget,
            MaxRetryAttempts = request.MaxRetryAttempts,
            NextRunAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _catalog.ProspectingCampaigns.Add(campaign);
        await _catalog.SaveChangesAsync(ct);
        return ToDto(campaign);
    }

    public async Task<bool> SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var campaign = await _catalog.ProspectingCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return false;
        campaign.Status = active ? ProspectingCampaignStatus.Active : ProspectingCampaignStatus.Paused;
        campaign.UpdatedAt = DateTime.UtcNow;
        if (active && campaign.NextRunAt < DateTime.UtcNow)
            campaign.NextRunAt = DateTime.UtcNow;
        await _catalog.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ProspectingCampaignRunDto?> EnqueueAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _catalog.ProspectingCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return null;

        var activeRun = await _catalog.ProspectingCampaignRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.CampaignId == id &&
                (r.Status == ProspectingCampaignRunStatus.Queued || r.Status == ProspectingCampaignRunStatus.Running), ct);
        if (activeRun is not null) return ToDto(activeRun);

        var today = DateTime.UtcNow.Date;
        var runsToday = await _catalog.ProspectingCampaignRuns.AsNoTracking()
            .CountAsync(r => r.CampaignId == id && r.CreatedAt >= today, ct);
        if (runsToday >= campaign.DailyRunBudget)
            throw new ProspectingBudgetExceededException(
                $"Orçamento diário da campanha atingido ({campaign.DailyRunBudget} execução(ões)).");

        var run = new ProspectingCampaignRun { CampaignId = id, NextAttemptAt = DateTime.UtcNow };
        _catalog.ProspectingCampaignRuns.Add(run);
        campaign.NextRunAt = DateTime.UtcNow.AddHours(campaign.IntervalHours);
        campaign.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _catalog.SaveChangesAsync(ct);
            return ToDto(run);
        }
        catch (DbUpdateException)
        {
            _catalog.ChangeTracker.Clear();
            var concurrent = await _catalog.ProspectingCampaignRuns.AsNoTracking()
                .FirstOrDefaultAsync(r => r.CampaignId == id &&
                    (r.Status == ProspectingCampaignRunStatus.Queued ||
                     r.Status == ProspectingCampaignRunStatus.Running), ct);
            if (concurrent is not null) return ToDto(concurrent);
            throw;
        }
    }

    public async Task<List<ProspectCandidateDto>> ListReviewQueueAsync(
        int limit = 100, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var searchIds = _catalog.ProspectingCampaignRuns.AsNoTracking()
            .Where(r => r.Status == ProspectingCampaignRunStatus.Completed && r.SearchId != null)
            .Select(r => r.SearchId!.Value);

        var candidates = await _catalog.ProspectCandidates.AsNoTracking()
            .Include(c => c.Observations.OrderByDescending(o => o.ObservedAt).Take(5))
            .Where(c => searchIds.Contains(c.SearchId) &&
                (c.Status == ProspectCandidateStatus.New || c.Status == ProspectCandidateStatus.Selected))
            .OrderByDescending(c => c.OpportunityScore)
            .ThenByDescending(c => c.LastSeenAt)
            .Take(safeLimit * 3)
            .ToListAsync(ct);

        return candidates
            .GroupBy(c => $"{c.Source}:{c.SourceId}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.LastSeenAt).First())
            .OrderByDescending(c => c.OpportunityScore)
            .Take(safeLimit)
            .Select(c => new ProspectCandidateDto
            {
                Id = c.Id, PlaceId = c.SourceId, Nome = c.Name, Endereco = c.Address,
                Telefone = c.Phone, Website = c.Website, DigitalPresence = c.DigitalPresence,
                OpportunityScore = c.OpportunityScore, EstimatedRevenueRange = c.EstimatedRevenueRange,
                Status = c.Status.ToString(), LeadId = c.LeadId, FirstSeenAt = c.FirstSeenAt,
                LastSeenAt = c.LastSeenAt, EnrichmentStatus = c.EnrichmentStatus.ToString(),
                LastEnrichedAt = c.LastEnrichedAt, EnrichmentSource = c.EnrichmentSource,
                EnrichmentConfidence = c.EnrichmentConfidence, SuggestedApproach = c.SuggestedApproach,
                RecentObservations = c.Observations.OrderByDescending(o => o.ObservedAt).Take(5)
                    .Select(ToObservationDto).ToList(),
            }).ToList();
    }

    public async Task<bool> SuppressCandidateAsync(
        Guid candidateId, string reason, CancellationToken ct = default)
    {
        var candidate = await _catalog.ProspectCandidates.FirstOrDefaultAsync(c => c.Id == candidateId, ct);
        if (candidate is null) return false;

        var keys = BuildSuppressionKeys(candidate).ToList();
        foreach (var (type, value) in keys)
        {
            if (!await _catalog.ProspectSuppressions.AnyAsync(
                    s => s.KeyType == type && s.NormalizedValue == value, ct))
                _catalog.ProspectSuppressions.Add(new ProspectSuppression
                {
                    KeyType = type, NormalizedValue = value, Reason = reason.Trim(),
                });
        }

        var normalizedPhone = NormalizePhone(candidate.Phone);
        var matchingCandidates = await _catalog.ProspectCandidates
            .Where(c => (c.Source == candidate.Source && c.SourceId == candidate.SourceId) ||
                        (normalizedPhone != null && c.Phone != null && c.Phone == candidate.Phone))
            .ToListAsync(ct);
        foreach (var match in matchingCandidates)
            match.Status = ProspectCandidateStatus.Suppressed;
        await _catalog.SaveChangesAsync(ct);
        return true;
    }

    internal static IEnumerable<(ProspectSuppressionKeyType Type, string Value)> BuildSuppressionKeys(
        ProspectCandidate candidate)
    {
        yield return (ProspectSuppressionKeyType.SourceId,
            $"{candidate.Source}:{candidate.SourceId}".ToLowerInvariant());
        if (NormalizePhone(candidate.Phone) is { } phone)
            yield return (ProspectSuppressionKeyType.Phone, phone);
        if (NormalizeDomain(candidate.Website) is { } domain)
            yield return (ProspectSuppressionKeyType.Domain, domain);
    }

    internal static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = string.Concat(value.Where(char.IsDigit));
        return digits.Length >= 8 ? digits : null;
    }

    internal static string? NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            Uri.TryCreate($"https://{value}", UriKind.Absolute, out uri);
        if (uri is null || string.IsNullOrWhiteSpace(uri.Host)) return null;
        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    internal static ProspectingCampaignDto ToDto(ProspectingCampaign campaign) => new()
    {
        Id = campaign.Id,
        Name = campaign.Name,
        Categoria = campaign.Category,
        Cidade = campaign.City,
        Status = campaign.Status.ToString(),
        IntervalHours = campaign.IntervalHours,
        MaxCandidatesPerRun = campaign.MaxCandidatesPerRun,
        DailyRunBudget = campaign.DailyRunBudget,
        MaxRetryAttempts = campaign.MaxRetryAttempts,
        NextRunAt = campaign.NextRunAt,
        LastRunAt = campaign.LastRunAt,
        LastError = campaign.LastError,
        RecentRuns = campaign.Runs.OrderByDescending(r => r.CreatedAt).Take(5).Select(ToDto).ToList(),
    };

    internal static ProspectingCampaignRunDto ToDto(ProspectingCampaignRun run) => new()
    {
        Id = run.Id,
        Status = run.Status.ToString(),
        SearchId = run.SearchId,
        DiscoveredCount = run.DiscoveredCount,
        NewCount = run.NewCount,
        AttemptCount = run.AttemptCount,
        NextAttemptAt = run.NextAttemptAt,
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt,
        Error = run.Error,
    };

    private static ProspectObservationDto ToObservationDto(ProspectObservation observation) => new()
    {
        FieldName = observation.FieldName, PreviousValue = observation.PreviousValue,
        ObservedValue = observation.ObservedValue, Source = observation.Source,
        Confidence = observation.Confidence, ObservedAt = observation.ObservedAt,
    };
}
