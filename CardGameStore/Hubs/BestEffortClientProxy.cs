using Microsoft.AspNetCore.SignalR;

namespace CardGameStore.Hubs;

/// <summary>Falha de atualização ao vivo não transforma uma gravação confirmada em erro.</summary>
internal sealed class BestEffortClientProxy(IClientProxy inner, ILogger logger) : IClientProxy
{
    public async Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        try { await inner.SendCoreAsync(method, args, cancellationToken); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notificação {Event} não entregue; o cliente deve atualizar o estado pela API.", method);
        }
    }
}
