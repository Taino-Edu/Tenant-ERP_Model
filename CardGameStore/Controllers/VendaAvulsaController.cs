// =============================================================================
// VendaAvulsaController.cs — Endpoints de Venda Avulsa (caixa do balcão)
//
// POST /api/venda-avulsa          → Registra venda no balcão (Admin)
//                                    Valida estoque, decrementa PostgreSQL,
//                                    persiste evento imutável no MongoDB.
// GET  /api/venda-avulsa/recent   → Últimas N vendas (dashboard/histórico)
//
// Separado do ComandaController intencionalmente:
//   VendaAvulsa = evento de caixa, sem usuário cadastrado, sem comanda.
//   Comanda     = pedido de mesa via QR Code, com ciclo de vida.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Models.MongoDB;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/venda-avulsa")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public class VendaAvulsaController : ControllerBase
{
    private readonly IVendaAvulsaService _service;

    public VendaAvulsaController(IVendaAvulsaService service) => _service = service;

    /// <summary>
    /// Registra uma venda avulsa no balcão.
    /// Decrementa estoque (PostgreSQL) e persiste o evento no MongoDB.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(VendaAvulsaDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] VendaAvulsaRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!request.IsPaymentMethodValid())
            return BadRequest(new { Message = $"Forma de pagamento inválida. Use: {string.Join(", ", PaymentMethod.All)}" });

        try
        {
            var adminId   = GetUserId();
            var adminName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                         ?? User.FindFirst("name")?.Value
                         ?? "Admin";

            var result = await _service.RegisterAsync(request, adminId, adminName);
            return StatusCode(201, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Retorna as vendas avulsas mais recentes para exibição no dashboard.</summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IEnumerable<VendaAvulsaDto>), 200)]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 50)
    {
        if (limit is < 1 or > 200)
            limit = 50;

        var result = await _service.GetRecentAsync(limit);
        return Ok(result);
    }

    /// <summary>Retorna todas as vendas avulsas de uma data específica (YYYY-MM-DD, fuso de Brasília). Padrão: hoje.</summary>
    [HttpGet("by-date")]
    [ProducesResponseType(typeof(IEnumerable<VendaAvulsaDto>), 200)]
    public async Task<IActionResult> GetByDate([FromQuery] string? date = null)
    {
        // Quando não há ?date=, passa null → serviço calcula "hoje" no fuso BR.
        // Quando há data explícita, repassa como DateTime para o serviço converter corretamente.
        DateTime? day = null;
        if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsed))
            day = parsed.Date;

        var result = await _service.GetByDateAsync(day);
        return Ok(result);
    }

    /// <summary>
    /// Preenche o custo (UnitCostInCents) em itens de vendas avulsas antigas que ficaram com custo = 0.
    /// Usa o custo atual de cada produto no PostgreSQL como referência.
    /// </summary>
    [HttpPost("backfill-costs")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> BackfillCosts()
    {
        var total = await _service.BackfillCostsAsync();
        return Ok(new { itensAtualizados = total, mensagem = $"{total} item(s) de venda avulsa atualizados com custo." });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token inválido: identificador de usuário ausente.");
        return id;
    }
}
