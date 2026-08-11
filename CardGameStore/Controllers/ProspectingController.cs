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
    private readonly IProspectingCampaignService _campaigns;
    private readonly ILogger<ProspectingController> _logger;

    public ProspectingController(IProspectingService prospecting,
        IProspectingCampaignService campaigns, ILogger<ProspectingController> logger)
    {
        _prospecting = prospecting;
        _campaigns = campaigns;
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

    [HttpGet("campaigns")]
    public async Task<IActionResult> ListCampaigns(CancellationToken ct) =>
        Ok(await _campaigns.ListAsync(ct));

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign(
        [FromBody] CreateProspectingCampaignRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var campaign = await _campaigns.CreateAsync(request, ct);
        return CreatedAtAction(nameof(ListCampaigns), new { id = campaign.Id }, campaign);
    }

    [HttpPost("campaigns/{id:guid}/run")]
    public async Task<IActionResult> RunCampaign(Guid id, CancellationToken ct)
    {
        try
        {
            var run = await _campaigns.EnqueueAsync(id, ct);
            return run is null ? NotFound() : Accepted(run);
        }
        catch (ProspectingBudgetExceededException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { Message = ex.Message });
        }
    }

    [HttpPatch("campaigns/{id:guid}/status")]
    public async Task<IActionResult> SetCampaignStatus(Guid id,
        [FromBody] SetProspectingCampaignStatusRequest request, CancellationToken ct) =>
        await _campaigns.SetActiveAsync(id, request.Active, ct) ? NoContent() : NotFound();

    [HttpGet("campaigns/review-queue")]
    public async Task<IActionResult> ReviewQueue([FromQuery] int limit = 100, CancellationToken ct = default) =>
        Ok(await _campaigns.ListReviewQueueAsync(Math.Clamp(limit, 1, 500), ct));

    [HttpPost("candidates/{id:guid}/suppress")]
    public async Task<IActionResult> SuppressCandidate(Guid id,
        [FromBody] SuppressProspectRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return await _campaigns.SuppressCandidateAsync(id, request.Reason, ct)
            ? NoContent()
            : NotFound();
    }

    /// <summary>Gera sob demanda uma sugestão de abordagem via Gemini e a
    /// persiste no candidato. A IA não cria nem altera dados empresariais ou
    /// financeiros observados.</summary>
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

    /// <summary>Gera e salva uma abordagem para um lead que já está no CRM.</summary>
    [HttpPost("leads/{id:guid}/enrich")]
    public async Task<IActionResult> EnrichLead(Guid id)
    {
        try
        {
            var result = await _prospecting.EnrichLeadWithAiAsync(id);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Falha ao enriquecer lead {LeadId}: {Msg}", id, ex.Message);
            return StatusCode(503, new { Message = ex.Message });
        }
    }
}
