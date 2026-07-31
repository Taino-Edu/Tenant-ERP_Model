using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace CardGameStore.Middleware;

/// <summary>
/// Impede a aplicação de iniciar quando uma nova rota /api autenticada e
/// acessível a Operator não declara como deve ser autorizada.
/// </summary>
public static class OperatorPermissionEndpointValidator
{
    private static readonly HashSet<string> OperatorPolicies =
        new(StringComparer.OrdinalIgnoreCase) { "AdminOnly", "CustomerOrAdmin" };

    private static readonly HashSet<string> ExclusivePolicies =
        new(StringComparer.OrdinalIgnoreCase) { "PlatformOwnerOnly", "ContadorOnly" };

    public static WebApplication ValidateOperatorPermissionCoverage(this WebApplication app)
    {
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints);
        var missing = FindUnclassified(endpoints);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Rotas acessíveis a Operator sem classificação de permissão:\n - " +
                string.Join("\n - ", missing));
        }

        return app;
    }

    internal static IReadOnlyList<string> FindUnclassified(IEnumerable<Endpoint> endpoints) => endpoints
        .OfType<RouteEndpoint>()
        .Where(IsOperatorApiEndpoint)
        .Where(endpoint =>
            endpoint.Metadata.GetMetadata<RequireOperatorPermissionAttribute>() is null &&
            endpoint.Metadata.GetMetadata<OperatorSelfServiceAttribute>() is null &&
            endpoint.Metadata.GetMetadata<OperatorForbiddenAttribute>() is null)
        .Select(endpoint => $"{endpoint.RoutePattern.RawText} ({endpoint.DisplayName})")
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static bool IsOperatorApiEndpoint(RouteEndpoint endpoint)
    {
        var route = endpoint.RoutePattern.RawText?.TrimStart('/');
        if (route is null || !route.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            return false;

        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            return false;

        var authorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        if (authorization.Count == 0)
            return false;

        if (authorization.Any(item =>
                !string.IsNullOrWhiteSpace(item.Policy) && ExclusivePolicies.Contains(item.Policy)))
            return false;

        // Atributos de papel são combinados por AND. Se qualquer um exclui
        // Operator, essa rota não pertence ao contrato deste middleware.
        if (authorization.Any(item =>
                !string.IsNullOrWhiteSpace(item.Roles) &&
                !item.Roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Contains("Operator", StringComparer.OrdinalIgnoreCase)))
            return false;

        return authorization.Any(item =>
            string.IsNullOrWhiteSpace(item.Policy) || OperatorPolicies.Contains(item.Policy));
    }
}
