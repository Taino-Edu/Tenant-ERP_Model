using System.Security.Claims;
using CardGameStore.Multitenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Security;

public static class IntegrationScope
{
    public const string FinanceRead  = "financeiro.read";
    public const string FinanceWrite = "financeiro.write";
    public const string FiscalRead   = "fiscal.read";
    public const string FiscalWrite  = "fiscal.write";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [FinanceRead, FinanceWrite, FiscalRead, FiscalWrite],
        StringComparer.OrdinalIgnoreCase);
}

public static class IntegrationClaim
{
    public const string TokenType         = "token_type";
    public const string TokenTypeValue    = "client_credentials";
    public const string ClientRecordId    = "integration_client_id";
    public const string CredentialVersion = "credential_version";
    public const string Scope             = "scope";
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireIntegrationScopeAttribute(string scope) : Attribute
{
    public string Scope { get; } = scope;
}

/// <summary>
/// Tokens tecnicos sao negados por padrao. Para passar, a rota precisa declarar
/// escopo, o cliente precisa continuar ativo e a versao da credencial deve bater.
/// </summary>
public sealed class IntegrationAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IntegrationAccessMiddleware> _logger;

    public IntegrationAccessMiddleware(RequestDelegate next, ILogger<IntegrationAccessMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, CatalogDbContext catalog, ITenantContext tenant)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true ||
            user.FindFirstValue(IntegrationClaim.TokenType) != IntegrationClaim.TokenTypeValue)
        {
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        var requirements = endpoint?.Metadata
            .GetOrderedMetadata<RequireIntegrationScopeAttribute>() ?? [];
        if (requirements.Count == 0)
        {
            await DenyAsync(context, "rota sem escopo de integracao");
            return;
        }

        var recordIdValue = user.FindFirstValue(IntegrationClaim.ClientRecordId);
        var versionValue = user.FindFirstValue(IntegrationClaim.CredentialVersion);
        if (!Guid.TryParse(recordIdValue, out var recordId) || !int.TryParse(versionValue, out var version))
        {
            await DenyAsync(context, "claims tecnicas invalidas", StatusCodes.Status401Unauthorized);
            return;
        }

        var client = await catalog.ApiIntegrationClients.AsNoTracking()
            .Where(item => item.Id == recordId && item.TenantId == tenant.TenantId)
            .Select(item => new { item.IsActive, item.CredentialVersion })
            .SingleOrDefaultAsync(context.RequestAborted);

        if (client is null || !client.IsActive || client.CredentialVersion != version)
        {
            await DenyAsync(context, "credencial revogada ou rotacionada", StatusCodes.Status401Unauthorized);
            return;
        }

        var granted = (user.FindFirstValue(IntegrationClaim.Scope) ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requirements.Any(requirement => !granted.Contains(requirement.Scope)))
        {
            await DenyAsync(context, $"requer {string.Join(", ", requirements.Select(item => item.Scope))}");
            return;
        }

        await _next(context);
    }

    private async Task DenyAsync(HttpContext context, string reason, int status = StatusCodes.Status403Forbidden)
    {
        _logger.LogWarning(
            "Integracao {ClientId} negada em {Method} {Path}: {Reason}",
            context.User.FindFirstValue("client_id"), context.Request.Method, context.Request.Path, reason);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new
        {
            Message = status == StatusCodes.Status401Unauthorized
                ? "Credencial de integracao invalida ou revogada."
                : "A integracao nao possui acesso a esta operacao."
        });
    }
}

public static class IntegrationAccessMiddlewareExtensions
{
    public static IApplicationBuilder UseIntegrationAccess(this IApplicationBuilder app) =>
        app.UseMiddleware<IntegrationAccessMiddleware>();
}
