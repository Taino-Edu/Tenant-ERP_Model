using System.Security.Claims;
using CardGameStore.Security;
using CardGameStore.Multitenancy;
using CardGameStore.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Moq;

namespace CardGameStore.Tests.Security;

public class PublicAccountSecurityTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Operator", false)]
    [InlineData("Integration", false)]
    [InlineData("Customer", false)]
    public async Task Mcp_OnlyAdminIsAllowed(string role, bool expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        using var sp = services.BuildServiceProvider();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "test"));
        var result = await sp.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, McpAccess.Policy);
        result.Succeeded.Should().Be(expected);
    }

    [Theory]
    [InlineData("Customer", true, true)]
    [InlineData("Customer", false, false)]
    [InlineData("Admin", true, false)]
    public void CustomerSession_MustBelongToCurrentTenant(string role, bool sameTenant, bool allowed)
    {
        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(TenantConstants.TenantIdClaimType, (sameTenant ? tenant : Guid.NewGuid()).ToString())
        }, "test"));
        CustomerSessionIdentity.Resolve(user, tenant).Should().Be(allowed ? id : null);
    }

    [Fact]
    public async Task SignalRFailure_DoesNotReportCommittedOperationAsFailure()
    {
        var client = new Mock<IClientProxy>();
        client.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("connection lost"));
        var proxy = new BestEffortClientProxy(client.Object, NullLogger.Instance);
        await FluentActions.Awaiting(() => proxy.SendCoreAsync("ComandaOpened", new object[] { "id" }))
            .Should().NotThrowAsync();
    }
}
