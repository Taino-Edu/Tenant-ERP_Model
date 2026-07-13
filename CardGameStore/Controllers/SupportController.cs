// =============================================================================
// SupportController.cs — Chamados de suporte, lado do lojista.
//
// POST /api/support/tickets              → abre chamado
// GET  /api/support/tickets               → lista os chamados da própria loja
// GET  /api/support/tickets/{id}          → detalha um chamado (com mensagens)
// POST /api/support/tickets/{id}/messages → responde um chamado
//
// Os tickets vivem no catálogo (CatalogDbContext, schema "public"), não no
// schema do tenant — o dono da plataforma precisa enxergá-los cross-tenant
// (ver endpoints em PlatformController). ITenantContext já vem populado pelo
// TenantResolutionMiddleware da própria requisição, sem precisar de scope
// manual (diferente do PlatformController, que lê OUTROS tenants).
// =============================================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/support")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public class SupportController : ControllerBase
{
    private readonly CatalogDbContext  _catalog;
    private readonly ITenantContext    _tenantContext;
    private readonly ILogger<SupportController> _logger;

    public SupportController(CatalogDbContext catalog, ITenantContext tenantContext, ILogger<SupportController> logger)
    {
        _catalog       = catalog;
        _tenantContext = tenantContext;
        _logger        = logger;
    }

    private static SupportTicketDto ToDto(SupportTicket t) => new()
    {
        Id                = t.Id,
        TenantId          = t.TenantId,
        Subject           = t.Subject,
        Status            = t.Status.ToString(),
        CreatedByUserName = t.CreatedByUserName,
        CreatedAt         = t.CreatedAt,
        UpdatedAt         = t.UpdatedAt,
        MessageCount      = t.Messages.Count,
    };

    private static SupportTicketMessageDto ToDto(SupportTicketMessage m) => new()
    {
        Id         = m.Id,
        AuthorRole = m.AuthorRole.ToString(),
        AuthorName = m.AuthorName,
        Body       = m.Body,
        CreatedAt  = m.CreatedAt,
    };

    private (Guid Id, string Name) GetCurrentUser()
    {
        var id   = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
        var name = User.FindFirst(JwtRegisteredClaimNames.Name)?.Value ?? "Admin";
        return (id != null && Guid.TryParse(id.Value, out var guid) ? guid : Guid.Empty, name);
    }

    /// <summary>Abre um chamado de suporte com a plataforma.</summary>
    /// <param name="request">Assunto e mensagem inicial.</param>
    [HttpPost("tickets")]
    public async Task<IActionResult> Create([FromBody] CreateSupportTicketRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (userId, userName) = GetCurrentUser();

        var ticket = new SupportTicket
        {
            TenantId          = _tenantContext.TenantId,
            Subject           = request.Subject.Trim(),
            CreatedByUserId   = userId,
            CreatedByUserName = userName,
        };
        ticket.Messages.Add(new SupportTicketMessage
        {
            AuthorRole   = SupportTicketAuthorRole.Tenant,
            AuthorUserId = userId,
            AuthorName   = userName,
            Body         = request.Body.Trim(),
        });

        _catalog.SupportTickets.Add(ticket);
        await _catalog.SaveChangesAsync();

        _logger.LogInformation("Chamado de suporte {Id} aberto pelo tenant {TenantId}", ticket.Id, ticket.TenantId);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ToDto(ticket));
    }

    /// <summary>Lista os chamados de suporte da própria loja, mais recente primeiro.</summary>
    [HttpGet("tickets")]
    public async Task<IActionResult> List()
    {
        var tickets = await _catalog.SupportTickets
            .Include(t => t.Messages)
            .Where(t => t.TenantId == _tenantContext.TenantId)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync();

        return Ok(tickets.Select(ToDto));
    }

    /// <summary>Detalha um chamado da própria loja, com todas as mensagens.</summary>
    /// <param name="id">Id do chamado.</param>
    [HttpGet("tickets/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var ticket = await _catalog.SupportTickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == _tenantContext.TenantId);

        // 404 em vez de 403 quando o TenantId não bate — não confirma nem a
        // existência do ticket de outra loja.
        if (ticket is null) return NotFound();

        var dto = new SupportTicketDetailDto
        {
            Id = ticket.Id, TenantId = ticket.TenantId, Subject = ticket.Subject,
            Status = ticket.Status.ToString(), CreatedByUserName = ticket.CreatedByUserName,
            CreatedAt = ticket.CreatedAt, UpdatedAt = ticket.UpdatedAt,
            MessageCount = ticket.Messages.Count,
            Messages = ticket.Messages.OrderBy(m => m.CreatedAt).Select(ToDto).ToList(),
        };
        return Ok(dto);
    }

    /// <summary>Responde um chamado da própria loja.</summary>
    /// <param name="id">Id do chamado.</param>
    /// <param name="request">Texto da mensagem.</param>
    [HttpPost("tickets/{id:guid}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] CreateSupportMessageRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ticket = await _catalog.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == _tenantContext.TenantId);
        if (ticket is null) return NotFound();

        var (userId, userName) = GetCurrentUser();

        _catalog.SupportTicketMessages.Add(new SupportTicketMessage
        {
            TicketId     = ticket.Id,
            AuthorRole   = SupportTicketAuthorRole.Tenant,
            AuthorUserId = userId,
            AuthorName   = userName,
            Body         = request.Body.Trim(),
        });
        ticket.UpdatedAt = DateTime.UtcNow;

        await _catalog.SaveChangesAsync();
        return NoContent();
    }
}
