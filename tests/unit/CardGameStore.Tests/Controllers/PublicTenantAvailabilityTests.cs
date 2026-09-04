using CardGameStore.Controllers;
using CardGameStore.Multitenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text.Json;

namespace CardGameStore.Tests.Controllers;

public class PublicTenantAvailabilityTests
{
    [Fact]
    public async Task PublicDirectory_OnlyListsOptedInActiveTenantsWithLogo()
    {
        using var catalog = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        catalog.Tenants.AddRange(
            new Tenant { Slug = "publica", SchemaName = "tenant_publica", Status = TenantStatus.Active, IsPubliclyListed = true, LogoUrl = "/uploads/publica.png", DisplayName = "Loja Pública" },
            new Tenant { Slug = "sem-opt-in", SchemaName = "tenant_sem_opt_in", Status = TenantStatus.Active, IsPubliclyListed = false, LogoUrl = "/uploads/privada.png" },
            new Tenant { Slug = "sem-logo", SchemaName = "tenant_sem_logo", Status = TenantStatus.Active, IsPubliclyListed = true },
            new Tenant { Slug = "suspensa", SchemaName = "tenant_suspensa", Status = TenantStatus.Suspended, IsPubliclyListed = true, LogoUrl = "/uploads/suspensa.png" });
        await catalog.SaveChangesAsync();

        var controller = new PublicDirectoryController(catalog, Mock.Of<IServiceScopeFactory>());

        var response = Assert.IsType<OkObjectResult>(await controller.ListTenants());
        var tenants = Assert.IsAssignableFrom<IEnumerable<PublicTenantDto>>(response.Value).ToArray();

        var tenant = Assert.Single(tenants);
        Assert.Equal("publica", tenant.Slug);
        Assert.Equal("/uploads/publica.png", tenant.LogoUrl);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableTenant_HasExplicitBusinessCode_WithoutOpeningTenantDatabase(bool suspended)
    {
        using var catalog = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        if (suspended)
        {
            catalog.Tenants.Add(new Tenant { Slug = "missing", SchemaName = "tenant_missing", Status = TenantStatus.Suspended });
            await catalog.SaveChangesAsync();
        }
        var scopes = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        var controller = new PublicDirectoryController(catalog, scopes.Object);

        var response = Assert.IsType<NotFoundObjectResult>(await controller.GetSiteIcons("missing"));

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("tenant_unavailable",
            JsonSerializer.SerializeToElement(response.Value).GetProperty("errorCode").GetString());
        scopes.Verify(s => s.CreateScope(), Times.Never);
    }
}
