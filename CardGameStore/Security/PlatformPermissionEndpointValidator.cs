using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace CardGameStore.Security;

/// <summary>
/// Impede a aplicação de iniciar quando uma rota do painel da plataforma não
/// declara qual permissão ela exige.
///
/// PlatformAccessMiddleware só age onde existe [RequirePlatformPermission] — sem
/// o atributo ele deixa passar. Uma rota nova que esquecesse a declaração não
/// ficaria "protegida por padrão": ficaria aberta a QUALQUER integrante da
/// equipe, auditoria inclusive, e a falha seria silenciosa. Este validador é o
/// espelho do OperatorPermissionEndpointValidator, que já fazia isso pelo lado
/// do lojista; a assimetria era o débito.
/// </summary>
public static class PlatformPermissionEndpointValidator
{
    private const string PlatformPolicy = "PlatformOwnerOnly";

    public static WebApplication ValidatePlatformPermissionCoverage(this WebApplication app)
    {
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints);
        var missing = FindUnclassified(endpoints);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Rotas da plataforma sem declaração de permissão ([RequirePlatformPermission]):\n - " +
                string.Join("\n - ", missing));
        }

        return app;
    }

    internal static IReadOnlyList<string> FindUnclassified(IEnumerable<Endpoint> endpoints) => endpoints
        .OfType<RouteEndpoint>()
        .Where(IsPlatformEndpoint)
        .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<RequirePlatformPermissionAttribute>().Count == 0)
        .Select(endpoint => $"{endpoint.RoutePattern.RawText} ({endpoint.DisplayName})")
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static bool IsPlatformEndpoint(RouteEndpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            return false;

        return endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(item => string.Equals(item.Policy, PlatformPolicy, StringComparison.OrdinalIgnoreCase));
    }
}
