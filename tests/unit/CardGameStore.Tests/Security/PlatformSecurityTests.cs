using System.Security.Claims;
using CardGameStore.Security;
using CardGameStore.Services.Implementations;
using CardGameStore.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CardGameStore.Tests.Security;

public sealed class PlatformSecurityTests
{
    [Fact]
    public void SelectableProfiles_NeverGrantTeamOrWildcard()
    {
        var selectable = PlatformAccessProfiles.All.Values.Where(profile => profile.Selectable).ToArray();

        Assert.NotEmpty(selectable);
        Assert.All(selectable, profile =>
        {
            Assert.DoesNotContain(PlatformPermission.Team, profile.Permissions);
            Assert.DoesNotContain(PlatformPermission.All, profile.Permissions);
        });
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

    [Theory]
    [InlineData(typeof(PlatformController))]
    [InlineData(typeof(PlatformBillingController))]
    [InlineData(typeof(ProspectingController))]
    [InlineData(typeof(PlatformTeamController))]
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
