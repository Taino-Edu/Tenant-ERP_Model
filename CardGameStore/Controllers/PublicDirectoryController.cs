// =============================================================================
// PublicDirectoryController.cs — Diretório público de lojas ativas, pro site
// institucional listar/linkar as lojas da plataforma. SEM autenticação.
//
// Propositalmente um controller à parte de PlatformController (que é
// PlatformOwnerOnly e devolve Tenant inteiro, com SchemaName/PaymentStatus/
// EnabledModules) — a estreiteza deste controller (só projeta 3 campos
// seguros) É o controle de segurança, não um [AllowAnonymous] colado ao lado
// de endpoints sensíveis.
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
[Produces("application/json")]
public class PublicDirectoryController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    private readonly IServiceScopeFactory _scopeFactory;

    public PublicDirectoryController(CatalogDbContext catalog, IServiceScopeFactory scopeFactory)
    {
        _catalog      = catalog;
        _scopeFactory = scopeFactory;
    }

    // ── GET /api/public/tenants ────────────────────────────────────────────────
    /// <summary>
    /// Lista as lojas ativas da plataforma (slug, nome de exibição, logo) — endpoint
    /// público, pro site institucional listar/linkar as lojas. Só projeta campos
    /// já públicos de propósito (nunca SchemaName/PaymentStatus/EnabledModules).
    /// </summary>
    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants()
    {
        var tenants = await _catalog.Tenants
            .Where(t => t.Status == TenantStatus.Active
                && t.IsPubliclyListed
                && t.LogoUrl != null
                && t.LogoUrl != "")
            .OrderBy(t => t.DisplayName ?? t.Slug)
            .Select(t => new PublicTenantDto
            {
                Slug        = t.Slug,
                DisplayName = t.DisplayName == SiteConfig.LegacyDefaultSiteName
                    ? SiteConfig.DefaultSiteName
                    : t.DisplayName ?? t.Slug,
                LogoUrl     = t.LogoUrl,
            })
            .ToListAsync();

        return Ok(tenants);
    }

    // ── GET /api/public/site-icons?slug=loja-final ────────────────────────────
    // Usado só pelo SSR do Next.js (generateMetadata/manifest.ts) pra buscar
    // favicon/ícone de PWA/nome do tenant sem depender do header Host — fetch()
    // (undici, usado pelo Next.js server-side) ignora silenciosamente uma
    // tentativa de sobrescrever o header Host via `headers`, já que é um
    // "forbidden header name" do próprio Fetch spec. Em vez de brigar com isso,
    // recebe o slug como query param comum (dado já público, aparece na URL de
    // qualquer loja) e resolve o tenant no catálogo, sem tocar em Host nenhum.
    // Só devolve os mesmos campos já públicos via GET /api/site-config
    // (favicon/PWA icon/nome) — nada sensível, mesmo espírito de ListTenants.
    /// <summary>
    /// Favicon/ícone de PWA/nome do tenant, resolvidos pelo slug (não pelo header
    /// Host) — usado pelo SSR do Next.js, que não consegue sobrescrever o header
    /// Host no fetch(). Endpoint público: mesmos campos já expostos em
    /// GET /api/site-config, nada sensível.
    /// </summary>
    /// <param name="slug">Slug da loja (mesmo valor usado no subdomínio). 404 se não existir ou estiver inativa.</param>
    [HttpGet("site-icons")]
    public async Task<IActionResult> GetSiteIcons([FromQuery] string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return BadRequest();

        var tenant = await _catalog.Tenants
            .Where(t => t.Slug == slug.Trim().ToLowerInvariant() && t.Status == TenantStatus.Active)
            .Select(t => new { t.Id, t.SchemaName, t.EnabledModules })
            .FirstOrDefaultAsync();

        if (tenant is null) return NotFound(new { errorCode = "tenant_unavailable" });

        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

        var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cfg = await db.SiteConfigs.FindAsync(SiteConfig.SingletonId);

        return Ok(new
        {
            FaviconUrl   = cfg?.FaviconUrl,
            PwaIconUrl   = cfg?.PwaIconUrl,
            SiteName     = SiteConfig.ResolveSiteName(cfg?.SiteName),
            HeroSubtitle = cfg?.HeroSubtitle,
            AddressLine  = cfg?.AddressLine,
            UpdatedAt    = cfg?.UpdatedAt,
        });
    }

    // ── GET /api/public/product?slug=loja-final&id=guid ───────────────────────
    // Mesmo motivo do site-icons acima: usado pelo generateMetadata da página de
    // produto (app/produtos/[id]/page.tsx) pra montar <title>/OG/JSON-LD com
    // nome, preço e imagem reais do produto, sem depender do header Host. Mesmos
    // campos já expostos anonimamente em GET /api/product/{id} (ProductPublicDto,
    // sem custo/margem) — só resolvido por slug em vez de Host.
    /// <summary>
    /// Produto (nome, descrição, preço, imagem) pro tenant resolvido pelo slug —
    /// usado pelo SSR do Next.js pra montar metadados de SEO da página de
    /// produto. Mesmos campos já expostos em GET /api/product/{id}, nada sensível.
    /// </summary>
    /// <param name="slug">Slug da loja (mesmo valor usado no subdomínio).</param>
    /// <param name="id">Id do produto.</param>
    [HttpGet("product")]
    [ProducesResponseType(typeof(ProductPublicDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPublicProduct([FromQuery] string slug, [FromQuery] Guid id)
    {
        if (string.IsNullOrWhiteSpace(slug)) return BadRequest();

        var tenant = await _catalog.Tenants
            .Where(t => t.Slug == slug.Trim().ToLowerInvariant() && t.Status == TenantStatus.Active)
            .Select(t => new { t.Id, t.SchemaName, t.EnabledModules })
            .FirstOrDefaultAsync();

        if (tenant is null) return NotFound();

        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

        var service = scope.ServiceProvider.GetRequiredService<IProductService>();
        var product = await service.GetByIdAsync(id);

        return product is null ? NotFound() : Ok(ProductPublicDto.FromEntity(product));
    }

    // ── GET /api/public/sitemap?slug=loja-final ───────────────────────────────
    // Alimenta o app/sitemap.ts do Next.js. Sem ele, o sitemap de uma vitrine
    // só conseguia listar "/" e "/produtos": as páginas de produto existiam,
    // mas o buscador só chegava nelas seguindo link, o que na prática significa
    // catálogo indexado devagar e de forma incompleta.
    //
    // Devolve APENAS id e data de atualização — nada de nome, preço ou estoque.
    // Um sitemap não precisa de mais que isso, e este endpoint é anônimo e
    // enumerável por natureza (é a lista completa do catálogo público de uma
    // loja): quanto menos campo ele projeta, menos serve para raspagem.
    /// <summary>
    /// Ids e datas dos produtos públicos da loja, para montar o sitemap.xml.
    /// Só produtos ativos e marcados para aparecer no site.
    /// </summary>
    /// <param name="slug">Slug da loja (mesmo valor usado no subdomínio).</param>
    [HttpGet("sitemap")]
    [ProducesResponseType(typeof(IEnumerable<PublicSitemapEntryDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPublicSitemap([FromQuery] string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return BadRequest();

        var tenant = await _catalog.Tenants
            .Where(t => t.Slug == slug.Trim().ToLowerInvariant() && t.Status == TenantStatus.Active)
            .Select(t => new { t.Id, t.SchemaName, t.EnabledModules })
            .FirstOrDefaultAsync();

        if (tenant is null) return NotFound();

        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // O teto de 5.000 não é sobre o buscador (o limite do protocolo é
        // 50.000 por arquivo): é sobre esta resposta, que é anônima e sem
        // paginação. Catálogo maior que isso precisa de sitemap index, e aí a
        // conversa é outra — melhor cortar do que servir um JSON de vários MB
        // para quem pedir.
        var produtos = await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.ShowOnSite)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(5000)
            .Select(p => new PublicSitemapEntryDto { Id = p.Id, UpdatedAt = p.UpdatedAt })
            .ToListAsync();

        return Ok(produtos);
    }
}

public class PublicSitemapEntryDto
{
    public Guid     Id        { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public class PublicTenantDto
{
    public string  Slug        { get; init; } = string.Empty;
    public string  DisplayName { get; init; } = string.Empty;
    public string? LogoUrl     { get; init; }
}
