// =============================================================================
// AuditController.cs — Endpoints admin para consulta de audit logs
// GET /api/audit → lista paginada de registros de auditoria (Admin only)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lista registros de auditoria com paginação e filtros.
    /// Somente Admin — acesso restrito para proteger informações de operação.
    /// </summary>
    /// <param name="page">Número da página (base 1, padrão 1).</param>
    /// <param name="pageSize">Registros por página (padrão 50, máximo 200).</param>
    /// <param name="entityType">Filtro por tipo de entidade (ex: "User", "LgpdRequest").</param>
    /// <param name="actorUserId">Filtro por ID do ator.</param>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogPagedResponse), 200)]
    public async Task<IActionResult> List(
        [FromQuery] int    page         = 1,
        [FromQuery] int    pageSize     = 50,
        [FromQuery] string? entityType  = null,
        [FromQuery] string? action      = null,
        [FromQuery] string? actorUserId = null)
    {
        // Limites de segurança
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrWhiteSpace(actorUserId))
            query = query.Where(a => a.ActorUserId == actorUserId);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto
            {
                Id            = a.Id,
                ActorUserId   = a.ActorUserId,
                ActorUserName = a.ActorUserName,
                Action        = a.Action,
                EntityType    = a.EntityType,
                EntityId      = a.EntityId,
                Details       = a.Details,
                CreatedAt     = a.CreatedAt,
            })
            .ToListAsync();

        return Ok(new AuditLogPagedResponse
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize,
            TotalPages = totalPages,
        });
    }
}
