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

    public PublicDirectoryController(CatalogDbContext catalog)
    {
        _catalog = catalog;
    }

    // ── GET /api/public/tenants ────────────────────────────────────────────────
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
}

public class PublicTenantDto
{
    public string  Slug        { get; init; } = string.Empty;
    public string  DisplayName { get; init; } = string.Empty;
    public string? LogoUrl     { get; init; }
}
