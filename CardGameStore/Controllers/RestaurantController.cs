using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

/// <summary>
/// Recursos exclusivos do módulo Restaurante. A rota inteira exige o módulo
/// contratado no tenant; esconder o menu no frontend nunca é a única proteção.
/// </summary>
[ApiController]
[Route("api/restaurante")]
[Authorize(Policy = "AdminOnly")]
[RequireModule("restaurante")]
[Produces("application/json")]
public class RestaurantController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public RestaurantController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet("areas-producao")]
    public async Task<ActionResult<IReadOnlyList<RestaurantProductionAreaDto>>> ListProductionAreas(
        [FromQuery] bool includeInactive = false)
    {
        var query = _db.RestaurantProductionAreas.AsNoTracking();
        if (!includeInactive)
            query = query.Where(area => area.IsActive);

        var areas = await query
            .OrderBy(area => area.DisplayOrder)
            .ThenBy(area => area.Name)
            .Select(area => ToDto(area))
            .ToListAsync();

        return Ok(areas);
    }

    [HttpPost("areas-producao")]
    public async Task<ActionResult<RestaurantProductionAreaDto>> CreateProductionArea(
        [FromBody] SaveRestaurantProductionAreaRequest request)
    {
        var normalizedName = request.Name.Trim();
        if (await _db.RestaurantProductionAreas.AnyAsync(area => area.Name.ToLower() == normalizedName.ToLower()))
            return Conflict(new { Message = "Já existe uma área de produção com este nome." });

        var area = new RestaurantProductionArea
        {
            Name = normalizedName,
            Description = NormalizeOptional(request.Description),
            Color = request.Color.ToUpperInvariant(),
            DisplayOrder = request.DisplayOrder,
        };

        _db.RestaurantProductionAreas.Add(area);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CriouAreaProducaoRestaurante", "RestaurantProductionArea", area.Id.ToString(),
            details: area.Name, httpContext: HttpContext);

        return CreatedAtAction(nameof(ListProductionAreas), ToDto(area));
    }

    [HttpPut("areas-producao/{id:guid}")]
    public async Task<ActionResult<RestaurantProductionAreaDto>> UpdateProductionArea(
        Guid id, [FromBody] SaveRestaurantProductionAreaRequest request)
    {
        var area = await _db.RestaurantProductionAreas.FindAsync(id);
        if (area is null) return NotFound();

        var normalizedName = request.Name.Trim();
        if (await _db.RestaurantProductionAreas.AnyAsync(other =>
                other.Id != id && other.Name.ToLower() == normalizedName.ToLower()))
            return Conflict(new { Message = "Já existe uma área de produção com este nome." });

        area.Name = normalizedName;
        area.Description = NormalizeOptional(request.Description);
        area.Color = request.Color.ToUpperInvariant();
        area.DisplayOrder = request.DisplayOrder;
        area.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ToDto(area));
    }

    /// <summary>Desativa sem apagar: pedidos futuros poderão manter a referência histórica.</summary>
    [HttpDelete("areas-producao/{id:guid}")]
    public async Task<IActionResult> DeactivateProductionArea(Guid id)
    {
        var area = await _db.RestaurantProductionAreas.FindAsync(id);
        if (area is null) return NotFound();
        if (!area.IsActive) return NoContent();

        area.IsActive = false;
        area.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("DesativouAreaProducaoRestaurante", "RestaurantProductionArea", area.Id.ToString(),
            details: area.Name, httpContext: HttpContext);

        return NoContent();
    }

    [HttpPost("areas-producao/{id:guid}/reativar")]
    public async Task<ActionResult<RestaurantProductionAreaDto>> ReactivateProductionArea(Guid id)
    {
        var area = await _db.RestaurantProductionAreas.FindAsync(id);
        if (area is null) return NotFound();

        area.IsActive = true;
        area.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("ReativouAreaProducaoRestaurante", "RestaurantProductionArea", area.Id.ToString(),
            details: area.Name, httpContext: HttpContext);

        return Ok(ToDto(area));
    }

    private static RestaurantProductionAreaDto ToDto(RestaurantProductionArea area) => new()
    {
        Id = area.Id,
        Name = area.Name,
        Description = area.Description,
        Color = area.Color,
        DisplayOrder = area.DisplayOrder,
        IsActive = area.IsActive,
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
