// =============================================================================
// PlatformControllerDomainTests.cs — Testa PATCH /api/platform/tenants/{id}/domain
// (cadastro de domínio próprio / BYO domain): validação de formato, colisão
// com outro tenant, colisão com o domínio raiz da plataforma, tolerância a
// URL colada inteira, e limpeza do campo.
// =============================================================================

using Xunit;
using CardGameStore.Controllers;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CardGameStore.Tests.Controllers;

public class PlatformControllerDomainTests
{
    private static CatalogDbContext CreateCatalogDb() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PlatformController CreateController(CatalogDbContext catalog, string? rootDomain = "2esysten.com.br")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(rootDomain is null ? [] : new Dictionary<string, string?> { ["Multitenancy:RootDomain"] = rootDomain })
            .Build();

        return new PlatformController(
            catalog,
            new Mock<ITenantProvisioningService>().Object,
            NullLogger<PlatformController>.Instance,
            new Mock<IServiceScopeFactory>().Object,
            new MemoryCache(new MemoryCacheOptions()),
            config);
    }

    private static async Task<Guid> SeedTenantAsync(CatalogDbContext db, string slug, string? customDomain = null)
    {
        var id = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = id, Slug = slug, SchemaName = "tenant_" + slug.Replace('-', '_'), Status = TenantStatus.Active, CustomDomain = customDomain });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task UpdateCustomDomain_DominioValido_Salva()
    {
        var db = CreateCatalogDb();
        var id = await SeedTenantAsync(db, "loja-a");
        var controller = CreateController(db);

        var result = await controller.UpdateCustomDomain(id, new UpdateTenantDomainRequest { CustomDomain = "minhaloja.com.br" });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Tenants.FindAsync(id))!.CustomDomain.Should().Be("minhaloja.com.br");
    }

    [Fact]
    public async Task UpdateCustomDomain_TolerouUrlColada_ExtraiSoOHost()
    {
        var db = CreateCatalogDb();
        var id = await SeedTenantAsync(db, "loja-b");
        var controller = CreateController(db);

        await controller.UpdateCustomDomain(id, new UpdateTenantDomainRequest { CustomDomain = "https://Minhaloja.com.br/alguma/coisa" });

        (await db.Tenants.FindAsync(id))!.CustomDomain.Should().Be("minhaloja.com.br");
    }

    [Fact]
    public async Task UpdateCustomDomain_VazioOuNull_LimpaOCampo()
    {
        var db = CreateCatalogDb();
        var id = await SeedTenantAsync(db, "loja-c", customDomain: "antigo.com.br");
        var controller = CreateController(db);

        var result = await controller.UpdateCustomDomain(id, new UpdateTenantDomainRequest { CustomDomain = "" });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Tenants.FindAsync(id))!.CustomDomain.Should().BeNull();
    }

    [Theory]
    [InlineData("dominio invalido com espaco")]
    [InlineData("sem-ponto")]
    [InlineData("-comeca-com-hifen.com")]
    public async Task UpdateCustomDomain_FormatoInvalido_RetornaBadRequest(string invalido)
    {
        var db = CreateCatalogDb();
        var id = await SeedTenantAsync(db, "loja-d");
        var controller = CreateController(db);

        var result = await controller.UpdateCustomDomain(id, new UpdateTenantDomainRequest { CustomDomain = invalido });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateCustomDomain_JaEmUsoPorOutroTenant_RetornaBadRequest()
    {
        var db = CreateCatalogDb();
        await SeedTenantAsync(db, "loja-e", customDomain: "jaexiste.com.br");
        var idNovo = await SeedTenantAsync(db, "loja-f");
        var controller = CreateController(db);

        var result = await controller.UpdateCustomDomain(idNovo, new UpdateTenantDomainRequest { CustomDomain = "jaexiste.com.br" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData("2esysten.com.br")]
    [InlineData("loja-x.2esysten.com.br")]
    public async Task UpdateCustomDomain_ColideComDominioDaPlataforma_RetornaBadRequest(string dominio)
    {
        var db = CreateCatalogDb();
        var id = await SeedTenantAsync(db, "loja-g");
        var controller = CreateController(db, rootDomain: "2esysten.com.br");

        var result = await controller.UpdateCustomDomain(id, new UpdateTenantDomainRequest { CustomDomain = dominio });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateCustomDomain_TenantInexistente_RetornaNotFound()
    {
        var db = CreateCatalogDb();
        var controller = CreateController(db);

        var result = await controller.UpdateCustomDomain(Guid.NewGuid(), new UpdateTenantDomainRequest { CustomDomain = "x.com.br" });

        result.Should().BeOfType<NotFoundResult>();
    }
}
