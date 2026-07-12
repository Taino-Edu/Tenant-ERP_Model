using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/products/{productId:guid}/variants")]
[Produces("application/json")]
public class ProductVariantController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductVariantController(AppDbContext db) => _db = db;

    // GET /api/products/{productId}/variants
    // Público — precisa listar as opções de variante na hora da venda (PDV,
    // autoatendimento do cliente), então não pode exigir AdminOnly nem módulo pago.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(Guid productId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return NotFound(new { Message = "Produto não encontrado." });

        var variants = await _db.ProductVariants
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.Color).ThenBy(v => v.Size)
            .Select(v => ToDto(v, product.PriceInCents))
            .ToListAsync();

        return Ok(variants);
    }

    // POST /api/products/{productId}/variants
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [RequireModule("estoque")]
    public async Task<IActionResult> Create(Guid productId, [FromBody] VariantRequest req)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return NotFound(new { Message = "Produto não encontrado." });

        var variant = new ProductVariant
        {
            ProductId    = productId,
            Size         = req.Size?.Trim(),
            Color        = req.Color?.Trim(),
            StockQuantity = req.StockQuantity,
            PriceInCents = req.PriceInCents,
            Sku          = req.Sku?.Trim(),
        };

        _db.ProductVariants.Add(variant);
        product.HasVariants = true;
        await _db.SaveChangesAsync();

        return Ok(ToDto(variant, product.PriceInCents));
    }

    // POST /api/products/{productId}/variants/bulk — cria a grade completa
    [HttpPost("bulk")]
    [Authorize(Policy = "AdminOnly")]
    [RequireModule("estoque")]
    public async Task<IActionResult> BulkCreate(Guid productId, [FromBody] BulkRequest req)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return NotFound(new { Message = "Produto não encontrado." });

        var sizes  = req.Sizes  ?? new[] { (string?)null };
        var colors = req.Colors ?? new[] { (string?)null };

        var created = new List<ProductVariant>();
        foreach (var color in colors)
        {
            foreach (var size in sizes)
            {
                // Não duplica combinações já existentes
                var exists = await _db.ProductVariants.AnyAsync(v =>
                    v.ProductId == productId &&
                    v.Size == size && v.Color == color);
                if (exists) continue;

                var v = new ProductVariant
                {
                    ProductId     = productId,
                    Size          = size?.Trim(),
                    Color         = color?.Trim(),
                    StockQuantity = req.BaseStockQuantity,
                };
                _db.ProductVariants.Add(v);
                created.Add(v);
            }
        }

        product.HasVariants = true;
        await _db.SaveChangesAsync();

        return Ok(created.Select(v => ToDto(v, product.PriceInCents)));
    }

    // PUT /api/products/{productId}/variants/{variantId}
    [HttpPut("{variantId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [RequireModule("estoque")]
    public async Task<IActionResult> Update(Guid productId, Guid variantId, [FromBody] VariantRequest req)
    {
        var product = await _db.Products.FindAsync(productId);
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId);

        if (product is null || variant is null) return NotFound();

        variant.Size          = req.Size?.Trim()  ?? variant.Size;
        variant.Color         = req.Color?.Trim() ?? variant.Color;
        variant.StockQuantity = req.StockQuantity;
        variant.PriceInCents  = req.PriceInCents;
        variant.Sku           = req.Sku?.Trim()   ?? variant.Sku;
        variant.UpdatedAt     = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToDto(variant, product.PriceInCents));
    }

    // DELETE /api/products/{productId}/variants/{variantId}
    [HttpDelete("{variantId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [RequireModule("estoque")]
    public async Task<IActionResult> Delete(Guid productId, Guid variantId)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId);
        if (variant is null) return NotFound();

        _db.ProductVariants.Remove(variant);
        await _db.SaveChangesAsync();

        // Se não sobrou nenhuma variante, desliga has_variants
        var remaining = await _db.ProductVariants.CountAsync(v => v.ProductId == productId);
        if (remaining == 0)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product is not null) { product.HasVariants = false; await _db.SaveChangesAsync(); }
        }

        return NoContent();
    }

    private static object ToDto(ProductVariant v, int parentPriceCents) => new
    {
        v.Id, v.Size, v.Color, v.StockQuantity,
        v.PriceInCents,
        effectivePriceCents = v.PriceInCents ?? parentPriceCents,
        v.Sku,
        label = v.Label,
        v.CreatedAt, v.UpdatedAt,
    };
}

public class VariantRequest
{
    public string? Size          { get; init; }
    public string? Color         { get; init; }
    public int     StockQuantity { get; init; }
    public int?    PriceInCents  { get; init; }
    public string? Sku           { get; init; }
}

public class BulkRequest
{
    public string[]? Sizes            { get; init; }
    public string[]? Colors           { get; init; }
    public int       BaseStockQuantity { get; init; }
}
