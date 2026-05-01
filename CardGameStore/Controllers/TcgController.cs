// =============================================================================
// TcgController.cs — Busca de Cartas TCG (Cache-First via MongoDB)
// GET /api/tcg/search?name=pikachu&game=Pokemon  → pesquisa cartas
// GET /api/tcg/cards/{id}                        → busca por ID
// GET /api/tcg/sets?game=Pokemon                 → lista sets
// DELETE /api/tcg/cards/{id}/cache               → invalida cache (Admin)
// POST /api/tcg/purge-cache                      → purga itens expirados (Admin)
// =============================================================================

using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TcgController : ControllerBase
{
    private readonly ITcgService _tcgService;

    public TcgController(ITcgService tcgService)
    {
        _tcgService = tcgService;
    }

    /// <summary>
    /// Pesquisa cartas por nome com estratégia Cache-First.
    /// Primeiro busca no MongoDB; se não encontrar, consulta a API TCG externa.
    /// </summary>
    [HttpGet("search")]
    [Authorize]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Search(
        [FromQuery] string  name,
        [FromQuery] string? game     = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { Message = "Parâmetro 'name' é obrigatório." });

        var result = await _tcgService.SearchCardsByNameAsync(name, game, page, pageSize);
        return Ok(result);
    }

    /// <summary>Busca uma carta específica pelo ID da API TCG.</summary>
    [HttpGet("cards/{tcgCardId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetCard(string tcgCardId)
    {
        var card = await _tcgService.GetCardByIdAsync(tcgCardId);
        return card == null ? NotFound(new { Message = $"Carta '{tcgCardId}' não encontrada." }) : Ok(card);
    }

    /// <summary>Lista os sets disponíveis para um jogo.</summary>
    [HttpGet("sets")]
    [Authorize]
    public async Task<IActionResult> GetSets([FromQuery] string game = "Pokemon")
    {
        var sets = await _tcgService.GetAvailableSetsAsync(game);
        return Ok(sets);
    }

    /// <summary>Força atualização do cache de uma carta. Apenas Admin.</summary>
    [HttpPost("cards/{tcgCardId}/refresh")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RefreshCache(string tcgCardId)
    {
        var card = await _tcgService.RefreshCardCacheAsync(tcgCardId);
        return card == null ? NotFound() : Ok(card);
    }

    /// <summary>Remove uma carta do cache. Apenas Admin.</summary>
    [HttpDelete("cards/{tcgCardId}/cache")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> InvalidateCache(string tcgCardId)
    {
        await _tcgService.InvalidateCacheAsync(tcgCardId);
        return NoContent();
    }

    /// <summary>Remove todos os documentos expirados do cache MongoDB. Apenas Admin.</summary>
    [HttpPost("purge-cache")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> PurgeCache()
    {
        var count = await _tcgService.PurgeExpiredCacheAsync();
        return Ok(new { RemovedCount = count, Message = $"{count} cartas expiradas removidas do cache." });
    }
}
