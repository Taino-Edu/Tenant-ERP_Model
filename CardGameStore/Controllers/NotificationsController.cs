// =============================================================================
// NotificationsController.cs — Notificações in-app por usuário
// GET    /api/notifications          → lista (não lidas primeiro, max 50)
// GET    /api/notifications/unread-count → só o contador
// PATCH  /api/notifications/{id}/read   → marca como lida
// PATCH  /api/notifications/read-all    → marca todas como lidas
// DELETE /api/notifications/{id}        → remove
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public NotificationsController(AppDbContext db) { _db = db; }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var list = await _db.Notifications
            .Where(n => n.UserId == uid)
            .OrderBy(n => n.ReadAt.HasValue)
            .ThenByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new {
                n.Id, n.Title, n.Body, n.Link, n.ImageUrl,
                n.CreatedAt, n.ReadAt, n.IsRead
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var count = await _db.Notifications
            .CountAsync(n => n.UserId == uid && n.ReadAt == null);

        return Ok(new { count });
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var n = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == uid);
        if (n == null) return NotFound();

        n.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        await _db.Notifications
            .Where(n => n.UserId == uid && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        await _db.Notifications
            .Where(n => n.Id == id && n.UserId == uid)
            .ExecuteDeleteAsync();

        return NoContent();
    }
}
