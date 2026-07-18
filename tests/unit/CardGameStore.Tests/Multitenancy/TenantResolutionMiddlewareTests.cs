// =============================================================================
// TenantResolutionMiddlewareTests.cs — Testa a resolução de tenant por Host:
// subdomínio do RootDomain (ExtractSlug, já existia sem cobertura) e o
// domínio próprio (CustomDomain, novo nesta sessão) como caminho alternativo
// quando o host não é um subdomínio reconhecido.
// =============================================================================

using Xunit;
using CardGameStore.Multitenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CardGameStore.Tests.Multitenancy;

public class TenantResolutionMiddlewareTests
{
    // ── ExtractSlug (função pura) ────────────────────────────────────────────

    [Theory]
    [InlineData("loja-maikon.2esysten.com.br", "2esysten.com.br", "loja-maikon")]
    [InlineData("2esysten.com.br",             "2esysten.com.br", null)]        // domínio raiz — sem slug
    [InlineData("a.b.2esysten.com.br",         "2esysten.com.br", null)]        // multi-nível — não é slug válido
    [InlineData("179.197.67.64",               "2esysten.com.br", null)]        // IP puro
    [InlineData("localhost",                   "2esysten.com.br", null)]
    [InlineData("outrodominio.com",            "2esysten.com.br", null)]        // domínio de terceiro — não é subdomínio
    [InlineData("loja.2esysten.com.br",        null,               null)]       // sem RootDomain configurado
    public void ExtractSlug_CasosDeHost(string host, string? rootDomain, string? esperado)
    {
        TenantResolutionMiddleware.ExtractSlug(host, rootDomain).Should().Be(esperado);
    }

    [Fact]
    public void ExtractSlug_CaseInsensitive()
    {
        TenantResolutionMiddleware.ExtractSlug("Loja-Maikon.2ESYSTEN.COM.BR", "2esysten.com.br")
            .Should().Be("loja-maikon");
    }

    // ── InvokeAsync — resolução via CustomDomain (BYO domain) ────────────────

    private static CatalogDbContext CreateCatalogDb() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (HttpContext ctx, ITenantContext tenantContext) BuildContext(string host, IServiceProvider services)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Host = new HostString(host);
        return (httpContext, new TenantContext());
    }

    private static TenantResolutionMiddleware CreateMiddleware(RequestDelegate next, string? rootDomain = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(rootDomain is null ? [] : new Dictionary<string, string?> { ["Multitenancy:RootDomain"] = rootDomain })
            .Build();
        return new TenantResolutionMiddleware(next, new MemoryCache(new MemoryCacheOptions()), config);
    }

    [Fact]
    public async Task InvokeAsync_HostBateComCustomDomain_ResolveOTenantCorreto()
    {
        var catalog = CreateCatalogDb();
        var tenantId = Guid.NewGuid();
        catalog.Tenants.Add(new Tenant
        {
            Id = tenantId, Slug = "loja-x", SchemaName = "tenant_loja_x",
            Status = TenantStatus.Active, CustomDomain = "minhaloja.com.br",
        });
        await catalog.SaveChangesAsync();

        var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        var (ctx, tenantContext) = BuildContext("minhaloja.com.br", services);

        var middleware = CreateMiddleware(_ => Task.CompletedTask, rootDomain: "2esysten.com.br");
        await middleware.InvokeAsync(ctx, tenantContext, catalog);

        tenantContext.TenantId.Should().Be(tenantId);
        tenantContext.SchemaName.Should().Be("tenant_loja_x");
    }

    [Fact]
    public async Task InvokeAsync_SlugTemPrioridadeSobreCustomDomain()
    {
        // Um host que bate com o padrão de subdomínio nunca deveria cair na
        // checagem de CustomDomain — evita ambiguidade entre os dois mecanismos.
        var catalog = CreateCatalogDb();
        var idPorSlug = Guid.NewGuid();
        catalog.Tenants.Add(new Tenant { Id = idPorSlug, Slug = "loja-y", SchemaName = "tenant_loja_y", Status = TenantStatus.Active });
        await catalog.SaveChangesAsync();

        var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        var (ctx, tenantContext) = BuildContext("loja-y.2esysten.com.br", services);

        var middleware = CreateMiddleware(_ => Task.CompletedTask, rootDomain: "2esysten.com.br");
        await middleware.InvokeAsync(ctx, tenantContext, catalog);

        tenantContext.TenantId.Should().Be(idPorSlug);
    }

    [Fact]
    public async Task InvokeAsync_CustomDomainDeTenantSuspenso_Retorna403()
    {
        var catalog = CreateCatalogDb();
        catalog.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(), Slug = "loja-z", SchemaName = "tenant_loja_z",
            Status = TenantStatus.Suspended, CustomDomain = "lojasuspensa.com.br",
        });
        await catalog.SaveChangesAsync();

        var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        var (ctx, tenantContext) = BuildContext("lojasuspensa.com.br", services);
        ctx.Response.Body = new MemoryStream();

        var middleware = CreateMiddleware(_ => Task.CompletedTask, rootDomain: "2esysten.com.br");
        await middleware.InvokeAsync(ctx, tenantContext, catalog);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_HostDesconhecido_CaiNoTenantZero()
    {
        var catalog = CreateCatalogDb();
        var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        var (ctx, tenantContext) = BuildContext("dominio-nunca-cadastrado.com", services);

        var middleware = CreateMiddleware(_ => Task.CompletedTask, rootDomain: "2esysten.com.br");
        await middleware.InvokeAsync(ctx, tenantContext, catalog);

        tenantContext.TenantId.Should().Be(TenantConstants.TenantZeroId);
        tenantContext.SchemaName.Should().Be(TenantConstants.TenantZeroSchema);
    }

    [Fact]
    public async Task InvokeAsync_SubdominioInexistente_Retorna404_NaoServeTenantZero()
    {
        // Subdomínio BEM-FORMADO do RootDomain, mas sem tenant no catálogo (typo,
        // loja removida): tem de dar 404, não pode servir a vitrine/login do
        // tenant-zero (schema "public") — ver comentário em InvokeAsync.
        var catalog = CreateCatalogDb();
        var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        var (ctx, tenantContext) = BuildContext("loja-que-nao-existe.2esysten.com.br", services);
        ctx.Response.Body = new MemoryStream();

        var nextChamado = false;
        var middleware = CreateMiddleware(_ => { nextChamado = true; return Task.CompletedTask; }, rootDomain: "2esysten.com.br");
        await middleware.InvokeAsync(ctx, tenantContext, catalog);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        nextChamado.Should().BeFalse("a requisição não pode seguir o pipeline servindo o tenant-zero");
        tenantContext.TenantId.Should().Be(TenantConstants.TenantZeroId, "o contexto não deve ter sido alterado para nenhum tenant real");
    }
}
