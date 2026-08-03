// =============================================================================
// TenantHubFilter.cs — Propaga o tenant resolvido por Host para dentro do SignalR.
//
// POR QUE ISTO EXISTE
//
// ITenantContext é scoped, e o SignalR cria um escopo de DI próprio por invocação
// de hub — ele NÃO herda o escopo da requisição HTTP. O TenantResolutionMiddleware
// roda no handshake e morre ali junto com o escopo dele. Resultado: o
// ITenantContext que o hub recebe nasce no valor padrão, que é a tenant-zero.
//
// Isso quebrava o tempo real inteiro fora da tenant-zero, de duas formas:
//
//   - Admin/Operator: o OnConnectedAsync deles só entra em grupo, não toca o
//     banco. A conexão subia limpa, o badge do painel ficava verde, e eles
//     entravam em "Tenant_00000000...0000_Admin" enquanto os eventos da loja
//     iam pra "Tenant_{idRealDaLoja}_Admin". Conectado e surdo, sem log de erro.
//   - Cliente (QR code): o OnConnectedAsync toca o banco pra achar a comanda
//     ativa. Escopo sem Set() → o TenantConnectionInterceptor derruba com
//     "ITenantContext.Set(...) nunca foi chamado neste escopo". Toda conexão de
//     cliente falhava.
//
// Em desenvolvimento nada disso aparecia: lá tudo roda na tenant-zero, onde
// Guid.Empty casa com Guid.Empty.
//
// COMO RESOLVE
//
// O HttpContext do handshake sobrevive à conexão inteira (HubCallerContext
// .GetHttpContext()), e o middleware deixa lá uma cópia do tenant resolvido. Este
// filtro lê essa cópia e chama Set() no ITenantContext do escopo da invocação,
// antes de qualquer método do hub rodar — inclusive OnConnectedAsync, que é onde
// o cliente morria.
//
// A cópia é promovida pra HubCallerContext.Items na primeira vez. Os Items da
// conexão são do SignalR e vivem exatamente o tempo dela; depois disso não se
// toca mais no HttpContext, que é objeto do pipeline HTTP e não foi feito pra ser
// lido a cada mensagem de WebSocket.
// =============================================================================

using Microsoft.AspNetCore.SignalR;

namespace CardGameStore.Multitenancy;

/// <summary>Tenant resolvido por Host, no formato que atravessa a fronteira entre
/// o pipeline HTTP e o SignalR.</summary>
public sealed record TenantSnapshot(Guid TenantId, string SchemaName, string[] EnabledModules);

public sealed class TenantHubFilter : IHubFilter
{
    /// <summary>Chave em HttpContext.Items onde o TenantResolutionMiddleware
    /// deixa o tenant da requisição de handshake.</summary>
    public const string HttpContextItemKey = "CardGameStore.Tenant";

    /// <summary>Chave em HubCallerContext.Items — cópia promovida na primeira
    /// invocação, pra não reler o HttpContext a cada mensagem.</summary>
    private const string ConnectionItemKey = "CardGameStore.Tenant.Connection";

    public Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        ApplyTenant(context.Context, context.ServiceProvider);
        return next(context);
    }

    public Task OnDisconnectedAsync(
        HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        // Aqui NÃO falha se o tenant não vier: no desligamento não há nada a
        // proteger — o OnDisconnectedAsync do hub só registra log — e uma exceção
        // no teardown de uma conexão que já morreu (inclusive uma que nem chegou a
        // completar o handshake) só produziria ruído em cima do erro de verdade.
        var snapshot = ResolveSnapshot(context.Context);
        if (snapshot is not null)
            context.ServiceProvider.GetRequiredService<ITenantContext>()
                .Set(snapshot.TenantId, snapshot.SchemaName, snapshot.EnabledModules);

        return next(context, exception);
    }

    public ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ApplyTenant(invocationContext.Context, invocationContext.ServiceProvider);
        return next(invocationContext);
    }

    private static void ApplyTenant(HubCallerContext caller, IServiceProvider services)
    {
        var snapshot = ResolveSnapshot(caller)
            // Toda requisição passa pelo TenantResolutionMiddleware, inclusive o
            // handshake — se o snapshot não está aqui, o tenant se perdeu no
            // caminho. Falhar alto é melhor que deixar a conexão cair
            // silenciosamente na tenant-zero, que foi exatamente o bug original.
            ?? throw new HubException(
                "Tenant não resolvido para esta conexão — o handshake não passou pelo " +
                "TenantResolutionMiddleware ou o HttpContext foi perdido.");

        services.GetRequiredService<ITenantContext>()
            .Set(snapshot.TenantId, snapshot.SchemaName, snapshot.EnabledModules);
    }

    private static TenantSnapshot? ResolveSnapshot(HubCallerContext caller)
    {
        if (caller.Items.TryGetValue(ConnectionItemKey, out var cached) && cached is TenantSnapshot promoted)
            return promoted;

        if (caller.GetHttpContext()?.Items.TryGetValue(HttpContextItemKey, out var fromHttp) == true
            && fromHttp is TenantSnapshot snapshot)
        {
            caller.Items[ConnectionItemKey] = snapshot;
            return snapshot;
        }

        return null;
    }
}
