// =============================================================================
// CreditarioController.cs — Endpoints REST de Crediários
// GET    /api/crediarios                  → lista todos os crediários (Admin)
// GET    /api/crediarios/abertos          → crediários em aberto (Admin)
// GET    /api/crediarios/vencidos         → crediários vencidos (Admin)
// GET    /api/crediarios/user/{userId}    → crediários de um usuário (Admin ou próprio)
// GET    /api/crediarios/user/{userId}/total → total devido (Admin ou próprio)
// GET    /api/crediarios/{id}             → detalhe de um crediário (Admin ou proprietário)
// PUT    /api/crediarios/{id}/pagar       → marca como pago (Admin)
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class CreditarioController : ControllerBase
{
    private readonly ICreditarioService _service;

    public CreditarioController(ICreditarioService service)
    {
        _service = service;
    }

    /// <summary>Lista TODOS os crediários (abertos e pagos). Apenas Admin.</summary>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(List<CrediariosDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Lista crediários abertos (não pagos). Útil para dashboard do Admin.</summary>
    [HttpGet("abertos")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(List<CrediariosDto>), 200)]
    public async Task<IActionResult> GetAbertos()
    {
        var result = await _service.GetAbertoAsync();
        return Ok(result);
    }

    /// <summary>Lista crediários vencidos (abertos e além da data de vencimento).</summary>
    [HttpGet("vencidos")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(List<CrediariosDto>), 200)]
    public async Task<IActionResult> GetVencidos()
    {
        var result = await _service.GetVencidosAsync();
        return Ok(result);
    }

    /// <summary>Retorna todos os crediários de um usuário. Admin vê qualquer um, cliente vê o seu.</summary>
    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(List<CrediariosDto>), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetByUser(Guid userId)
    {
        var requestingUserId = GetUserId();
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        // Cliente pode ver apenas seus próprios crediários
        if (role != "Admin" && requestingUserId != userId)
            return Forbid("Você só pode ver seus próprios crediários.");

        var result = await _service.GetByUserAsync(userId);
        return Ok(result);
    }

    /// <summary>Retorna o total devido por um usuário (soma de crediários abertos).</summary>
    [HttpGet("user/{userId:guid}/total")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetTotalDevido(Guid userId)
    {
        var requestingUserId = GetUserId();
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        // Cliente pode ver apenas seu próprio total
        if (role != "Admin" && requestingUserId != userId)
            return Forbid("Você só pode ver seu próprio total.");

        var total = await _service.GetTotalDevidoAsync(userId);
        return Ok(new { TotalDevidoEmReais = total });
    }

    /// <summary>Retorna um crediário específico pelo ID. Admin ou proprietário.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CrediariosDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var crediario = await _service.GetByIdAsync(id);
        if (crediario == null)
            return NotFound(new { Message = "Crediário não encontrado." });

        var requestingUserId = GetUserId();
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        // Cliente pode ver apenas seus próprios crediários
        if (role != "Admin" && requestingUserId != crediario.UserId)
            return Forbid("Você só pode ver seus próprios crediários.");

        return Ok(crediario);
    }

    /// <summary>Marca um crediário como pago. Apenas Admin.</summary>
    [HttpPut("{id:guid}/pagar")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(CrediariosDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkAsPaid(Guid id, [FromBody] MarcarPagoRequest? request)
    {
        try
        {
            var adminId = GetUserId();
            var observacao = request?.Observacao;
            var result = await _service.MarkAsPaidAsync(id, adminId, observacao);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
