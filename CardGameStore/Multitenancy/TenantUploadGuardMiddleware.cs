namespace CardGameStore.Multitenancy;

/// <summary>
/// Impede que um Host resolvido para um tenant sirva uploads particionados de
/// outro tenant. URLs legadas sem /uploads/t/{tenantId}/ continuam válidas
/// durante a migração dos arquivos já persistidos.
/// </summary>
public sealed class TenantUploadGuardMiddleware
{
    private readonly RequestDelegate _next;

    public TenantUploadGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        if (context.Request.Path.StartsWithSegments("/uploads/t", out var remaining))
        {
            var tenantSegment = remaining.Value?
                .TrimStart('/')
                .Split('/', 2, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!Guid.TryParseExact(tenantSegment, "N", out var requestedTenant)
                || requestedTenant != tenant.TenantId)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        await _next(context);
    }
}

public static class TenantUploadGuardMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantUploadGuard(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantUploadGuardMiddleware>();
}
