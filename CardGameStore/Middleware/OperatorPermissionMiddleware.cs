using System.Security.Claims;
using System.Text.Json;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Middleware;

/// <summary>
/// Autoriza Operator pela metadata do endpoint e pelas permissões atuais no banco.
/// Não confia no claim do JWT: remover um perfil/permissão passa a valer na próxima
/// requisição, sem aguardar renovação ou expiração do token.
/// </summary>
public class OperatorPermissionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OperatorPermissionMiddleware> _logger;

    public OperatorPermissionMiddleware(RequestDelegate next, ILogger<OperatorPermissionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var user = context.User;
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole(UserRole.Operator))
        {
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint is null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        // Endpoint sem Authorize é público. Um usuário autenticado não deve perder
        // acesso a uma rota pública apenas por carregar o papel Operator.
        if (endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
        {
            await _next(context);
            return;
        }

        if (endpoint.Metadata.GetMetadata<OperatorSelfServiceAttribute>() is not null)
        {
            await _next(context);
            return;
        }

        if (endpoint.Metadata.GetMetadata<OperatorForbiddenAttribute>() is not null)
        {
            await DenyAsync(context, "endpoint deliberadamente restrito a Admin");
            return;
        }

        var requirement = endpoint.Metadata.GetMetadata<RequireOperatorPermissionAttribute>();
        if (requirement is null || requirement.Permissions.Count == 0)
        {
            await DenyAsync(context, "endpoint autenticado sem classificação de Operator");
            return;
        }

        var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
        {
            await DenyAsync(context, "identificador de usuário inválido");
            return;
        }

        var current = await db.Users.AsNoTracking()
            .Where(item => item.Id == userId && item.IsActive && item.Role == UserRole.Operator)
            .Select(item => new { item.PerfilId, PermissoesJson = item.Perfil == null ? null : item.Perfil.PermissoesJson })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (current?.PerfilId is null || string.IsNullOrWhiteSpace(current.PermissoesJson))
        {
            await DenyAsync(context, "operador sem perfil ativo");
            return;
        }

        string[] permissions;
        try { permissions = JsonSerializer.Deserialize<string[]>(current.PermissoesJson) ?? []; }
        catch { permissions = []; }

        if (!requirement.Permissions.Any(required => permissions.Contains(required, StringComparer.OrdinalIgnoreCase)))
        {
            await DenyAsync(context, $"requer {string.Join(" ou ", requirement.Permissions)}");
            return;
        }

        await _next(context);
    }

    private async Task DenyAsync(HttpContext context, string reason)
    {
        _logger.LogWarning("Operator {UserId} negado em {Method} {Path}: {Reason}",
            context.User.FindFirst("sub")?.Value, context.Request.Method, context.Request.Path, reason);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { Message = "Sem permissão para esta ação." });
    }
}

public static class OperatorPermissionMiddlewareExtensions
{
    public static IApplicationBuilder UseOperatorPermissions(this IApplicationBuilder app) =>
        app.UseMiddleware<OperatorPermissionMiddleware>();
}
