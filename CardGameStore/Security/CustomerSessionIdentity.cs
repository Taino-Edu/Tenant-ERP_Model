using System.Security.Claims;
using CardGameStore.Multitenancy;
using CardGameStore.Models.PostgreSQL;
namespace CardGameStore.Security;

internal static class CustomerSessionIdentity
{
    public static Guid? Resolve(ClaimsPrincipal user, Guid currentTenant)
    {
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole(UserRole.Customer)) return null;
        if (!Guid.TryParse(user.FindFirst(TenantConstants.TenantIdClaimType)?.Value, out var tenantId)
            || tenantId != currentTenant) return null;
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
