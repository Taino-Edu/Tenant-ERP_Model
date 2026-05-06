// =============================================================================
// CrediariosController.cs — Gestão de crediários
//
// GET  /api/crediarios                     → Admin: lista todos (filtro por status)
// GET  /api/crediarios/usuario/{userId}    → Admin: crediários de um cliente
// GET  /api/crediarios/meu                 → Cliente: seu crediário ativo
// PUT  /api/crediarios/{id}/pagar          → Admin: marca como pago + envia email
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/crediarios")]
[Authorize]
public class CrediariosController : ControllerBase
{
    private readonly AppDbContext    _db;
    private readonly IEmailService   _email;
    private readonly ILogger<CrediariosController> _logger;

    public CrediariosController(AppDbContext db, IEmailService email, ILogger<CrediariosController> logger)
    {
        _db     = db;
        _email  = email;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /api/crediarios?status=Aberto
    // -------------------------------------------------------------------------
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<CrediariosDto>>> GetAll([FromQuery] string? status)
    {
        var query = _db.Crediarios
            .Include(c => c.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<CrediariosStatus>(status, ignoreCase: true, out var s))
            query = query.Where(c => c.Status == s);

        var crediarios = await query
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        return Ok(crediarios.Select(MapToDto).ToList());
    }

    // -------------------------------------------------------------------------
    // GET /api/crediarios/usuario/{userId}
    // -------------------------------------------------------------------------
    [HttpGet("usuario/{userId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<CrediariosDto>>> GetByUser(Guid userId)
    {
        var crediarios = await _db.Crediarios
            .Include(c => c.User)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        return Ok(crediarios.Select(MapToDto).ToList());
    }

    // -------------------------------------------------------------------------
    // GET /api/crediarios/meu
    // -------------------------------------------------------------------------
    [HttpGet("meu")]
    public async Task<ActionResult<CrediariosDto>> GetMeu()
    {
        var userId    = GetUserId();
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .Where(c => c.UserId == userId && c.Status == CrediariosStatus.Aberto)
            .FirstOrDefaultAsync();

        if (crediario == null)
            return NotFound(new { Message = "Nenhum crediário em aberto." });

        return Ok(MapToDto(crediario));
    }

    // -------------------------------------------------------------------------
    // PUT /api/crediarios/{id}/pagar
    // -------------------------------------------------------------------------
    [HttpPut("{id:guid}/pagar")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<CrediariosDto>> MarcarPago(Guid id, [FromBody] MarcarPagoRequest? request)
    {
        var adminId   = GetUserId();
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (crediario == null)
            return NotFound(new { Message = "Crediário não encontrado." });

        if (crediario.Status == CrediariosStatus.Pago)
            return BadRequest(new { Message = "Crediário já está quitado." });

        crediario.Status        = CrediariosStatus.Pago;
        crediario.DataPagamento = DateTime.UtcNow;
        crediario.PagoPorAdminId = adminId;

        if (!string.IsNullOrWhiteSpace(request?.Observacao))
            crediario.Observacao = (crediario.Observacao != null
                ? crediario.Observacao + " | " : "") + request.Observacao;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Crediário {Id} quitado pelo admin {AdminId} — R$ {Valor:N2}",
            id, adminId, crediario.ValorEmReais);

        // Envia email de confirmação (não bloqueia)
        if (!string.IsNullOrWhiteSpace(crediario.User?.Email))
            _ = _email.SendCrediarioPagoAsync(
                crediario.User.Email, crediario.User.Name, crediario.ValorEmReais);

        return Ok(MapToDto(crediario));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CrediariosDto MapToDto(Crediario c)
    {
        var agora = DateTime.UtcNow;
        var vencido = c.Status == CrediariosStatus.Aberto && c.DataVencimento < agora;
        var dias    = (int)Math.Round((c.DataVencimento - agora).TotalDays);

        return new CrediariosDto
        {
            Id             = c.Id,
            UserId         = c.UserId,
            UserName       = c.User?.Name ?? string.Empty,
            UserEmail      = c.User?.Email,
            ComandaId      = c.ComandaId,
            ValorEmReais   = c.ValorEmReais,
            DataAbertura   = c.DataAbertura,
            DataVencimento = c.DataVencimento,
            DataPagamento  = c.DataPagamento,
            Status         = vencido ? "Vencido" : c.Status.ToString(),
            Observacao     = c.Observacao,
            Vencido        = vencido,
            DiasRestantes  = dias,
        };
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
