using CardGameStore.DTOs;
using CardGameStore.Middleware;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Security;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/integrations")]
[Authorize(Policy = "AdminOnly")]
[OperatorForbidden]
[Produces("application/json")]
public sealed class IntegrationClientsController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    private readonly ITenantContext _tenant;
    private readonly IntegrationTokenService _tokens;
    private readonly IAuditService _audit;

    public IntegrationClientsController(
        CatalogDbContext catalog,
        ITenantContext tenant,
        IntegrationTokenService tokens,
        IAuditService audit)
    {
        _catalog = catalog;
        _tenant = tenant;
        _tokens = tokens;
        _audit = audit;
    }

    [HttpGet("clients")]
    public async Task<ActionResult<IReadOnlyList<IntegrationClientDto>>> ListAsync(CancellationToken ct)
    {
        var clients = await _catalog.ApiIntegrationClients.AsNoTracking()
            .Where(item => item.TenantId == _tenant.TenantId)
            .OrderBy(item => item.Name)
            .Select(item => new IntegrationClientDto(
                item.Id, item.Name, item.ClientId, item.Scopes,
                item.IsActive, item.CreatedAt, item.LastUsedAt))
            .ToListAsync(ct);
        return Ok(clients);
    }

    [HttpGet("scopes")]
    public IActionResult ListScopes() => Ok(IntegrationScope.All.OrderBy(value => value));

    [HttpPost("clients")]
    public async Task<ActionResult<IntegrationClientCreatedDto>> CreateAsync(
        [FromBody] CreateIntegrationClientRequest request, CancellationToken ct)
    {
        if (_tenant.TenantId == TenantConstants.TenantZeroId)
            return BadRequest(new { Message = "Crie a integracao no dominio da loja." });

        try
        {
            var (client, secret) = await _tokens.CreateAsync(_tenant.TenantId, request.Name, request.Scopes, ct);
            await AuditAsync("IntegrationClient.Created", client);
            return CreatedAtAction(nameof(ListAsync), new IntegrationClientCreatedDto(
                client.Id, client.Name, client.ClientId, secret, client.Scopes, client.CreatedAt));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("clients/{id:guid}/rotate")]
    public async Task<ActionResult<IntegrationClientCreatedDto>> RotateAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var (client, secret) = await _tokens.RotateAsync(_tenant.TenantId, id, ct);
            await AuditAsync("IntegrationClient.Rotated", client);
            return Ok(new IntegrationClientCreatedDto(
                client.Id, client.Name, client.ClientId, secret, client.Scopes, client.CreatedAt));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    [HttpDelete("clients/{id:guid}")]
    public async Task<IActionResult> RevokeAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await _tokens.RevokeAsync(_tenant.TenantId, id, ct);
            await _audit.LogAsync("IntegrationClient.Revoked", nameof(ApiIntegrationClient), id.ToString(),
                channel: "API", httpContext: HttpContext);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    private Task AuditAsync(string action, ApiIntegrationClient client) =>
        _audit.LogAsync(action, nameof(ApiIntegrationClient), client.Id.ToString(),
            details: System.Text.Json.JsonSerializer.Serialize(new
            {
                client.ClientId,
                client.Name,
                client.Scopes,
            }),
            httpContext: HttpContext,
            channel: "API");
}
