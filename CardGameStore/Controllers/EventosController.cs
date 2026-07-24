// =============================================================================
// EventosController.cs — Gestão de eventos da loja (dia de torneio, festa) e
// venda/check-in de entradas cobradas. MVP do módulo "eventos" — sem
// integração fiscal (ver comentário em Evento.cs).
//
// GET    /api/eventos                              → lista eventos
// POST   /api/eventos                               → cria evento
// PUT    /api/eventos/{id}                          → edita evento
// DELETE /api/eventos/{id}                          → cancela evento (soft)
// GET    /api/eventos/{id}/entradas                 → lista entradas do evento
// POST   /api/eventos/{id}/entradas                 → vende uma entrada
// POST   /api/eventos/{id}/entradas/{entradaId}/checkin → confirma na portaria
// DELETE /api/eventos/{id}/entradas/{entradaId}     → cancela uma entrada
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/eventos")]
[Authorize(Policy = "AdminOnly")]
[RequireModule("eventos")]
[Produces("application/json")]
public class EventosController : ControllerBase
{
    private readonly AppDbContext  _db;
    private readonly IAuditService _audit;

    public EventosController(AppDbContext db, IAuditService audit)
    {
        _db    = db;
        _audit = audit;
    }

    /// <summary>Lista eventos, mais recente primeiro, com resumo de entradas/faturamento.</summary>
    /// <param name="status">Filtro opcional por status ("Planejado", "EmAndamento", "Concluido", "Cancelado").</param>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status = null)
    {
        var query = _db.Eventos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<EventoStatus>(status, out var parsed) || !Enum.IsDefined(parsed))
                return BadRequest(new { Message = $"Status inválido: '{status}'." });
            query = query.Where(e => e.Status == parsed);
        }

        var eventos = await query.OrderByDescending(e => e.DataEvento).ToListAsync();
        var ids = eventos.Select(e => e.Id).ToList();

        var resumosBrutos = await _db.EventoEntradas.AsNoTracking()
            .Where(en => ids.Contains(en.EventoId) && en.CanceladaEm == null)
            .GroupBy(en => en.EventoId)
            .Select(g => new
            {
                EventoId = g.Key,
                Vendidas = g.Count(),
                CheckIn  = g.Count(en => en.CheckInEm != null),
                Faturamento = g.Sum(en => (long)en.ValorPagoInCents),
            })
            .ToListAsync();
        var resumos = resumosBrutos.ToDictionary(
            x => x.EventoId,
            x => ((int Vendidas, int CheckIn, long Faturamento)?)(x.Vendidas, x.CheckIn, x.Faturamento));

        return Ok(eventos.Select(e => ToDto(e, resumos.GetValueOrDefault(e.Id))));
    }

    /// <summary>Cadastra um evento novo.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var evento = new Evento
        {
            Nome                = request.Nome.Trim(),
            Descricao           = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim(),
            DataEvento          = request.DataEvento,
            PrecoEntradaInCents = request.PrecoEntradaInCents,
            CapacidadeMaxima    = request.CapacidadeMaxima,
        };

        _db.Eventos.Add(evento);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CriouEvento", "Evento", evento.Id.ToString(), details: evento.Nome, httpContext: HttpContext);

        return CreatedAtAction(nameof(List), ToDto(evento, null));
    }

    /// <summary>Edita um evento existente (dados, preço padrão, capacidade e status).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEventoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Enum.TryParse<EventoStatus>(request.Status, out var status) || !Enum.IsDefined(status))
            return BadRequest(new { Message = $"Status inválido: '{request.Status}'." });

        var evento = await _db.Eventos.FindAsync(id);
        if (evento is null) return NotFound();

        if (request.CapacidadeMaxima.HasValue)
        {
            var vendidasAtivas = await _db.EventoEntradas.CountAsync(en => en.EventoId == id && en.CanceladaEm == null);
            if (request.CapacidadeMaxima.Value < vendidasAtivas)
                return BadRequest(new { Message = $"Já há {vendidasAtivas} entrada(s) ativa(s) vendida(s) — a capacidade não pode ser menor que isso." });
        }

        evento.Nome                = request.Nome.Trim();
        evento.Descricao           = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
        evento.DataEvento          = request.DataEvento;
        evento.PrecoEntradaInCents = request.PrecoEntradaInCents;
        evento.CapacidadeMaxima    = request.CapacidadeMaxima;
        evento.Status              = status;
        evento.UpdatedAt           = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var resumo = await ResumoAsync(id);
        return Ok(ToDto(evento, resumo));
    }

    /// <summary>Cancela um evento — soft (Status=Cancelado), preserva as entradas já
    /// vendidas pro histórico/estorno manual em vez de apagar tudo.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var evento = await _db.Eventos.FindAsync(id);
        if (evento is null) return NotFound();

        evento.Status    = EventoStatus.Cancelado;
        evento.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CancelouEvento", "Evento", id.ToString(), details: evento.Nome, httpContext: HttpContext);

        return NoContent();
    }

    /// <summary>Lista as entradas vendidas pro evento, mais recentes primeiro.</summary>
    [HttpGet("{id:guid}/entradas")]
    public async Task<IActionResult> ListEntradas(Guid id)
    {
        var existe = await _db.Eventos.AnyAsync(e => e.Id == id);
        if (!existe) return NotFound();

        var entradas = await _db.EventoEntradas.AsNoTracking()
            .Where(en => en.EventoId == id)
            .OrderByDescending(en => en.CreatedAt)
            .ToListAsync();

        return Ok(entradas.Select(ToDto));
    }

    /// <summary>Registra a venda de uma entrada. Valor pago default é o preço padrão do
    /// evento — informe ValorPagoInCents pra meia-entrada/cortesia/preço promocional.
    /// Bloqueia se a capacidade máxima do evento já foi atingida.</summary>
    [HttpPost("{id:guid}/entradas")]
    public async Task<IActionResult> VenderEntrada(Guid id, [FromBody] CreateEntradaRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!PaymentMethod.IsValid(request.FormaPagamento))
            return BadRequest(new { Message = $"Forma de pagamento inválida: '{request.FormaPagamento}'." });

        var evento = await _db.Eventos.FindAsync(id);
        if (evento is null) return NotFound();

        if (evento.Status is EventoStatus.Cancelado or EventoStatus.Concluido)
            return BadRequest(new { Message = $"Evento está {evento.Status} — não é possível vender mais entradas." });

        var entrada = new EventoEntrada
        {
            EventoId            = id,
            NomeCliente         = request.NomeCliente.Trim(),
            UserId              = request.UserId,
            FormaPagamento      = request.FormaPagamento,
            ValorPagoInCents    = request.ValorPagoInCents ?? evento.PrecoEntradaInCents,
            VendidaPorAdminId   = GetUserId(),
            VendidaPorAdminNome = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Admin",
        };

        // Trava a linha do evento (SELECT ... FOR UPDATE) antes de contar as entradas
        // ativas — duas vendas concorrentes pro último lugar não podem mais as duas
        // passar pela checagem de capacidade e inserir (achado de review: sem a trava,
        // a checagem e o INSERT são operações separadas e dá pra vender acima do limite).
        var capacidadeExcedida = false;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM eventos WHERE id = {id} FOR UPDATE");

            if (evento.CapacidadeMaxima.HasValue)
            {
                var vendidas = await _db.EventoEntradas.CountAsync(en => en.EventoId == id && en.CanceladaEm == null);
                if (vendidas >= evento.CapacidadeMaxima.Value)
                {
                    capacidadeExcedida = true;
                    await tx.RollbackAsync();
                    return;
                }
            }

            _db.EventoEntradas.Add(entrada);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        if (capacidadeExcedida)
            return BadRequest(new { Message = $"Capacidade máxima ({evento.CapacidadeMaxima}) já atingida pro evento." });

        await _audit.LogAsync("VendeuEntradaEvento", "EventoEntrada", entrada.Id.ToString(),
            details: $"{entrada.NomeCliente} — {entrada.ValorPagoInCents / 100m:F2}", httpContext: HttpContext);

        return CreatedAtAction(nameof(ListEntradas), new { id }, ToDto(entrada));
    }

    /// <summary>Confirma a entrada na portaria do evento.</summary>
    [HttpPost("{id:guid}/entradas/{entradaId:guid}/checkin")]
    public async Task<IActionResult> CheckIn(Guid id, Guid entradaId)
    {
        var entrada = await _db.EventoEntradas.FirstOrDefaultAsync(en => en.Id == entradaId && en.EventoId == id);
        if (entrada is null) return NotFound();

        if (entrada.CanceladaEm is not null)
            return BadRequest(new { Message = "Esta entrada foi cancelada." });
        if (entrada.CheckInEm is not null)
            return BadRequest(new { Message = "Check-in já confirmado." });

        entrada.CheckInEm = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ToDto(entrada));
    }

    /// <summary>Cancela uma entrada vendida (estorno manual, fora do sistema —
    /// nenhum reembolso automático é disparado, só libera a vaga na capacidade).</summary>
    [HttpDelete("{id:guid}/entradas/{entradaId:guid}")]
    public async Task<IActionResult> CancelarEntrada(Guid id, Guid entradaId)
    {
        var entrada = await _db.EventoEntradas.FirstOrDefaultAsync(en => en.Id == entradaId && en.EventoId == id);
        if (entrada is null) return NotFound();

        if (entrada.CanceladaEm is not null)
            return BadRequest(new { Message = "Esta entrada já estava cancelada." });

        entrada.CanceladaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CancelouEntradaEvento", "EventoEntrada", entrada.Id.ToString(), httpContext: HttpContext);

        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(int Vendidas, int CheckIn, long Faturamento)?> ResumoAsync(Guid eventoId)
    {
        var ativas = await _db.EventoEntradas.AsNoTracking()
            .Where(en => en.EventoId == eventoId && en.CanceladaEm == null)
            .ToListAsync();
        if (ativas.Count == 0) return (0, 0, 0);
        return (ativas.Count, ativas.Count(en => en.CheckInEm != null), ativas.Sum(en => (long)en.ValorPagoInCents));
    }

    private static EventoDto ToDto(Evento e, (int Vendidas, int CheckIn, long Faturamento)? resumo) => new()
    {
        Id                  = e.Id,
        Nome                = e.Nome,
        Descricao           = e.Descricao,
        DataEvento          = e.DataEvento,
        PrecoEntradaInCents = e.PrecoEntradaInCents,
        CapacidadeMaxima    = e.CapacidadeMaxima,
        Status              = e.Status.ToString(),
        EntradasVendidas    = resumo?.Vendidas ?? 0,
        EntradasCheckIn     = resumo?.CheckIn ?? 0,
        FaturamentoInCents  = resumo?.Faturamento ?? 0,
        CreatedAt           = e.CreatedAt,
    };

    private static EventoEntradaDto ToDto(EventoEntrada en) => new()
    {
        Id                  = en.Id,
        NomeCliente         = en.NomeCliente,
        UserId              = en.UserId,
        FormaPagamento      = en.FormaPagamento,
        ValorPagoInCents    = en.ValorPagoInCents,
        CheckInEm           = en.CheckInEm,
        CanceladaEm         = en.CanceladaEm,
        VendidaPorAdminNome = en.VendidaPorAdminNome,
        CreatedAt           = en.CreatedAt,
    };

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token inválido: identificador de usuário ausente.");
        return id;
    }
}
