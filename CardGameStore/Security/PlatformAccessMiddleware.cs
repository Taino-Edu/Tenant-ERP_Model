using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Security;

public sealed class PlatformAccessMiddleware
{
    private readonly RequestDelegate _next;

    public PlatformAccessMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var requirements = context.GetEndpoint()?.Metadata
            .GetOrderedMetadata<RequirePlatformPermissionAttribute>();

        if (requirements is not { Count: > 0 } ||
            context.User.Identity?.IsAuthenticated != true ||
            !context.User.IsInRole(UserRole.PlatformOwner))
        {
            await _next(context);
            return;
        }

        var subject = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out var userId))
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, "Sessão inválida.");
            return;
        }

        var owner = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.Role == UserRole.PlatformOwner)
            .Select(u => new
            {
                u.IsActive,
                u.IsPlatformPrimaryOwner,
                u.PlatformPermissionsJson,
                u.SessionVersion,
            })
            .SingleOrDefaultAsync(context.RequestAborted);

        var claimVersion = context.User.FindFirstValue("session_version");
        if (owner is null || !owner.IsActive || !int.TryParse(claimVersion, out var sessionVersion) || sessionVersion != owner.SessionVersion)
        {
            context.Response.Cookies.Delete("accessToken");
            context.Response.Cookies.Delete("refreshToken");
            await RejectAsync(context, StatusCodes.Status401Unauthorized, "Seu acesso foi atualizado. Entre novamente.");
            return;
        }

        if (!owner.IsPlatformPrimaryOwner)
        {
            var granted = PlatformAccessProfiles.Deserialize(owner.PlatformPermissionsJson);
            var allowed = requirements.All(requirement =>
                granted.Contains(PlatformPermission.All, StringComparer.OrdinalIgnoreCase) ||
                granted.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase));

            if (!allowed)
            {
                await RejectAsync(context, StatusCodes.Status403Forbidden, "Seu perfil não permite esta ação.");
                return;
            }
        }

        await _next(context);
    }

    private static Task RejectAsync(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { Message = message });
    }
}

public static class PlatformAccessMiddlewareExtensions
{
    public static IApplicationBuilder UsePlatformAccess(this IApplicationBuilder app) =>
        app.UseMiddleware<PlatformAccessMiddleware>();
}
