using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/reservations")]
[Produces("application/json")]
public class ReservationController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReservationController(AppDbContext db) => _db = db;

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id)) throw new UnauthorizedAccessException();
        return id;
    }

    // GET /api/reservations/mine — reservas do usuário logado
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetUserId();
        var list = await _db.ProductReservations
            .Where(r => r.UserId == userId)
            .Include(r => r.Product)
            .Include(r => r.Variant)
            .OrderByDescending(r => r.ReservedAt)
            .ToListAsync();

        return Ok(list.Select(r => ToDto(r)));
    }

    // POST /api/reservations — cria reserva (somente via site)
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest req)
    {
        var userId = GetUserId();

        var product = await _db.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == req.ProductId && p.IsActive);

        if (product is null) return NotFound(new { Message = "Produto não encontrado." });

        var qty = req.Quantity < 1 ? 1 : req.Quantity;

        // Calcula estoque disponível descontando reservas ativas
        int stockBase;
        if (req.VariantId.HasValue)
        {
            var variant = product.Variants.FirstOrDefault(v => v.Id == req.VariantId.Value);
            if (variant is null) return BadRequest(new { Message = "Variante não encontrada." });
            stockBase = variant.StockQuantity;
        }
        else
        {
            stockBase = product.StockQuantity;
        }

        var activeReservedQty = await _db.ProductReservations
            .Where(r => r.ProductId == req.ProductId
                     && r.VariantId == req.VariantId
                     && r.Status == "active"
                     && r.ExpiresAt > DateTime.UtcNow)
            .SumAsync(r => r.Quantity);

        if (stockBase - activeReservedQty < qty)
            return BadRequest(new { Message = $"Estoque insuficiente. Disponível para reserva: {Math.Max(0, stockBase - activeReservedQty)}." });

        var reservation = new ProductReservation
        {
            UserId    = userId,
            ProductId = req.ProductId,
            VariantId = req.VariantId,
            Quantity  = qty,
            Notes     = req.Notes,
            ExpiresAt = DateTime.UtcNow.AddHours(48),
        };

        _db.ProductReservations.Add(reservation);
        await _db.SaveChangesAsync();

        await _db.Entry(reservation).Reference(r => r.Product).LoadAsync();
        await _db.Entry(reservation).Reference(r => r.Variant).LoadAsync();

        return Ok(ToDto(reservation));
    }

    // DELETE /api/reservations/{id} — cancela reserva própria
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = GetUserId();
        var res = await _db.ProductReservations.FirstOrDefaultAsync(r => r.Id == id);

        if (res is null) return NotFound();
        if (res.UserId != userId && !User.IsInRole("Admin")) return Forbid();
        if (res.Status != "active") return BadRequest(new { Message = "Reserva não está ativa." });

        res.Status      = "cancelled";
        res.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // GET /api/reservations — lista todas [AdminOnly]
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] Guid?   userId = null,
        [FromQuery] int     page   = 1)
    {
        var q = _db.ProductReservations
            .Include(r => r.User)
            .Include(r => r.Product)
            .Include(r => r.Variant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        if (userId.HasValue)                    q = q.Where(r => r.UserId == userId.Value);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(r => r.ReservedAt)
            .Skip((page - 1) * 30).Take(30)
            .ToListAsync();

        return Ok(new { items = items.Select(ToDto), total, totalPages = (int)Math.Ceiling(total / 30.0) });
    }

    // PUT /api/reservations/{id}/status — admin atualiza status
    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest req)
    {
        var res = await _db.ProductReservations
            .Include(r => r.Product).ThenInclude(p => p.Variants)
            .Include(r => r.Variant)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (res is null) return NotFound();
        if (res.Status != "active") return BadRequest(new { Message = "Reserva não está ativa." });

        res.Status = req.Status;

        if (req.Status == "fulfilled")
        {
            res.FulfilledAt = DateTime.UtcNow;
            // Decrementa estoque ao confirmar
            if (res.VariantId.HasValue && res.Variant is not null)
            {
                res.Variant.StockQuantity = Math.Max(0, res.Variant.StockQuantity - res.Quantity);
                res.Variant.UpdatedAt     = DateTime.UtcNow;
            }
            else
            {
                res.Product.StockQuantity = Math.Max(0, res.Product.StockQuantity - res.Quantity);
                res.Product.UpdatedAt     = DateTime.UtcNow;
            }
        }
        else if (req.Status == "cancelled")
        {
            res.CancelledAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(ToDto(res));
    }

    // GET /api/reservations/product/{productId} — quantidade reservada (público)
    [HttpGet("product/{productId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProductReservedQty(Guid productId, [FromQuery] Guid? variantId = null)
    {
        var reserved = await _db.ProductReservations
            .Where(r => r.ProductId == productId
                     && r.VariantId == variantId
                     && r.Status == "active"
                     && r.ExpiresAt > DateTime.UtcNow)
            .SumAsync(r => r.Quantity);

        return Ok(new { productId, variantId, reservedQuantity = reserved });
    }

    private static object ToDto(ProductReservation r) => new
    {
        r.Id,
        r.UserId,
        userName       = r.User?.Name,
        r.ProductId,
        productName    = r.Product?.Name,
        productImageUrl= r.Product?.ImageUrl,
        r.VariantId,
        variantLabel   = r.Variant?.Label,
        r.Quantity,
        r.Status,
        r.Notes,
        r.ReservedAt,
        r.ExpiresAt,
        r.FulfilledAt,
        r.CancelledAt,
        isExpired      = r.IsExpired,
    };
}

public class CreateReservationRequest
{
    public Guid  ProductId { get; init; }
    public Guid? VariantId { get; init; }
    public int   Quantity  { get; init; } = 1;
    public string? Notes   { get; init; }
}

public class UpdateStatusRequest
{
    public string Status { get; init; } = "";
}
