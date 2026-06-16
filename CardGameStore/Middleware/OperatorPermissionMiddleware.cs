using CardGameStore.Models.PostgreSQL;
using System.Security.Claims;

namespace CardGameStore.Middleware;

/// <summary>
/// Verifica se um Operator tem a permissão necessária para acessar uma rota.
/// Admin sempre passa. Customer é bloqueado nas rotas admin.
/// </summary>
public class OperatorPermissionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OperatorPermissionMiddleware> _logger;

    public OperatorPermissionMiddleware(RequestDelegate next, ILogger<OperatorPermissionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Só verificar rotas /api/ (exceto auth e health)
        if (!path.StartsWith("/api/") || path.StartsWith("/api/auth") || path == "/health")
        {
            await _next(context);
            return;
        }

        var user = context.User;
        if (!user.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        var role = user.FindFirst(ClaimTypes.Role)?.Value;

        // Admin passa sempre
        if (role == UserRole.Admin)
        {
            await _next(context);
            return;
        }

        // Operator: verifica permissões
        if (role == UserRole.Operator)
        {
            var permissionsClaim = user.FindFirst("permissions")?.Value;
            if (string.IsNullOrEmpty(permissionsClaim))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { Message = "Operador sem permissões configuradas." });
                return;
            }

            string[] permissions;
            try { permissions = System.Text.Json.JsonSerializer.Deserialize<string[]>(permissionsClaim) ?? []; }
            catch { permissions = []; }

            var hasAccess = false;
            foreach (var (permissao, prefixos) in Permissao.RotasPrefixo)
            {
                if (!permissions.Contains(permissao)) continue;
                if (prefixos.Any(p => path.StartsWith(p.ToLowerInvariant())))
                {
                    hasAccess = true;
                    break;
                }
            }

            // Sempre permite rotas genéricas de leitura própria
            if (path.StartsWith("/api/user/me") || path.StartsWith("/api/comanda/mesa"))
                hasAccess = true;

            if (!hasAccess)
            {
                _logger.LogWarning("Operator {UserId} sem permissão para {Path}", user.FindFirst("sub")?.Value, path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { Message = "Sem permissão para esta ação." });
                return;
            }
        }

        await _next(context);
    }
}

public static class OperatorPermissionMiddlewareExtensions
{
    public static IApplicationBuilder UseOperatorPermissions(this IApplicationBuilder app) =>
        app.UseMiddleware<OperatorPermissionMiddleware>();
}
