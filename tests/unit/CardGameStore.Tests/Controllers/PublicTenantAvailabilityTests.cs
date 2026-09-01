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
