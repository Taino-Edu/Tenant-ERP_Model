using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IProspectingCampaignService
{
    Task<List<ProspectingCampaignDto>> ListAsync(CancellationToken ct = default);
    Task<ProspectingCampaignDto> CreateAsync(CreateProspectingCampaignRequest request, CancellationToken ct = default);
    Task<bool> SetActiveAsync(Guid id, bool active, CancellationToken ct = default);
    Task<ProspectingCampaignRunDto?> EnqueueAsync(Guid id, CancellationToken ct = default);
    Task<List<ProspectCandidateDto>> ListReviewQueueAsync(int limit = 100, CancellationToken ct = default);
    Task<bool> SuppressCandidateAsync(Guid candidateId, string reason, CancellationToken ct = default);
}

public sealed class ProspectingBudgetExceededException : InvalidOperationException
{
    public ProspectingBudgetExceededException(string message) : base(message) { }
}
