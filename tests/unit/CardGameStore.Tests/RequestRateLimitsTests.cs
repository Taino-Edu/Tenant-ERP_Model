using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using CardGameStore.Multitenancy;
using CardGameStore.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardGameStore.Tests;

public sealed class RequestRateLimitsTests : IDisposable
{
    private readonly List<ServiceProvider> _providers = new();

    private DefaultHttpContext Context(string ip = "192.0.2.1", Guid? tenant = null, string? user = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(tenant ?? Guid.Empty, "test", Array.Empty<string>());
        var provider = new ServiceCollection().AddSingleton<ITenantContext>(tenantContext).BuildServiceProvider();
        _providers.Add(provider);
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (user != null)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user) }, "test"));
        return context;
    }

    [Theory]
    [InlineData("auth", 15)]
    [InlineData("api", 200)]
    [InlineData("global", 300)]
    [InlineData("locate-account", 5)]
    [InlineData("comanda-hub", 30)]
    [InlineData("public-lead", 5)]
    [InlineData("public-ai", 10)]
    [InlineData("integration-token", 10)]
    public void ExhaustingOneIpDoesNotBlockAnother(string policy, int permits)
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RequestRateLimits.Partition(context, policy));
        var first = Context();
        using var acquired = limiter.AttemptAcquire(first, permits);
        using var rejected = limiter.AttemptAcquire(first);
        using var other = limiter.AttemptAcquire(Context("192.0.2.2"));
        Assert.True(acquired.IsAcquired);
        Assert.False(rejected.IsAcquired);
        Assert.True(other.IsAcquired);
    }

    [Fact]
    public void ArbitraryForwardingHeadersCannotRotateBucket()
    {
        var context = Context();
        var original = RequestRateLimits.Partition(context, "integration-token").PartitionKey;
        context.Request.Headers["CF-Connecting-IP"] = "198.51.100.1";
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.2";
        Assert.Equal(original, RequestRateLimits.Partition(context, "integration-token").PartitionKey);
        Assert.Equal(RequestRateLimits.ClientIp(Context()), RequestRateLimits.ClientIp(Context("::ffff:192.0.2.1")));
    }

    [Fact]
    public void AuthIsPerTenantIpAndAuthenticatedApiIsPerTenantUser()
    {
        var tenant = Guid.NewGuid();
        Assert.NotEqual(RequestRateLimits.Partition(Context(), "auth").PartitionKey,
            RequestRateLimits.Partition(Context(tenant: tenant), "auth").PartitionKey);
        Assert.NotEqual(RequestRateLimits.TenantUserOrIp(Context(user: "one")), RequestRateLimits.TenantUserOrIp(Context(user: "two")));
        Assert.NotEqual(RequestRateLimits.TenantUserOrIp(Context(user: "one")), RequestRateLimits.TenantUserOrIp(Context(tenant: tenant, user: "one")));
        Assert.Equal(RequestRateLimits.TenantUserOrIp(Context(user: "one")), RequestRateLimits.TenantUserOrIp(Context("192.0.2.2", user: "one")));
        // Busca global de conta não pode contornar o limite trocando de tenant.
        Assert.Equal(RequestRateLimits.Partition(Context(), "locate-account").PartitionKey,
            RequestRateLimits.Partition(Context(tenant: tenant), "locate-account").PartitionKey);
    }

    [Fact]
    public void UnauthenticatedClaimsDoNotCreateUserBuckets()
    {
        var context = Context();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "untrusted") }));
        Assert.Equal(RequestRateLimits.TenantIp(context), RequestRateLimits.TenantUserOrIp(context));
    }

    [Theory]
    [InlineData("locate-account", 3600)]
    [InlineData("public-lead", 900)]
    public async Task RejectionReportsActualWindowAndIsNotCacheable(string policy, int seconds)
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RequestRateLimits.Partition(context, policy));
        var context = Context();
        using var body = new MemoryStream();
        context.Response.Body = body;
        using var acquired = limiter.AttemptAcquire(context, 5);
        using var rejected = limiter.AttemptAcquire(context);
        await RequestRateLimits.RejectAsync(new OnRejectedContext { HttpContext = context, Lease = rejected }, CancellationToken.None);
        Assert.Equal(429, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.InRange(int.Parse(context.Response.Headers["Retry-After"]!), seconds - 5, seconds);
    }

    public void Dispose() { foreach (var provider in _providers) provider.Dispose(); }
}
