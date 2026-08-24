using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace CardGameStore.Security;

/// <summary>
/// Valida as declaracoes de escopo no startup para que erros de configuracao
/// nao aparecam somente quando um integrador chamar a rota.
/// </summary>
public static class IntegrationScopeEndpointValidator
{
    public static WebApplication ValidateIntegrationScopeCoverage(this WebApplication app)
    {
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints);
        var invalid = FindInvalid(endpoints);
        if (invalid.Count > 0)
        {
            throw new InvalidOperationException(
                "Rotas com declaracao de integracao invalida:\n - " +
                string.Join("\n - ", invalid));
        }

        return app;
    }

    internal static IReadOnlyList<string> FindInvalid(IEnumerable<Endpoint> endpoints) => endpoints
        .OfType<RouteEndpoint>()
        .SelectMany(ValidateEndpoint)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static IEnumerable<string> ValidateEndpoint(RouteEndpoint endpoint)
    {
        var scopes = endpoint.Metadata.GetOrderedMetadata<RequireIntegrationScopeAttribute>();
        if (scopes.Count == 0)
            yield break;

        var label = $"{endpoint.RoutePattern.RawText} ({endpoint.DisplayName})";
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            yield return $"{label}: rota com escopo nao pode ser anonima";

        if (endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
            yield return $"{label}: rota com escopo precisa exigir autenticacao";

        foreach (var scope in scopes.Select(item => item.Scope).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!IntegrationScope.All.Contains(scope))
                yield return $"{label}: escopo desconhecido '{scope}'";
        }
    }
}
