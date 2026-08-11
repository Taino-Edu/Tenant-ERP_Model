// =============================================================================
// ProspectingController.cs — Busca de possíveis clientes (prospecção) pelo
// dono da plataforma. Ver CardGameStore/Services/Implementations/ProspectingService.cs
// pro racional da busca via OpenStreetMap (gratuita) + enriquecimento via
// Gemini com chave dedicada.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Security;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/platform/prospecting")]
[Authorize(Policy = "PlatformOwnerOnly")]
[RequirePlatformPermission(PlatformPermission.Leads)]
public class ProspectingController : ControllerBase
{
    private readonly IProspectingService _prospecting;
    private readonly ILogger<ProspectingController> _logger;

    public ProspectingController(IProspectingService prospecting, ILogger<ProspectingController> logger)
    {
        _prospecting = prospecting;
        _logger      = logger;
    }

    /// <summary>Busca negócios por categoria+cidade via OpenStreetMap, já
    /// classificados e persistidos. A mesma consulta dentro do prazo retorna
    /// o snapshot salvo, salvo quando ForceRefresh=true.</summary>
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] ProspectingSearchRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            return Ok(await _prospecting.SearchAsync(request.Categoria, request.Cidade, request.ForceRefresh));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { Message = ex.Message });
        }
    }

    [HttpGet("searches")]
    public async Task<IActionResult> ListSearches([FromQuery] int limit = 20) =>
        Ok(await _prospecting.ListSearchesAsync(Math.Clamp(limit, 1, 100)));

    [HttpGet("searches/{id:guid}")]
    public async Task<IActionResult> GetSearch(Guid id)
    {
        var result = await _prospecting.GetSearchAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("categories")]
    public IActionResult ListCategories() => Ok(_prospecting.ListSupportedCategories());

    /// <summary>Enriquece um candidato específico via Gemini (chave dedicada de
    /// prospecção) — gera faixa de faturamento mais fina e sugestão de
    /// abordagem. Só roda quando pedido explicitamente, nunca em massa.</summary>
    [HttpPost("enrich")]
    public async Task<IActionResult> Enrich([FromBody] ProspectingEnrichRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var result = await _prospecting.EnrichWithAiAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Falha ao enriquecer candidato {Nome}: {Msg}", request.Nome, ex.Message);
            return StatusCode(503, new { Message = ex.Message });
        }
    }
}
