using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Hubs;
using CardGameStore.Middleware;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace CardGameStore.Controllers;

/// <summary>
/// Recursos exclusivos do módulo Restaurante. A rota inteira exige o módulo
/// contratado no tenant; esconder o menu no frontend nunca é a única proteção.
/// </summary>
[ApiController]
[Route("api/restaurante")]
[Authorize(Policy = "AdminOnly")]
[RequireModule("restaurante")]
[RequireOperatorPermission(Permissao.Restaurante)]
[Produces("application/json")]
public class RestaurantController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IHubContext<ComandaHub> _hub;
    private readonly ITenantContext _tenant;

    public RestaurantController(AppDbContext db, IAuditService audit, IHubContext<ComandaHub> hub, ITenantContext tenant)
    {
        _db = db;
        _audit = audit;
        _hub = hub;
        _tenant = tenant;
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

    /// <summary>Vincula um produto a uma área. Null remove o vínculo sem alterar comandas antigas.</summary>
    [HttpGet("produtos")]
    public async Task<ActionResult<IReadOnlyList<RestaurantProductMappingDto>>> ListProductMappings()
    {
        var products = await _db.Products.AsNoTracking()
            .Where(product => product.IsActive)
            .OrderBy(product => product.Category)
            .ThenBy(product => product.Name)
            .Select(product => new RestaurantProductMappingDto
            {
                Id = product.Id,
                Name = product.Name,
                Category = product.Category,
                ProductionAreaId = product.RestaurantProductionAreaId,
            })
            .ToListAsync();
        return Ok(products);
    }

    /// <summary>Vincula um produto a uma área. Null remove o vínculo sem alterar comandas antigas.</summary>
    [HttpPut("produtos/{productId:guid}/area-producao")]
    public async Task<IActionResult> AssignProductProductionArea(
        Guid productId, [FromBody] AssignProductProductionAreaRequest request)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return NotFound(new { Message = "Produto não encontrado." });

        if (request.ProductionAreaId.HasValue)
        {
            var areaExists = await _db.RestaurantProductionAreas.AnyAsync(area =>
                area.Id == request.ProductionAreaId && area.IsActive);
            if (!areaExists)
                return BadRequest(new { Message = "Área de produção inválida ou inativa." });
        }

        product.RestaurantProductionAreaId = request.ProductionAreaId;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("VinculouProdutoAreaProducao", "Product", product.Id.ToString(),
            details: request.ProductionAreaId?.ToString() ?? "sem área", httpContext: HttpContext);

        return Ok(new { product.Id, product.RestaurantProductionAreaId });
    }

    /// <summary>Fila operacional derivada dos itens das comandas ativas.</summary>
    [HttpGet("fila-producao")]
    public async Task<ActionResult<IReadOnlyList<RestaurantProductionItemDto>>> ListProductionQueue(
        [FromQuery] Guid? productionAreaId = null)
    {
        var query = _db.ComandaItems.AsNoTracking()
            .Include(item => item.Comanda).ThenInclude(comanda => comanda.User)
            .Where(item => item.ProductionAreaId != null &&
                           item.ProductionStatus != RestaurantProductionStatus.Servido &&
                           (item.Comanda.Status == ComandaStatus.Aberta ||
                            item.Comanda.Status == ComandaStatus.EmAndamento));

        if (productionAreaId.HasValue)
            query = query.Where(item => item.ProductionAreaId == productionAreaId);

        var items = await query
            .OrderBy(item => item.ProductionStatus)
            .ThenBy(item => item.AddedAt)
            .Select(item => new RestaurantProductionItemDto
            {
                ComandaId = item.ComandaId,
                TableIdentifier = item.Comanda.TableIdentifier,
                UserName = item.Comanda.User.Name,
                ItemId = item.Id,
                ItemName = item.ItemNameSnapshot,
                Quantity = item.Quantity,
                ProductionAreaId = item.ProductionAreaId!.Value,
                ProductionAreaName = item.ProductionAreaNameSnapshot ?? "Produção",
                Status = item.ProductionStatus!.Value.ToString(),
                AddedAt = item.AddedAt,
                ProductionStartedAt = item.ProductionStartedAt,
                ProductionReadyAt = item.ProductionReadyAt,
                ProductionServedAt = item.ProductionServedAt,
                ComandaNotes = item.Comanda.Notes,
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>Avança o preparo do item: Recebido → Preparando → Pronto → Servido.</summary>
    [HttpPut("comandas/{comandaId:guid}/itens/{itemId:guid}/status-producao")]
    public async Task<ActionResult<RestaurantProductionItemDto>> UpdateProductionStatus(
        Guid comandaId, Guid itemId, [FromBody] UpdateProductionStatusRequest request)
    {
        if (!Enum.TryParse<RestaurantProductionStatus>(request.Status, out var requestedStatus))
            return BadRequest(new { Message = "Status de produção inválido." });

        var item = await _db.ComandaItems
            .Include(current => current.Comanda).ThenInclude(comanda => comanda.User)
            .FirstOrDefaultAsync(current => current.Id == itemId && current.ComandaId == comandaId);
        if (item is null) return NotFound(new { Message = "Item da comanda não encontrado." });
        if (!item.ProductionAreaId.HasValue || !item.ProductionStatus.HasValue)
            return BadRequest(new { Message = "Este item não está vinculado a uma área de produção." });

        var currentStatus = item.ProductionStatus.Value;
        if (requestedStatus != currentStatus)
        {
            var expectedNext = currentStatus switch
            {
                RestaurantProductionStatus.Recebido => RestaurantProductionStatus.Preparando,
                RestaurantProductionStatus.Preparando => RestaurantProductionStatus.Pronto,
                RestaurantProductionStatus.Pronto => RestaurantProductionStatus.Servido,
                _ => (RestaurantProductionStatus?)null,
            };
            if (expectedNext != requestedStatus)
                return Conflict(new { Message = $"Transição inválida: {currentStatus} → {requestedStatus}." });

            item.ProductionStatus = requestedStatus;
            var now = DateTime.UtcNow;
            if (requestedStatus == RestaurantProductionStatus.Preparando) item.ProductionStartedAt = now;
            if (requestedStatus == RestaurantProductionStatus.Pronto) item.ProductionReadyAt = now;
            if (requestedStatus == RestaurantProductionStatus.Servido) item.ProductionServedAt = now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("AtualizouStatusProducao", "ComandaItem", item.Id.ToString(),
                details: $"{currentStatus} -> {requestedStatus}", httpContext: HttpContext);
            var eventPayload = new { item.ComandaId, ItemId = item.Id, Status = requestedStatus.ToString() };
            await _hub.Clients.Group(ComandaHub.GetAdminGroup(_tenant.TenantId))
                .SendAsync("ProductionStatusUpdated", eventPayload);
            await _hub.Clients.Group(ComandaHub.GetComandaGroup(_tenant.TenantId, item.ComandaId))
                .SendAsync("ProductionStatusUpdated", eventPayload);
        }

        return Ok(ToProductionItemDto(item));
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

    private static RestaurantProductionItemDto ToProductionItemDto(ComandaItem item) => new()
    {
        ComandaId = item.ComandaId,
        TableIdentifier = item.Comanda.TableIdentifier,
        UserName = item.Comanda.User.Name,
        ItemId = item.Id,
        ItemName = item.ItemNameSnapshot,
        Quantity = item.Quantity,
        ProductionAreaId = item.ProductionAreaId!.Value,
        ProductionAreaName = item.ProductionAreaNameSnapshot ?? "Produção",
        Status = item.ProductionStatus!.Value.ToString(),
        AddedAt = item.AddedAt,
        ProductionStartedAt = item.ProductionStartedAt,
        ProductionReadyAt = item.ProductionReadyAt,
        ProductionServedAt = item.ProductionServedAt,
        ComandaNotes = item.Comanda.Notes,
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
