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
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Multitenancy;

public class TenantResolutionMiddlewareTests
{
    // ── ExtractSlug (função pura) ────────────────────────────────────────────

    [Theory]
    [InlineData("loja-maikon.3esysten.com.br", "3esysten.com.br", "loja-maikon")]
    [InlineData("3esysten.com.br",             "3esysten.com.br", null)]        // domínio raiz — sem slug
    [InlineData("www.3esysten.com.br",         "3esysten.com.br", null)]        // www pertence à plataforma
    [InlineData("a.b.3esysten.com.br",         "3esysten.com.br", null)]        // multi-nível — não é slug válido
    [InlineData("179.197.67.64",               "3esysten.com.br", null)]        // IP puro
    [InlineData("localhost",                   "3esysten.com.br", null)]
    [InlineData("outrodominio.com",            "3esysten.com.br", null)]        // domínio de terceiro — não é subdomínio
    [InlineData("loja.3esysten.com.br",        null,               null)]       // sem RootDomain configurado
    public void ExtractSlug_CasosDeHost(string host, string? rootDomain, string? esperado)
    {
        TenantResolutionMiddleware.ExtractSlug(host, rootDomain).Should().Be(esperado);
    }

    [Fact]
    public void ExtractSlug_CaseInsensitive()
    {
        TenantResolutionMiddleware.ExtractSlug("Loja-Maikon.3ESYSTEN.COM.BR", "3esysten.com.br")
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

    private static TenantResolutionMiddleware CreateMiddleware(
        RequestDelegate next, string? rootDomain = null, bool rejectUnknownHosts = false)
    {
        var values = new Dictionary<string, string?>
        {
            ["Multitenancy:RootDomain"] = rootDomain,
            ["Multitenancy:RejectUnknownHosts"] = rejectUnknownHosts.ToString(),
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new TenantResolutionMiddleware(
            next,
            new MemoryCache(new MemoryCacheOptions()),
            config,
            NullLogger<TenantResolutionMiddleware>.Instance);
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

        var middleware = CreateMiddleware(_ => Task.CompletedTask, rootDomain: "3esysten.com.br");
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
        var (ctx, tenantContext) = BuildContext("loja-y.3esysten.com.br", services);

        var middleware = CreateMiddleware(_ => Task.CompletedTask, rootDomain: "3esysten.com.br");
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

        var middleware = CreateMiddleware(_ => Task.CompletedTask, rootDomain: "3esysten.com.br");
        await middleware.InvokeAsync(ctx, tenantContext, catalog);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_HostDesconhecido_CaiNoTenantZero()
    {
        var catalog = CreateCatalogDb();
        var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        var (ctx, tenantContext) = BuildContext("dominio-nunca-cadastrado.com", services);

        var middleware = CreateMiddleware(_ => Task.CompletedTask, rootDomain: "3esysten.com.br");
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
        var (ctx, tenantContext) = BuildContext("loja-que-nao-existe.3esysten.com.br", services);
        ctx.Response.Body = new MemoryStream();

        var nextChamado = false;
        var middleware = CreateMiddleware(_ => { nextChamado = true; return Task.CompletedTask; }, rootDomain: "3esysten.com.br");
        await middleware.InvokeAsync(ctx, tenantContext, catalog);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        nextChamado.Should().BeFalse("a requisição não pode seguir o pipeline servindo o tenant-zero");
        tenantContext.TenantId.Should().Be(TenantConstants.TenantZeroId, "o contexto não deve ter sido alterado para nenhum tenant real");
    }

    [Fact]
    public async Task InvokeAsync_HostDesconhecido_ComRejeicaoAtiva_Retorna404()
    {
        var catalog = CreateCatalogDb();
        var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        var (ctx, tenantContext) = BuildContext("host-forjado.example", services);
        ctx.Response.Body = new MemoryStream();

        var nextChamado = false;
        var middleware = CreateMiddleware(
            _ => { nextChamado = true; return Task.CompletedTask; },
            rootDomain: "3esysten.com.br",
            rejectUnknownHosts: true);

        await middleware.InvokeAsync(ctx, tenantContext, catalog);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        nextChamado.Should().BeFalse();
        tenantContext.IsExplicitlySet.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_HealthcheckInterno_ComHostLocal_PermiteTenantZero()
    {
        var catalog = CreateCatalogDb();
        var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        var (ctx, tenantContext) = BuildContext("localhost:5000", services);
        ctx.Request.Path = "/health";

        var nextChamado = false;
        var middleware = CreateMiddleware(
            _ => { nextChamado = true; return Task.CompletedTask; },
            rootDomain: "3esysten.com.br",
            rejectUnknownHosts: true);

        await middleware.InvokeAsync(ctx, tenantContext, catalog);

        nextChamado.Should().BeTrue();
        tenantContext.TenantId.Should().Be(TenantConstants.TenantZeroId);
        tenantContext.IsExplicitlySet.Should().BeTrue();
    }

    [Theory]
    [InlineData("3esysten.com.br")]
    [InlineData("www.3esysten.com.br")]
    public async Task InvokeAsync_HostDaPlataforma_ComRejeicaoAtiva_UsaTenantZero(string host)
    {
        var catalog = CreateCatalogDb();
        var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        var (ctx, tenantContext) = BuildContext(host, services);

        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            rootDomain: "3esysten.com.br",
            rejectUnknownHosts: true);

        await middleware.InvokeAsync(ctx, tenantContext, catalog);

        tenantContext.TenantId.Should().Be(TenantConstants.TenantZeroId);
        tenantContext.SchemaName.Should().Be(TenantConstants.TenantZeroSchema);
        tenantContext.IsExplicitlySet.Should().BeTrue();
    }
}
