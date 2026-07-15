// =============================================================================
// UsageController.cs — Ingestão de eventos de uso (analytics de tela/tempo)
// vindos do beacon do frontend (UsageTracker). Escrita em lote, tenant-scoped
// (AppDbContext já isola por schema) — a leitura agregada cross-tenant fica
// em PlatformController.GetTenantUsage.
// =============================================================================

using System.Security.Claims;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/usage")]
[Authorize(Policy = "AdminOnly")]
[EnableRateLimiting("api")]
public class UsageController : ControllerBase
{
    private const int MaxBatchSize = 50;

    private readonly AppDbContext _db;

    public UsageController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Recebe um lote de eventos de navegação (tela + duração) do
    /// beacon do admin. Fire-and-forget do ponto de vista do frontend —
    /// falha aqui nunca deve virar erro visível pro usuário.</summary>
    /// <param name="events">Lote de eventos (máx. 50 por request).</param>
    [HttpPost("events")]
    public async Task<IActionResult> PostEvents([FromBody] List<UsageEventRequest> events)
    {
        if (events is null || events.Count == 0) return Ok();
        if (events.Count > MaxBatchSize) events = events.Take(MaxBatchSize).ToList();

        var userId   = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;
        Guid.TryParse(userId, out var userGuid);

        var rows = events
            .Where(e => !string.IsNullOrWhiteSpace(e.Path))
            .Select(e => new PageViewEvent
            {
                UserId     = userGuid == Guid.Empty ? null : userGuid,
                UserName   = userName,
                Path       = e.Path.Length > 200 ? e.Path[..200] : e.Path,
                OccurredAt = e.OccurredAt == default ? DateTime.UtcNow : e.OccurredAt,
                DurationMs = e.DurationMs,
            });

        _db.PageViewEvents.AddRange(rows);
        await _db.SaveChangesAsync();

        return Ok();
    }
}

public class UsageEventRequest
{
    public string   Path       { get; init; } = string.Empty;
    public int?     DurationMs { get; init; }
    public DateTime OccurredAt { get; init; }
}
