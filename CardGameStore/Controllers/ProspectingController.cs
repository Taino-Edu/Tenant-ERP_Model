// =============================================================================
// ProspectingController.cs — Busca de possíveis clientes (prospecção) pelo
// dono da plataforma. Ver CardGameStore/Services/Implementations/ProspectingService.cs
// pro racional de duas chaves de API separadas (Places + Gemini dedicado).
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/platform/prospecting")]
[Authorize(Policy = "PlatformOwnerOnly")]
public class ProspectingController : ControllerBase
{
    private readonly IProspectingService _prospecting;
    private readonly ILogger<ProspectingController> _logger;

    public ProspectingController(IProspectingService prospecting, ILogger<ProspectingController> logger)
    {
        _prospecting = prospecting;
        _logger      = logger;
    }

    /// <summary>Busca negócios por categoria+cidade via Google Places API, já
    /// classificados (presença digital, score, faixa de faturamento) sem
    /// gastar IA. Resultados são efêmeros — só viram Lead de verdade em
    /// POST /api/platform/leads/prospeccao.</summary>
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] ProspectingSearchRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var candidates = await _prospecting.SearchAsync(request.Categoria, request.Cidade);
            return Ok(candidates);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { Message = ex.Message });
        }
    }

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
