using System.Security.Claims;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Security;
using CardGameStore.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Security;

public sealed class IntegrationAccessMiddlewareTests
{
    private static CatalogDbContext CreateCatalog() => new(
        new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ValidClientWithRequiredScope_Passes()
    {
        var result = await ExecuteAsync(IntegrationScope.FinanceRead, IntegrationScope.FinanceRead);
        result.NextCalled.Should().BeTrue();
        result.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task RouteWithoutIntegrationScope_IsDeniedByDefault()
    {
        var result = await ExecuteAsync(null, IntegrationScope.FinanceRead);
        result.NextCalled.Should().BeFalse();
        result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MissingRequiredScope_IsForbidden()
    {
        var result = await ExecuteAsync(IntegrationScope.FiscalWrite, IntegrationScope.FiscalRead);
        result.NextCalled.Should().BeFalse();
        result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task RotatedOrRevokedCredentialVersion_IsUnauthorized()
    {
        var result = await ExecuteAsync(
            IntegrationScope.FiscalRead, IntegrationScope.FiscalRead,
            storedVersion: 2, claimedVersion: 1);
        result.NextCalled.Should().BeFalse();
        result.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void EndpointValidator_RejectsUnknownScope()
    {
        var endpoint = RouteEndpoint("api/test", new AuthorizeAttribute(),
            new RequireIntegrationScopeAttribute("unknown.scope"));

        IntegrationScopeEndpointValidator.FindInvalid([endpoint])
            .Should().ContainSingle(value => value.Contains("escopo desconhecido"));
    }

    [Fact]
    public void EndpointValidator_RejectsAnonymousScopedRoute()
    {
        var endpoint = RouteEndpoint("api/test", new AllowAnonymousAttribute(),
            new RequireIntegrationScopeAttribute(IntegrationScope.FinanceRead));

        IntegrationScopeEndpointValidator.FindInvalid([endpoint])
            .Should().Contain(value => value.Contains("nao pode ser anonima"));
    }

    [Fact]
    public async Task EveryMappedIntegrationRoute_HasAValidDeclaration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(IntegrationClientsController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints);

        IntegrationScopeEndpointValidator.FindInvalid(endpoints).Should().BeEmpty();
    }

    private static async Task<(bool NextCalled, int StatusCode)> ExecuteAsync(
        string? requiredScope,
        string grantedScope,
        int storedVersion = 1,
        int claimedVersion = 1)
    {
        await using var catalog = CreateCatalog();
        var tenantId = Guid.NewGuid();
        var client = new ApiIntegrationClient
        {
            TenantId = tenantId,
            Name = "Soft Nerd",
            ClientId = "ti_test",
            SecretHash = "not-used",
            Scopes = [grantedScope],
            CredentialVersion = storedVersion,
        };
        catalog.ApiIntegrationClients.Add(client);
        await catalog.SaveChangesAsync();

        var metadata = requiredScope is null
            ? new EndpointMetadataCollection()
            : new EndpointMetadataCollection(new RequireIntegrationScopeAttribute(requiredScope));
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "integration-test"));
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, UserRole.Integration),
            new Claim(IntegrationClaim.TokenType, IntegrationClaim.TokenTypeValue),
            new Claim(IntegrationClaim.ClientRecordId, client.Id.ToString()),
            new Claim(IntegrationClaim.CredentialVersion, claimedVersion.ToString()),
            new Claim(IntegrationClaim.Scope, grantedScope),
            new Claim("client_id", client.ClientId),
        ], "Bearer"));
        var tenant = new TenantContext();
        tenant.Set(tenantId, "tenant_softnerd", ["fiscal"]);
        var nextCalled = false;
        var middleware = new IntegrationAccessMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<IntegrationAccessMiddleware>.Instance);

        await middleware.InvokeAsync(context, catalog, tenant);
        return (nextCalled, context.Response.StatusCode);
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
}
