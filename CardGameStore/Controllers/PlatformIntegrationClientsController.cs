using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Security;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

/// <summary>Gerencia credenciais técnicas de um tenant pelo domínio da plataforma.</summary>
[ApiController]
[Route("api/platform/tenants/{tenantId:guid}/integration-clients")]
[Authorize(Policy = "PlatformOwnerOnly")]
[Produces("application/json")]
public sealed class PlatformIntegrationClientsController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    private readonly IntegrationTokenService _tokens;
    private readonly ILogger<PlatformIntegrationClientsController> _logger;

    public PlatformIntegrationClientsController(
        CatalogDbContext catalog,
        IntegrationTokenService tokens,
        ILogger<PlatformIntegrationClientsController> logger)
    {
        _catalog = catalog;
        _tokens = tokens;
        _logger = logger;
    }

    [HttpGet]
    [RequirePlatformPermission(PlatformPermission.TenantsRead)]
    public async Task<ActionResult<IReadOnlyList<IntegrationClientDto>>> ListAsync(
        Guid tenantId, CancellationToken ct)
    {
        if (!await TenantExistsAsync(tenantId, ct)) return NotFound();

        var clients = await _catalog.ApiIntegrationClients.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Name)
            .Select(item => new IntegrationClientDto(
                item.Id, item.Name, item.ClientId, item.Scopes,
                item.IsActive, item.CreatedAt, item.LastUsedAt))
            .ToListAsync(ct);
        return Ok(clients);
    }

    [HttpGet("scopes")]
    [RequirePlatformPermission(PlatformPermission.TenantsRead)]
    public async Task<IActionResult> ListScopesAsync(Guid tenantId, CancellationToken ct)
    {
        if (!await TenantExistsAsync(tenantId, ct)) return NotFound();
        return Ok(IntegrationScope.All.OrderBy(value => value));
    }

    [HttpPost]
    [RequirePlatformPermission(PlatformPermission.TenantsManage)]
    public async Task<ActionResult<IntegrationClientCreatedDto>> CreateAsync(
        Guid tenantId, [FromBody] CreateIntegrationClientRequest request, CancellationToken ct)
    {
        if (!await TenantExistsAsync(tenantId, ct)) return NotFound();

        try
        {
            var (client, secret) = await _tokens.CreateAsync(tenantId, request.Name, request.Scopes, ct);
            _logger.LogWarning(
                "Credencial técnica {ClientId} criada para tenant {TenantId} pelo painel da plataforma.",
                client.ClientId, tenantId);
            return Created(string.Empty, new IntegrationClientCreatedDto(
                client.Id, client.Name, client.ClientId, secret, client.Scopes, client.CreatedAt));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/rotate")]
    [RequirePlatformPermission(PlatformPermission.TenantsManage)]
    public async Task<ActionResult<IntegrationClientCreatedDto>> RotateAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        try
        {
            var (client, secret) = await _tokens.RotateAsync(tenantId, id, ct);
            _logger.LogWarning(
                "Credencial técnica {ClientId} rotacionada para tenant {TenantId} pelo painel da plataforma.",
                client.ClientId, tenantId);
            return Ok(new IntegrationClientCreatedDto(
                client.Id, client.Name, client.ClientId, secret, client.Scopes, client.CreatedAt));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePlatformPermission(PlatformPermission.TenantsManage)]
    public async Task<IActionResult> RevokeAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        try
        {
            await _tokens.RevokeAsync(tenantId, id, ct);
            _logger.LogWarning(
                "Credencial técnica {CredentialId} revogada para tenant {TenantId} pelo painel da plataforma.",
                id, tenantId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    private Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken ct) =>
        _catalog.Tenants.AnyAsync(item => item.Id == tenantId, ct);
}
