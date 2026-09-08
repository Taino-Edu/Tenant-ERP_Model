using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CardGameStore.Data;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Security;

/// <summary>
/// Revoga access tokens de usuários do tenant imediatamente após logout, troca
/// de senha, desativação ou alteração explícita da versão de sessão.
/// Tokens especiais sem session_version (contador e impersonação curta) seguem
/// seus próprios ciclos de vida.
/// </summary>
public sealed class SessionVersionMiddleware
{
    private readonly RequestDelegate _next;

    public SessionVersionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            context.User.FindFirstValue("session_version") is not { } versionClaim)
        {
            await _next(context);
            return;
        }

        var subject = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out var userId) ||
            !int.TryParse(versionClaim, out var claimedVersion))
        {
            await RejectAsync(context);
            return;
        }

        var current = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.SessionVersion })
            .SingleOrDefaultAsync(context.RequestAborted);

        if (current is null || !current.IsActive || current.SessionVersion != claimedVersion)
        {
            await RejectAsync(context);
            return;
        }

        await _next(context);
    }

    private static Task RejectAsync(HttpContext context)
    {
        context.Response.Cookies.Delete("accessToken");
        context.Response.Cookies.Delete("refreshToken");
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new
        {
            Message = "Sua sessão foi encerrada ou atualizada. Entre novamente."
        });
    }
}

public static class SessionVersionMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionVersionValidation(this IApplicationBuilder app) =>
        app.UseMiddleware<SessionVersionMiddleware>();
}
