using CardGameStore.Multitenancy;
using CardGameStore.Middleware;
using CardGameStore.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

/// <summary>Confirma capacidades contratadas sem acessar o banco operacional do tenant.</summary>
[ApiController]
[Route("api/integrations/capabilities")]
[Authorize(Policy = "AdminOnly")]
[OperatorForbidden]
[Produces("application/json")]
public sealed class IntegrationCapabilitiesController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    private readonly ITenantContext _tenant;

    public IntegrationCapabilitiesController(CatalogDbContext catalog, ITenantContext tenant)
    {
        _catalog = catalog;
        _tenant = tenant;
    }

    [HttpGet("financeiro")]
    [RequireIntegrationScope(IntegrationScope.FinanceRead)]
    public Task<IActionResult> FinanceiroAsync(CancellationToken ct) =>
        BuildAsync("financeiro", ct);

    [HttpGet("fiscal")]
    [RequireIntegrationScope(IntegrationScope.FiscalRead)]
    public Task<IActionResult> FiscalAsync(CancellationToken ct) =>
        BuildAsync("fiscal", ct);

    private async Task<IActionResult> BuildAsync(string capability, CancellationToken ct)
    {
        var tenant = await _catalog.Tenants.AsNoTracking()
            .Where(item => item.Id == _tenant.TenantId)
            .Select(item => new
            {
                item.Id,
                item.Slug,
                Kind = item.Kind.ToString(),
                item.EnabledModules,
            })
            .SingleOrDefaultAsync(ct);

        if (tenant is null) return NotFound();

        return Ok(new
        {
            tenant.Id,
            tenant.Slug,
            tenant.Kind,
            Capability = capability,
            DataResidency = tenant.Kind == nameof(TenantKind.ExternalIntegrated)
                ? "ExternalSystem"
                : "TenantSchema",
            tenant.EnabledModules,
        });
    }
}
