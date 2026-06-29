namespace CardGameStore.Services.Interfaces;

public interface IPushService
{
    Task SendAsync(Guid userId, string title, string body, string? link = null);
    Task SendToManyAsync(IEnumerable<Guid> userIds, string title, string body, string? link = null);
}
