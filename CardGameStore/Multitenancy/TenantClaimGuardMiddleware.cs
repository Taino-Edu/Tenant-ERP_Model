// =============================================================================
// TenantClaimGuardMiddleware.cs — Defesa em profundidade multi-tenant.
//
// O schema que roteia a conexão é sempre o resolvido por Host
// (TenantResolutionMiddleware, fonte da verdade). Este middleware só compara
// a claim tenant_id do JWT (se autenticado) contra esse tenant resolvido —
// mismatch indica um token de um tenant sendo usado contra o subdomínio de
// outro, o que nunca deveria acontecer legitimamente.
//
// Roda depois de UseAuthentication (precisa de context.User já populado) e
// antes de UseAuthorization.
// =============================================================================

namespace CardGameStore.Multitenancy;

public class TenantClaimGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantClaimGuardMiddleware> _logger;

    public TenantClaimGuardMiddleware(RequestDelegate next, ILogger<TenantClaimGuardMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claimValue = context.User.FindFirst(TenantConstants.TenantIdClaimType)?.Value;
            // Tokens sem a claim (emitidos antes desta feature existir) são tratados
            // como tenant-zero — mesma convenção usada em TenantContext/AuthService.
            var claimedTenantId = Guid.TryParse(claimValue, out var parsed) ? parsed : TenantConstants.TenantZeroId;

            if (claimedTenantId != tenantContext.TenantId)
            {
                _logger.LogWarning(
                    "Tenant mismatch: token emitido pro tenant {ClaimedTenant} usado contra host resolvido pro tenant {ResolvedTenant}.",
                    claimedTenantId, tenantContext.TenantId);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { Message = "Sessão inválida para este endereço." });
                return;
            }
        }

        await _next(context);
    }
}

public static class TenantClaimGuardMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantClaimGuard(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantClaimGuardMiddleware>();
}
