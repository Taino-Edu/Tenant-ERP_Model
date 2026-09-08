using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace CardGameStore.Tests.Security;

public sealed class SessionVersionMiddlewareTests
{
    [Fact]
    public async Task MatchingActiveSession_Passes()
    {
        var result = await ExecuteAsync(storedVersion: 3, claimedVersion: 3, active: true);
        result.NextCalled.Should().BeTrue();
        result.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData(4, 3, true)]
    [InlineData(3, 3, false)]
    public async Task RevokedOrInactiveSession_IsUnauthorized(int storedVersion, int claimedVersion, bool active)
    {
        var result = await ExecuteAsync(storedVersion, claimedVersion, active);
        result.NextCalled.Should().BeFalse();
        result.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task SpecialTokenWithoutSessionVersion_PassesWithoutUserLookup()
    {
        using var db = TestDbFactory.Create(nameof(SpecialTokenWithoutSessionVersion_PassesWithoutUserLookup));
        var context = Context(Guid.NewGuid(), null);
        var nextCalled = false;
        var middleware = new SessionVersionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, db);

        nextCalled.Should().BeTrue();
    }

    private static async Task<(bool NextCalled, int StatusCode)> ExecuteAsync(
        int storedVersion, int claimedVersion, bool active)
    {
        using var db = TestDbFactory.Create(nameof(SessionVersionMiddlewareTests));
        var user = new User
        {
            Name = "Sessão", PasswordHash = "hash", Role = UserRole.Admin,
            IsActive = active, SessionVersion = storedVersion,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var context = Context(user.Id, claimedVersion);
        var nextCalled = false;
        var middleware = new SessionVersionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, db);
        return (nextCalled, context.Response.StatusCode);
    }

    private static DefaultHttpContext Context(Guid userId, int? version)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, userId.ToString()) };
        if (version.HasValue)
            claims.Add(new Claim("session_version", version.Value.ToString()));
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")),
            Response = { Body = new MemoryStream() },
        };
    }
}
