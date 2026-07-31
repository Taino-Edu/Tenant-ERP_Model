using System.Security.Claims;
using CardGameStore.Data;
using CardGameStore.Controllers;
using CardGameStore.Middleware;
using CardGameStore.Models.PostgreSQL;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Middleware;

public class OperatorPermissionMiddlewareTests
{
    [Fact]
    public async Task Allows_permission_that_is_currently_in_profile()
    {
        await using var db = CreateDb();
        var user = await SeedOperator(db, Permissao.Estoque);
        var context = Context(user.Id, new RequireOperatorPermissionAttribute(Permissao.Estoque));
        var called = false;
        var middleware = Middleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, db);

        called.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Denies_missing_permission()
    {
        await using var db = CreateDb();
        var user = await SeedOperator(db, Permissao.Dashboard);
        var context = Context(user.Id, new RequireOperatorPermissionAttribute(Permissao.Financeiro));

        await Middleware(_ => Task.CompletedTask).InvokeAsync(context, db);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Revocation_takes_effect_on_next_request_with_same_jwt()
    {
        await using var db = CreateDb();
        var user = await SeedOperator(db, Permissao.Estoque);
        var middleware = Middleware(_ => Task.CompletedTask);

        var before = Context(user.Id, new RequireOperatorPermissionAttribute(Permissao.Estoque));
        await middleware.InvokeAsync(before, db);
        before.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        user.Perfil!.PermissoesJson = "[]";
        await db.SaveChangesAsync();

        var after = Context(user.Id, new RequireOperatorPermissionAttribute(Permissao.Estoque));
        await middleware.InvokeAsync(after, db);
        after.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Inactive_operator_is_denied()
    {
        await using var db = CreateDb();
        var user = await SeedOperator(db, Permissao.Estoque);
        user.IsActive = false;
        await db.SaveChangesAsync();

        var context = Context(user.Id, new RequireOperatorPermissionAttribute(Permissao.Estoque));
        await Middleware(_ => Task.CompletedTask).InvokeAsync(context, db);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Self_service_does_not_require_a_profile_permission()
    {
        await using var db = CreateDb();
        var context = Context(Guid.NewGuid(), new OperatorSelfServiceAttribute());
        var called = false;

        await Middleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, db);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Explicitly_forbidden_endpoint_is_denied()
    {
        await using var db = CreateDb();
        var user = await SeedOperator(db, Permissao.Fiscal);
        var context = Context(user.Id, new OperatorForbiddenAttribute());

        await Middleware(_ => Task.CompletedTask).InvokeAsync(context, db);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void Coverage_validator_reports_unclassified_operator_route()
    {
        var endpoint = RouteEndpoint(
            "api/new-feature",
            new AuthorizeAttribute { Policy = "AdminOnly" });

        OperatorPermissionEndpointValidator.FindUnclassified([endpoint])
            .Should().ContainSingle(value => value.Contains("api/new-feature"));
    }

    [Fact]
    public void Coverage_validator_accepts_classified_operator_route()
    {
        var endpoint = RouteEndpoint(
            "api/new-feature",
            new AuthorizeAttribute { Policy = "AdminOnly" },
            new RequireOperatorPermissionAttribute(Permissao.Estoque));

        OperatorPermissionEndpointValidator.FindUnclassified([endpoint]).Should().BeEmpty();
    }

    [Fact]
    public async Task All_mapped_controller_routes_have_an_operator_classification()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints);

        OperatorPermissionEndpointValidator.FindUnclassified(endpoints).Should().BeEmpty();
        var finance = endpoints.OfType<RouteEndpoint>().Single(endpoint =>
            endpoint.RoutePattern.RawText?.EndsWith("api/analytics/financeiro", StringComparison.OrdinalIgnoreCase) == true);

        finance.Metadata.GetMetadata<RequireOperatorPermissionAttribute>()!.Permissions
            .Should().Equal(Permissao.Financeiro);
    }

    private static AppDbContext CreateDb() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"operator_permissions_{Guid.NewGuid():N}")
            .Options);

    private static async Task<User> SeedOperator(AppDbContext db, params string[] permissions)
    {
        var profile = new Perfil
        {
            Nome = "Operador",
            PermissoesJson = System.Text.Json.JsonSerializer.Serialize(permissions),
            CriadoPorAdminId = Guid.NewGuid(),
        };
        var user = new User
        {
            Name = "Operador de teste",
            Role = UserRole.Operator,
            IsActive = true,
            Perfil = profile,
            PerfilId = profile.Id,
        };
        db.AddRange(profile, user);
        await db.SaveChangesAsync();
        return user;
    }

    private static DefaultHttpContext Context(Guid userId, params object[] metadata)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.Role, UserRole.Operator),
        ], "test"));
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(metadata.Prepend(new AuthorizeAttribute())),
            "test endpoint"));
        return context;
    }

    private static OperatorPermissionMiddleware Middleware(RequestDelegate next) =>
        new(next, NullLogger<OperatorPermissionMiddleware>.Instance);

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
