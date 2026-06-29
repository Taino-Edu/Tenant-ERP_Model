// =============================================================================
// PushController.cs — Gerencia subscrições de browser push (VAPID/WebPush)
// POST   /api/push/subscribe         → salva/atualiza subscrição do browser
// DELETE /api/push/subscribe         → remove subscrição
// GET    /api/push/vapid-public-key  → devolve chave pública VAPID (anônimo)
// =============================================================================

using CardGameStore.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/push")]
public class PushController : ControllerBase
{
    private readonly AppDbContext  _db;
    private readonly IConfiguration _config;

    public PushController(AppDbContext db, IConfiguration config) { _db = db; _config = config; }

    // ── Chave pública VAPID — necessária para o browser montar a subscrição ──

    [HttpGet("vapid-public-key")]
    [AllowAnonymous]
    public IActionResult GetPublicKey()
    {
        var key = _config["Vapid:PublicKey"];
        if (string.IsNullOrWhiteSpace(key))
            return NotFound(new { Message = "Push não configurado neste servidor." });
        return Ok(new { publicKey = key });
    }

    // ── Salvar/atualizar subscrição ───────────────────────────────────────────

    [HttpPost("subscribe")]
    [Authorize]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequest req)
    {
        var userId = GetUserId();

        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == req.Endpoint);

        if (existing is not null)
        {
            existing.P256dh = req.P256dh;
            existing.Auth   = req.Auth;
            existing.UserId = userId;
        }
        else
        {
            _db.PushSubscriptions.Add(new Models.PostgreSQL.PushSubscription
            {
                UserId   = userId,
                Endpoint = req.Endpoint,
                P256dh   = req.P256dh,
                Auth     = req.Auth,
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { Message = "Subscrito com sucesso." });
    }

    // ── Remover subscrição ────────────────────────────────────────────────────

    [HttpDelete("subscribe")]
    [Authorize]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest req)
    {
        var userId = GetUserId();
        var sub = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == req.Endpoint && s.UserId == userId);
        if (sub is not null)
        {
            _db.PushSubscriptions.Remove(sub);
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException();
    }
}

public class PushSubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh   { get; set; } = string.Empty;
    public string Auth     { get; set; } = string.Empty;
}

public class PushUnsubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;
}
