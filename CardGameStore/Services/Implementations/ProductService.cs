// =============================================================================
// ProductService.cs — Implementação de Produtos (estoque físico)
// =============================================================================
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    public ProductService(AppDbContext db) { _db = db; }

    public async Task<IEnumerable<Product>> GetAllActiveAsync()
    {
        var list = await _db.Products
            .Where(p => p.IsActive && p.ShowOnMarketplace)
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync();
        await ApplyVariantStockAsync(list);
        return list;
    }

    public async Task<IEnumerable<Product>> GetAllForAdminAsync()
    {
        var list = await _db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync();
        await ApplyVariantStockAsync(list);
        return list;
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        var list = await _db.Products
            .Where(p => p.IsActive && p.ShowOnMarketplace && p.Category == category)
            .AsNoTracking()
            .ToListAsync();
        await ApplyVariantStockAsync(list);
        return list;
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        var p = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (p?.HasVariants == true)
            p.StockQuantity = await _db.Set<ProductVariant>()
                .Where(v => v.ProductId == id)
                .SumAsync(v => v.StockQuantity);
        return p;
    }

    // Busca soma de estoque por variante em query agrupada — evita Include que causaria
    // referência circular ProductVariant→Product→Variants na serialização JSON.
    private async Task ApplyVariantStockAsync(List<Product> products)
    {
        var ids = products.Where(p => p.HasVariants).Select(p => p.Id).ToList();
        if (ids.Count == 0) return;

        var sums = await _db.Set<ProductVariant>()
            .Where(v => ids.Contains(v.ProductId))
            .GroupBy(v => v.ProductId)
            .Select(g => new { g.Key, Total = g.Sum(v => v.StockQuantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);

        foreach (var p in products.Where(p => p.HasVariants))
            if (sums.TryGetValue(p.Id, out var sum))
                p.StockQuantity = sum;
    }

    public async Task<Product> CreateAsync(Product product)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<Product> UpdateAsync(Product updated)
    {
        var existing = await _db.Products.FindAsync(updated.Id)
            ?? throw new KeyNotFoundException($"Produto {updated.Id} não encontrado.");

        // Atualização campo a campo — evita sobrescrever com null/0 campos não enviados pelo frontend.
        existing.Name                 = updated.Name;
        existing.Description          = updated.Description;
        existing.Category             = updated.Category;
        existing.Barcode              = updated.Barcode;
        existing.CostPriceInCents     = updated.CostPriceInCents;
        existing.PriceInCents         = updated.PriceInCents;
        existing.DiscountPriceInCents = updated.DiscountPriceInCents;
        existing.StockQuantity        = updated.StockQuantity;
        existing.MinimumStock         = updated.MinimumStock;
        existing.ImageUrl             = updated.ImageUrl;
        existing.ImageUrls            = updated.ImageUrls;
        existing.FullDescription      = updated.FullDescription;
        existing.IsActive             = updated.IsActive;
        existing.IsFeatured           = updated.IsFeatured;
        existing.ShowOnSite           = updated.ShowOnSite;
        existing.ShowOnMarketplace    = updated.ShowOnMarketplace;
        existing.IsPreVenda           = updated.IsPreVenda;
        existing.UpdatedAt            = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeactivateAsync(Guid id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product != null) { product.IsActive = false; await _db.SaveChangesAsync(); }
    }

    public async Task<IEnumerable<Product>> GetLowStockAsync() =>
        await _db.Products.Where(p => p.IsActive && p.StockQuantity <= p.MinimumStock).ToListAsync();

    public async Task<Product?> GetByBarcodeAsync(string barcode) =>
        await _db.Products.FirstOrDefaultAsync(p => p.IsActive && p.Barcode == barcode);

    public async Task<bool> AdjustStockAsync(Guid id, int quantityDelta)
    {
        if (quantityDelta == 0) return true;

        // UPDATE atômico — garante que estoque nunca fica negativo mesmo sob carga concorrente.
        // Retorna 0 rows se o produto não existe, não está ativo ou o delta resultaria em negativo.
        var rows = await _db.Products
            .Where(p => p.Id == id && p.IsActive && p.StockQuantity + quantityDelta >= 0)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantityDelta)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));

        return rows > 0;
    }
}
