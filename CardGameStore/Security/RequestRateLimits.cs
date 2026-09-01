using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using CardGameStore.Multitenancy;
using Microsoft.AspNetCore.RateLimiting;

namespace CardGameStore.Security;

public static class RequestRateLimits
{
    // Já normalizado por UseForwardedHeaders, a partir dos proxies confiáveis.
    // Nunca confiar diretamente no CF-Connecting-IP enviado pelo solicitante.
    public static string ClientIp(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        return address?.IsIPv4MappedToIPv6 == true
            ? address.MapToIPv4().ToString()
            : address?.ToString() ?? "unknown";
    }

    public static string TenantIp(HttpContext context) =>
        $"{context.RequestServices.GetRequiredService<ITenantContext>().TenantId:N}:ip:{ClientIp(context)}";

    public static string TenantUserOrIp(HttpContext context)
    {
        var userId = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value
            : null;
        return !string.IsNullOrEmpty(userId)
            ? $"{context.RequestServices.GetRequiredService<ITenantContext>().TenantId:N}:user:{userId}"
            : TenantIp(context);
    }

    public static RateLimitPartition<string> Partition(HttpContext context, string policy)
    {
        var (key, limit, window, queue) = policy switch
        {
            "global" => (TenantUserOrIp(context), 300, TimeSpan.FromMinutes(1), 0),
            "auth" => (TenantIp(context), 15, TimeSpan.FromMinutes(1), 0),
            "api" => (TenantUserOrIp(context), 200, TimeSpan.FromMinutes(1), 10),
            "integration-token" => (ClientIp(context), 10, TimeSpan.FromMinutes(1), 0),
            "public-ai" => (ClientIp(context), 10, TimeSpan.FromMinutes(1), 0),
            "public-lead" => (ClientIp(context), 5, TimeSpan.FromMinutes(15), 0),
            // Busca de conta percorre tenants: limite por IP independente de loja.
            "locate-account" => (ClientIp(context), 5, TimeSpan.FromHours(1), 0),
            "comanda-hub" => (TenantUserOrIp(context), 30, TimeSpan.FromMinutes(1), 0),
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };
        return RateLimitPartition.GetFixedWindowLimiter($"{policy}:{key}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = window,
            QueueLimit = queue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    public static void Configure(RateLimiterOptions options)
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => Partition(context, "global"));
        foreach (var name in new[] { "auth", "api", "integration-token", "public-ai", "public-lead", "locate-account", "comanda-hub" })
            options.AddPolicy(name, context => Partition(context, name));

        options.OnRejected = RejectAsync;
    }

    public static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 60;
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers["Retry-After"] = seconds.ToString(CultureInfo.InvariantCulture);
        response.Headers.CacheControl = "no-store";
        await response.WriteAsJsonAsync(new
        {
            Message = $"Muitas requisições. Aguarde {seconds} segundos antes de tentar novamente.",
            RetryAfterSeconds = seconds,
        }, cancellationToken);
    }
}
