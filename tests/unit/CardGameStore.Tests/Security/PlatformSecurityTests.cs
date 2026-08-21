using System.Security.Claims;
using CardGameStore.Security;
using CardGameStore.Services.Implementations;
using CardGameStore.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace CardGameStore.Tests.Security;

public sealed class PlatformSecurityTests
{
    [Fact]
    public void PartnerProfile_GrantsEveryArea()
    {
        var partner = PlatformAccessProfiles.All[PlatformAccessProfiles.Partner];

        Assert.True(partner.Selectable);
        Assert.Contains(PlatformPermission.All, partner.Permissions);
    }

    [Fact]
    public void OperationalProfiles_NeverGrantTeamOrWildcard()
    {
        // Sócio à parte: comercial, financeiro, suporte e auditoria continuam
        // sendo recortes do painel — se um deles ganhar o curinga ou a gestão da
        // equipe, o recorte deixou de existir na prática.
        var restricted = PlatformAccessProfiles.All.Values
            .Where(profile => profile.Selectable && profile.Key != PlatformAccessProfiles.Partner)
            .ToArray();

        Assert.NotEmpty(restricted);
        Assert.All(restricted, profile =>
        {
            Assert.DoesNotContain(PlatformPermission.Team, profile.Permissions);
            Assert.DoesNotContain(PlatformPermission.All, profile.Permissions);
        });
    }

    [Fact]
    public void EffectivePermissions_FollowTheProfile_NotTheStoredSnapshot()
    {
        // Conta antiga: o JSON gravado no convite não tem a gestão de equipe.
        var stale = PlatformAccessProfiles.Serialize([PlatformPermission.Dashboard]);

        var granted = PlatformAccessProfiles.EffectivePermissions(
            isPrimaryOwner: false, PlatformAccessProfiles.Partner, stale);

        Assert.Contains(PlatformPermission.All, granted);
    }

    [Fact]
    public void EffectivePermissions_FallBackToSnapshot_WhenProfileKeyIsUnknown()
    {
        var stored = PlatformAccessProfiles.Serialize([PlatformPermission.Dashboard, PlatformPermission.Logs]);

        var granted = PlatformAccessProfiles.EffectivePermissions(
            isPrimaryOwner: false, "perfil_que_nao_existe_mais", stored);

        Assert.Equal(new[] { PlatformPermission.Dashboard, PlatformPermission.Logs }, granted);
    }

    [Fact]
    public void EffectivePermissions_IgnoreANonSelectableProfileOnAnOrdinaryAccount()
    {
        var stored = PlatformAccessProfiles.Serialize([PlatformPermission.Dashboard]);

        // Conta comum carregando a chave da conta raiz: não pode virar curinga.
        var granted = PlatformAccessProfiles.EffectivePermissions(
            isPrimaryOwner: false, PlatformAccessProfiles.Primary, stored);

        Assert.DoesNotContain(PlatformPermission.All, granted);
        Assert.Equal(new[] { PlatformPermission.Dashboard }, granted);
    }

    [Fact]
    public void EffectivePermissions_GiveThePrimaryOwnerTheWildcard()
    {
        var granted = PlatformAccessProfiles.EffectivePermissions(isPrimaryOwner: true, null, null);

        Assert.Equal(new[] { PlatformPermission.All }, granted);
    }

    [Fact]
    public void PasswordResetTokens_AreHashedBeforePersistence()
    {
        const string token = "convite-super-seguro";
        var hash = AuthService.HashOpaqueToken(token);

        Assert.NotEqual(token, hash);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, AuthService.HashOpaqueToken(token));
    }

    /// <summary>
    /// Todo controller PlatformOwnerOnly do assembly, e não uma lista escrita à
    /// mão: um controller novo que esquecesse a permissão granular ficaria aberto
    /// a qualquer integrante da equipe — auditoria inclusive — porque
    /// PlatformAccessMiddleware só age onde existe o atributo.
    /// </summary>
    public static TheoryData<Type> PlatformOwnerControllers()
    {
        var data = new TheoryData<Type>();
        var controllers = typeof(PlatformController).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>()
                .Any(attribute => attribute.Policy == "PlatformOwnerOnly"))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var controller in controllers) data.Add(controller);
        Assert.NotEmpty(data);
        return data;
    }

    [Fact]
    public void CoverageValidator_ReportsAPlatformRouteWithoutAPermission()
    {
        var endpoint = RouteEndpoint("api/platform/nova-area",
            new AuthorizeAttribute { Policy = "PlatformOwnerOnly" });

        Assert.Contains(
            PlatformPermissionEndpointValidator.FindUnclassified([endpoint]),
            value => value.Contains("api/platform/nova-area"));
    }

    [Fact]
    public void CoverageValidator_AcceptsAPlatformRouteThatDeclaresOne()
    {
        var endpoint = RouteEndpoint("api/platform/nova-area",
            new AuthorizeAttribute { Policy = "PlatformOwnerOnly" },
            new RequirePlatformPermissionAttribute(PlatformPermission.Dashboard));

        Assert.Empty(PlatformPermissionEndpointValidator.FindUnclassified([endpoint]));
    }

    [Fact]
    public async Task EveryMappedPlatformRoute_IsClassified()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(PlatformController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints);

        Assert.Empty(PlatformPermissionEndpointValidator.FindUnclassified(endpoints));
    }

    [Theory]
    [MemberData(nameof(PlatformOwnerControllers))]
    public void PlatformEndpoints_DeclareGranularPermission(Type controllerType)
    {
        var classRequirement = controllerType.GetCustomAttributes(typeof(RequirePlatformPermissionAttribute), true).Any();
        var actions = controllerType.GetMethods()
            .Where(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), true).Any())
            .ToArray();

        Assert.NotEmpty(actions);
        Assert.All(actions, action => Assert.True(
            classRequirement || action.GetCustomAttributes(typeof(RequirePlatformPermissionAttribute), true).Any(),
            $"{controllerType.Name}.{action.Name} precisa declarar RequirePlatformPermission."));
    }

    [Theory]
    [InlineData("same-site")]
    [InlineData("cross-site")]
    public async Task AuthenticatedUnsafeRequests_FromOtherOrigins_AreBlocked(string fetchSite)
    {
        var nextCalled = false;
        var middleware = new BrowserRequestGuardMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = AuthenticatedPost(fetchSite);

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUnsafeRequests_FromSameOrigin_AreAllowed()
    {
        var nextCalled = false;
        var middleware = new BrowserRequestGuardMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = AuthenticatedPost("same-origin");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    private static RouteEndpoint RouteEndpoint(string route, params object[] metadata)
    {
        var builder = new RouteEndpointBuilder(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(route),
            0) { DisplayName = route };
        foreach (var item in metadata) builder.Metadata.Add(item);
        return (RouteEndpoint)builder.Build();
    }

    private static DefaultHttpContext AuthenticatedPost(string fetchSite)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/platform/tenants";
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers["Sec-Fetch-Site"] = fetchSite;
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test"));
        return context;
    }
}
