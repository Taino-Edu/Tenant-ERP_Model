using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CardGameStore.Controllers;

/// <summary>
/// Superficie publica minima para client_credentials. Fica separada da gestao
/// administrativa para nao ativar AppDbContext ou servicos de auditoria antes
/// de validar o tenant e o payload tecnico.
/// </summary>
[ApiController]
[Route("api/integrations")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class IntegrationTokenController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly IntegrationTokenService _tokens;

    public IntegrationTokenController(ITenantContext tenant, IntegrationTokenService tokens)
    {
        _tenant = tenant;
        _tokens = tokens;
    }

    [HttpPost("token")]
    [EnableRateLimiting("integration-token")]
    public async Task<ActionResult<IntegrationTokenResponse>> TokenAsync(
        [FromBody] IntegrationTokenRequest request, CancellationToken ct)
    {
        if (_tenant.TenantId == TenantConstants.TenantZeroId)
            return Unauthorized(new { Message = "Credenciais invalidas para este tenant." });

        var token = await _tokens.IssueAsync(_tenant.TenantId, request, ct);
        return token is null
            ? Unauthorized(new { Message = "Credenciais invalidas para este tenant." })
            : Ok(token);
    }
}
