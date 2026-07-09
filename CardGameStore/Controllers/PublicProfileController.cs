// =============================================================================
// PublicProfileController.cs — Perfil público de um usuário
//
// GET /api/profile/{userId} — público, sem autenticação
//
// Retorna dados públicos do usuário: nome, avatar, data de cadastro,
// total de compras realizadas e pontos de fidelidade.
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/profile")]
[Produces("application/json")]
public class PublicProfileController : ControllerBase
{
    private readonly AppDbContext _db;

    public PublicProfileController(AppDbContext db) => _db = db;

    // ── GET /api/profile/{userId} ─────────────────────────────────────────────

    [HttpGet("{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicProfile(Guid userId)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive && u.DeletedAt == null);

        if (user is null)
            return NotFound(new { Message = "Usuário não encontrado." });

        var totalCompras = await _db.Comandas
            .AsNoTracking()
            .CountAsync(c => c.UserId == userId && c.Status == ComandaStatus.Fechada);

        var result = new
        {
            id              = user.Id,
            name            = user.Name,
            profileImageUrl = user.ProfileImageUrl,
            memberSince     = user.CreatedAt.Date,
            totalCompras,
            pointsBalance   = user.PointsBalance,
        };

        return Ok(result);
    }
}
