using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IPublicSalesAssistantService
{
    Task<PublicAssistantResponse> AskAsync(string message, CancellationToken cancellationToken = default);
}
