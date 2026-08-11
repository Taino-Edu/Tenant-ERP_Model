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

        var run = new ProspectingCampaignRun { CampaignId = id };
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
            }).ToList();
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
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt,
        Error = run.Error,
    };
}
