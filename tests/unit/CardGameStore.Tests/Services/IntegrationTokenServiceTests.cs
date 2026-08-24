using System.IdentityModel.Tokens.Jwt;
using CardGameStore.Configuration;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Security;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CardGameStore.Tests.Services;

public sealed class IntegrationTokenServiceTests
{
    private static CatalogDbContext CreateCatalog() => new(
        new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IntegrationTokenService CreateService(CatalogDbContext catalog) => new(
        catalog,
        Options.Create(new JwtSettings
        {
            SecretKey = "integration-test-secret-key-32-bytes-minimum",
            Issuer = "tests",
            Audience = "tests",
            IntegrationTokenExpirationMinutes = 15,
        }));

    [Fact]
    public async Task CreateAndIssue_EmitsTenantBoundScopedTokenWithoutPersistingSecret()
    {
        await using var catalog = CreateCatalog();
        var tenantId = Guid.NewGuid();
        catalog.Tenants.Add(new Tenant { Id = tenantId, Slug = "softnerd", SchemaName = "tenant_softnerd" });
        await catalog.SaveChangesAsync();
        var service = CreateService(catalog);

        var (client, secret) = await service.CreateAsync(
            tenantId, "Soft Nerd", [IntegrationScope.FiscalRead, IntegrationScope.FinanceRead], default);
        var response = await service.IssueAsync(tenantId, new IntegrationTokenRequest(
            IntegrationClaim.TokenTypeValue, client.ClientId, secret), default);

        response.Should().NotBeNull();
        client.SecretHash.Should().NotBe(secret);
        BCrypt.Net.BCrypt.Verify(secret, client.SecretHash).Should().BeTrue();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(response!.AccessToken);
        jwt.Claims.Single(claim => claim.Type == TenantConstants.TenantIdClaimType).Value.Should().Be(tenantId.ToString());
        jwt.Claims.Single(claim => claim.Type == IntegrationClaim.TokenType).Value.Should().Be(IntegrationClaim.TokenTypeValue);
        jwt.Claims.Single(claim => claim.Type == IntegrationClaim.Scope).Value
            .Should().Contain(IntegrationScope.FiscalRead).And.Contain(IntegrationScope.FinanceRead);
        response.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Issue_RejectsSameCredentialOnAnotherTenantHost()
    {
        await using var catalog = CreateCatalog();
        var tenantId = Guid.NewGuid();
        catalog.Tenants.Add(new Tenant { Id = tenantId, Slug = "a", SchemaName = "tenant_a" });
        await catalog.SaveChangesAsync();
        var service = CreateService(catalog);
        var (client, secret) = await service.CreateAsync(
            tenantId, "Soft Nerd", [IntegrationScope.FiscalRead], default);

        var response = await service.IssueAsync(Guid.NewGuid(), new IntegrationTokenRequest(
            IntegrationClaim.TokenTypeValue, client.ClientId, secret), default);

        response.Should().BeNull();
    }

    [Fact]
    public async Task Rotate_InvalidatesOldSecretAndIncrementsCredentialVersion()
    {
        await using var catalog = CreateCatalog();
        var tenantId = Guid.NewGuid();
        catalog.Tenants.Add(new Tenant { Id = tenantId, Slug = "a", SchemaName = "tenant_a" });
        await catalog.SaveChangesAsync();
        var service = CreateService(catalog);
        var (client, oldSecret) = await service.CreateAsync(
            tenantId, "Soft Nerd", [IntegrationScope.FiscalRead], default);

        var (_, newSecret) = await service.RotateAsync(tenantId, client.Id, default);

        (await service.IssueAsync(tenantId, new IntegrationTokenRequest(
            IntegrationClaim.TokenTypeValue, client.ClientId, oldSecret), default)).Should().BeNull();
        (await service.IssueAsync(tenantId, new IntegrationTokenRequest(
            IntegrationClaim.TokenTypeValue, client.ClientId, newSecret), default)).Should().NotBeNull();
        client.CredentialVersion.Should().Be(2);
    }

    [Fact]
    public void ValidateScopes_RejectsUnknownOrEmptyScopes()
    {
        var unknown = () => IntegrationTokenService.ValidateScopes(["admin.all"]);
        var empty = () => IntegrationTokenService.ValidateScopes([]);

        unknown.Should().Throw<ArgumentException>();
        empty.Should().Throw<ArgumentException>();
    }
}
