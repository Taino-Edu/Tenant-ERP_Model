// =============================================================================
// ComandaController.cs — Endpoints REST de Comandas
// GET  /api/comanda/dashboard     → lista todas as abertas (Admin)
// GET  /api/comanda/my            → comanda ativa do cliente logado
// POST /api/comanda/{id}/items    → adiciona item
// DELETE /api/comanda/{id}/items/{itemId} → remove item
// PUT  /api/comanda/{id}/close    → fecha comanda (Admin)
// PUT  /api/comanda/{id}/cancel   → cancela comanda (Admin)
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
public class ComandaController : ControllerBase
{
    private readonly IComandaService _service;

    public ComandaController(IComandaService service)
    {
        _service = service;
    }

    /// <summary>Dashboard do Admin: lista todas as comandas abertas/em andamento.</summary>
    [HttpGet("dashboard")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<ComandaDto>), 200)]
    public async Task<IActionResult> GetDashboard()
    {
        var comandas = await _service.GetActiveCommandasForDashboardAsync();
        return Ok(comandas);
    }

    /// <summary>Retorna a comanda ativa do cliente autenticado.</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(ComandaDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMyComanda()
    {
        var userId  = GetUserId();
        var comanda = await _service.GetActiveComandaAsync(userId);
        return comanda == null ? NotFound(new { Message = "Nenhuma comanda ativa encontrada." }) : Ok(comanda);
    }

    /// <summary>Adiciona um item à comanda do cliente autenticado.</summary>
    [HttpPost("{id:guid}/items")]
    [ProducesResponseType(typeof(ComandaDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddItemToComandaRequest request)
    {
        var userId = GetUserId();
        var role   = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        ComandaDto result;

        if (role == "Admin")
            result = await _service.AdminAddItemAsync(id, userId, request);
        else
            result = await _service.AddItemAsync(userId, request);

        return Ok(result);
    }

    /// <summary>Remove um item de uma comanda (Admin ou próprio cliente).</summary>
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [ProducesResponseType(typeof(ComandaDto), 200)]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId)
    {
        var userId = GetUserId();
        var result = await _service.RemoveItemAsync(id, itemId, userId);
        return Ok(result);
    }

    /// <summary>Fecha uma comanda (pagamento recebido). Apenas Admin.</summary>
    [HttpPut("{id:guid}/close")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ComandaDto), 200)]
    public async Task<IActionResult> Close(Guid id)
    {
        var adminId = GetUserId();
        var result  = await _service.CloseComandaAsync(id, adminId);
        return Ok(result);
    }

    /// <summary>Cancela uma comanda sem cobrança. Apenas Admin.</summary>
    [HttpPut("{id:guid}/cancel")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ComandaDto), 200)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var adminId = GetUserId();
        var result  = await _service.CancelComandaAsync(id, adminId);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
