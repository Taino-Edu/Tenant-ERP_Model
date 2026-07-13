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
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
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
            .Where(t => t.Status == TenantStatus.Active)
            .OrderBy(t => t.DisplayName ?? t.Slug)
            .Select(t => new PublicTenantDto
            {
                Slug        = t.Slug,
                DisplayName = t.DisplayName ?? t.Slug,
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

        if (tenant is null) return NotFound();

        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

        var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cfg = await db.SiteConfigs.FindAsync(SiteConfig.SingletonId);

        return Ok(new
        {
            FaviconUrl = cfg?.FaviconUrl,
            PwaIconUrl = cfg?.PwaIconUrl,
            SiteName   = cfg?.SiteName,
            UpdatedAt  = cfg?.UpdatedAt,
        });
    }
}

public class PublicTenantDto
{
    public string  Slug        { get; init; } = string.Empty;
    public string  DisplayName { get; init; } = string.Empty;
    public string? LogoUrl     { get; init; }
}
