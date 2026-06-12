// =============================================================================
// CrediariosController.cs — Gestão de crediários
//
// POST /api/crediarios                     → Admin: cria crediário manual (dívida antiga)
// GET  /api/crediarios                     → Admin: lista todos (filtro por status)
// GET  /api/crediarios/usuario/{userId}    → Admin: crediários de um cliente
// GET  /api/crediarios/meu                 → Cliente: seu crediário ativo
// PUT  /api/crediarios/{id}/pagar          → Admin: quita 100% (legado)
// POST /api/crediarios/{id}/pagamento      → Admin: registra pagamento parcial ou total
// =============================================================================

using System.Text.Json;
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
    // POST /api/crediarios — criação manual (dívidas anteriores ao sistema)
    // -------------------------------------------------------------------------
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(CrediariosDto), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<CrediariosDto>> CriarManual([FromBody] CriarCrediarioManualRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Verifica se o cliente existe
        var usuario = await _db.Users.FindAsync(request.UserId);
        if (usuario == null)
            return BadRequest(new { Message = "Cliente não encontrado." });

        // Bloqueia se já tem crediário aberto
        var jaTemAberto = await _db.Crediarios
            .AnyAsync(c => c.UserId == request.UserId && c.Status == CrediariosStatus.Aberto);
        if (jaTemAberto)
            return BadRequest(new { Message = "Este cliente já tem um crediário em aberto. Registre um pagamento antes de criar outro." });

        var adminId = GetUserId();
        var agora   = DateTime.UtcNow;

        // Serializa lista de itens se informada
        string? itensJson = null;
        if (request.Itens != null && request.Itens.Count > 0)
            itensJson = JsonSerializer.Serialize(request.Itens);

        var crediario = new Crediario
        {
            UserId           = request.UserId,
            ComandaId        = null, // dívida manual — sem comanda de origem
            ValorEmCentavos  = request.ValorEmCentavos,
            DataAbertura     = agora,
            DataVencimento   = request.DataVencimento.HasValue
                                   ? request.DataVencimento.Value.ToUniversalTime()
                                   : agora.AddDays(30),
            Status           = CrediariosStatus.Aberto,
            Observacao       = string.IsNullOrWhiteSpace(request.Observacao)
                                   ? "Dívida anterior ao sistema"
                                   : request.Observacao,
            AbertoPorAdminId = adminId,
            ItensJson        = itensJson,
        };

        _db.Crediarios.Add(crediario);
        await _db.SaveChangesAsync();

        // Recarrega com includes para montar o DTO
        var saved = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .FirstAsync(c => c.Id == crediario.Id);

        _logger.LogInformation(
            "Crediário manual {Id} criado pelo admin {AdminId} para usuário {UserId} — R$ {Valor:N2}",
            crediario.Id, adminId, request.UserId, request.ValorEmCentavos / 100m);

        return Ok(MapToDto(saved));
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
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
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
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        return Ok(crediarios.Select(MapToDto).ToList());
    }

    // -------------------------------------------------------------------------
    // GET /api/crediarios/meu — crediário aberto do cliente
    // -------------------------------------------------------------------------
    [HttpGet("meu")]
    public async Task<ActionResult<CrediariosDto>> GetMeu()
    {
        var userId    = GetUserId();
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .Where(c => c.UserId == userId && c.Status == CrediariosStatus.Aberto)
            .FirstOrDefaultAsync();

        if (crediario == null)
            return NotFound(new { Message = "Nenhum crediário em aberto." });

        return Ok(MapToDto(crediario));
    }

    // -------------------------------------------------------------------------
    // GET /api/crediarios/historico — todo o histórico de crediários do cliente
    // -------------------------------------------------------------------------
    [HttpGet("historico")]
    public async Task<ActionResult<List<CrediariosDto>>> GetMeuHistorico()
    {
        var userId = GetUserId();
        var lista  = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        return Ok(lista.Select(MapToDto).ToList());
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
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (crediario == null)
            return NotFound(new { Message = "Crediário não encontrado." });

        if (crediario.Status == CrediariosStatus.Pago)
            return BadRequest(new { Message = "Crediário já está quitado." });

        // Garante que ValorPago reflita a quitação total
        crediario.ValorPagoEmCentavos = crediario.ValorEmCentavos;
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

    // -------------------------------------------------------------------------
    // PATCH /api/crediarios/{id} — editar valor, observação ou vencimento
    // -------------------------------------------------------------------------
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(CrediariosDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CrediariosDto>> Editar(Guid id, [FromBody] EditarCrediarioRequest request)
    {
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (crediario == null)
            return NotFound(new { Message = "Crediário não encontrado." });

        if (crediario.Status == CrediariosStatus.Pago)
            return BadRequest(new { Message = "Não é possível editar um crediário já quitado." });

        if (request.ValorEmCentavos.HasValue)
        {
            if (request.ValorEmCentavos.Value < crediario.ValorPagoEmCentavos)
                return BadRequest(new
                {
                    Message = $"O novo valor (R$ {request.ValorEmCentavos.Value / 100m:N2}) não pode ser menor do que o valor já pago (R$ {crediario.ValorPagoEmCentavos / 100m:N2})."
                });
            crediario.ValorEmCentavos = request.ValorEmCentavos.Value;
        }

        if (request.Observacao != null)
            crediario.Observacao = request.Observacao;

        if (request.DataVencimento.HasValue)
        {
            if (request.DataVencimento.Value.ToUniversalTime().Date < DateTime.UtcNow.Date)
                return BadRequest(new { Message = "A data de vencimento não pode ser no passado." });
            crediario.DataVencimento = request.DataVencimento.Value.ToUniversalTime();
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Crediário {Id} editado pelo admin {AdminId}", id, GetUserId());

        return Ok(MapToDto(crediario));
    }

    // -------------------------------------------------------------------------
    // POST /api/crediarios/{id}/pagamento
    // -------------------------------------------------------------------------
    [HttpPost("{id:guid}/pagamento")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<CrediariosDto>> RegistrarPagamento(
        Guid id, [FromBody] RegistrarPagamentoRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var adminId   = GetUserId();
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (crediario == null)
            return NotFound(new { Message = "Crediário não encontrado." });

        if (crediario.Status == CrediariosStatus.Pago)
            return BadRequest(new { Message = "Crediário já está quitado." });

        var saldoAtual = crediario.SaldoRestanteEmCentavos;
        if (request.ValorEmCentavos > saldoAtual)
            return BadRequest(new
            {
                Message = $"Pagamento de R$ {request.ValorEmCentavos / 100m:N2} excede o saldo restante de R$ {saldoAtual / 100m:N2}."
            });

        // Registra o pagamento parcial
        var pagamento = new PagamentoCrediario
        {
            CrediarioId    = id,
            ValorEmCentavos = request.ValorEmCentavos,
            FormaPagamento  = request.FormaPagamento,
            Observacao      = request.Observacao,
            AdminId         = adminId,
        };
        _db.PagamentosCrediario.Add(pagamento);

        crediario.ValorPagoEmCentavos += request.ValorEmCentavos;

        // Quita automaticamente se saldo chegou a zero (tolerância de 1 centavo para arredondamentos)
        if (crediario.SaldoRestanteEmCentavos <= 1)
        {
            crediario.Status         = CrediariosStatus.Pago;
            crediario.DataPagamento  = DateTime.UtcNow;
            crediario.PagoPorAdminId = adminId;

            _logger.LogInformation(
                "Crediário {Id} quitado via pagamento parcial pelo admin {AdminId} — R$ {Valor:N2}",
                id, adminId, crediario.ValorEmReais);

            if (!string.IsNullOrWhiteSpace(crediario.User?.Email))
                _ = _email.SendCrediarioPagoAsync(
                    crediario.User.Email, crediario.User.Name, crediario.ValorEmReais);
        }
        else
        {
            _logger.LogInformation(
                "Crediário {Id}: pagamento parcial de R$ {Valor:N2} registrado pelo admin {AdminId}. Saldo restante: R$ {Saldo:N2}",
                id, request.ValorEmCentavos / 100m, adminId, crediario.SaldoRestanteEmReais);
        }

        await _db.SaveChangesAsync();
        return Ok(MapToDto(crediario));
    }

    // -------------------------------------------------------------------------
    // DELETE /api/crediarios/{id}
    // -------------------------------------------------------------------------
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Deletar(Guid id)
    {
        var crediario = await _db.Crediarios
            .Include(c => c.Pagamentos)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (crediario == null)
            return NotFound(new { Message = "Crediário não encontrado." });

        // Impede deleção se há qualquer pagamento registrado.
        // Um crediário com pagamento representa dinheiro já recebido — apagá-lo
        // removeria o histórico financeiro sem desfazer a receita original.
        if (crediario.ValorPagoEmCentavos > 0)
            return BadRequest(new
            {
                Message = $"Não é possível excluir este crediário pois já possui R$ {crediario.ValorPagoEmCentavos / 100m:N2} registrados como pagos. " +
                          "Exclua apenas crediários sem nenhum pagamento registrado."
            });

        _db.Crediarios.Remove(crediario);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Crediário {Id} excluído pelo admin {AdminId}", id, GetUserId());
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CrediariosDto MapToDto(Crediario c)
    {
        var agora   = DateTime.UtcNow;
        var vencido = c.Status == CrediariosStatus.Aberto && c.DataVencimento < agora;
        var dias    = (int)Math.Round((c.DataVencimento - agora).TotalDays);

        // Combina itens da comanda vinculada + itens de venda avulsa (frente de caixa).
        // Um crediário pode acumular de ambas as origens — exibe tudo junto.
        var fromComanda = c.Comanda?.Items
            .OrderBy(i => i.AddedAt)
            .Select(i => new ItemCrediarioDto
            {
                ItemName         = i.ItemNameSnapshot,
                Quantity         = i.Quantity,
                UnitPriceInReais = i.UnitPriceInCents / 100m,
                SubtotalInReais  = i.SubtotalInCents  / 100m,
            })
            .ToList() ?? new List<ItemCrediarioDto>();

        var fromJson = string.IsNullOrWhiteSpace(c.ItensJson)
            ? new List<ItemCrediarioDto>()
            : JsonSerializer.Deserialize<List<ItemCrediarioDto>>(c.ItensJson)
              ?? new List<ItemCrediarioDto>();

        var todosItens = fromComanda.Concat(fromJson).ToList();

        return new CrediariosDto
        {
            Id                   = c.Id,
            UserId               = c.UserId,
            UserName             = c.User?.Name ?? string.Empty,
            UserEmail            = c.User?.Email,
            ComandaId            = c.ComandaId,
            ValorEmReais         = c.ValorEmReais,
            ValorPagoEmReais     = c.ValorPagoEmReais,
            SaldoRestanteEmReais = c.SaldoRestanteEmReais,
            DataAbertura         = c.DataAbertura,
            DataVencimento       = c.DataVencimento,
            DataPagamento        = c.DataPagamento,
            Status               = vencido ? "Vencido" : c.Status.ToString(),
            Observacao           = c.Observacao,
            Vencido              = vencido,
            DiasRestantes        = dias,
            Pagamentos           = c.Pagamentos
                .OrderBy(p => p.CreatedAt)
                .Select(p => new PagamentoCrediarioDto
                {
                    Id             = p.Id,
                    ValorEmReais   = p.ValorEmReais,
                    FormaPagamento = p.FormaPagamento,
                    Observacao     = p.Observacao,
                    CreatedAt      = p.CreatedAt,
                }).ToList(),
            ItensComanda = todosItens,
        };
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token inválido: identificador de usuário ausente.");
        return id;
    }
}
