// =============================================================================
// AnnouncementController.cs — Anúncios e banners da loja
//
// GET  /api/announcements         → Anúncios visíveis (público, sem auth)
// GET  /api/announcements/all     → Todos os anúncios, ativos e inativos (Admin)
// POST /api/announcements         → Criar anúncio (Admin)
// PUT  /api/announcements/{id}    → Atualizar (Admin)
// DELETE /api/announcements/{id}  → Remover (Admin)
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/announcements")]
[Produces("application/json")]
public class AnnouncementController : ControllerBase
{
    private readonly IAnnouncementService _service;

    public AnnouncementController(IAnnouncementService service) => _service = service;

    /// <summary>Retorna os anúncios visíveis (ativos e dentro do prazo). Público.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<AnnouncementDto>), 200)]
    public async Task<IActionResult> GetVisible()
        => Ok(await _service.GetVisibleAsync());

    /// <summary>Retorna todos os anúncios (ativos e inativos). Admin only.</summary>
    [HttpGet("all")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<AnnouncementDto>), 200)]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetAllAsync());

    /// <summary>Cria um novo anúncio.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(AnnouncementDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateAnnouncementRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var adminId = GetUserId();
        var result  = await _service.CreateAsync(request, adminId);
        return StatusCode(201, result);
    }

    /// <summary>Atualiza título, corpo, imagem, expiração ou status ativo/inativo.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(AnnouncementDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAnnouncementRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>Remove permanentemente um anúncio.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
