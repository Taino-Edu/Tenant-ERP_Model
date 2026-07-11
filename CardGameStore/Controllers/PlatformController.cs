// =============================================================================
// PlatformController.cs — Gestão de tenants pelo dono da plataforma.
//
// GET   /api/platform/tenants            → lista todos os tenants
// POST  /api/platform/tenants            → provisiona um tenant novo
// PATCH /api/platform/tenants/{id}/status → suspende/reativa um tenant
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/platform")]
[Authorize(Policy = "PlatformOwnerOnly")]
public class PlatformController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    private readonly ITenantProvisioningService _provisioning;
    private readonly ILogger<PlatformController> _logger;

    public PlatformController(
        CatalogDbContext catalog,
        ITenantProvisioningService provisioning,
        ILogger<PlatformController> logger)
    {
        _catalog      = catalog;
        _provisioning = provisioning;
        _logger       = logger;
    }

    private static TenantSummaryDto ToDto(Tenant t) => new()
    {
        Id             = t.Id,
        Slug           = t.Slug,
        SchemaName     = t.SchemaName,
        Status         = t.Status.ToString(),
        CreatedAt      = t.CreatedAt,
        PlanName       = t.PlanName,
        PaymentStatus  = t.PaymentStatus.ToString(),
        EnabledModules = t.EnabledModules,
    };

    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants()
    {
        var tenants = await _catalog.Tenants
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(tenants.Select(ToDto));
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var tenant = await _provisioning.ProvisionAsync(request.Slug, request.AdminEmail, request.AdminPassword);
            return CreatedAtAction(nameof(ListTenants), ToDto(tenant));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada ao provisionar tenant '{Slug}'.", request.Slug);
            return StatusCode(500, new { Message = "Falha ao provisionar o tenant. Tente novamente." });
        }
    }

    [HttpPatch("tenants/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTenantStatusRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Enum.TryParse<TenantStatus>(request.Status, out var status))
            return BadRequest(new { Message = $"Status inválido: '{request.Status}'." });

        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        tenant.Status = status;
        await _catalog.SaveChangesAsync();

        return Ok(ToDto(tenant));
    }

    [HttpPatch("tenants/{id:guid}/billing")]
    public async Task<IActionResult> UpdateBilling(Guid id, [FromBody] UpdateTenantBillingRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Enum.TryParse<TenantPaymentStatus>(request.PaymentStatus, out var paymentStatus))
            return BadRequest(new { Message = $"Status de pagamento inválido: '{request.PaymentStatus}'." });

        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        tenant.PlanName       = request.PlanName;
        tenant.PaymentStatus  = paymentStatus;
        tenant.EnabledModules = request.EnabledModules;
        await _catalog.SaveChangesAsync();

        return Ok(ToDto(tenant));
    }
}
