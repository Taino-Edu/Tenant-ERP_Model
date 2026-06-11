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

    public async Task<IEnumerable<Product>> GetAllActiveAsync() =>
        await _db.Products.Where(p => p.IsActive && p.ShowOnSite).OrderBy(p => p.Name).ToListAsync();

    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category) =>
        await _db.Products.Where(p => p.IsActive && p.ShowOnSite && p.Category == category).ToListAsync();

    public async Task<Product?> GetByIdAsync(Guid id) =>
        await _db.Products.FindAsync(id);

    public async Task<Product> CreateAsync(Product product)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<Product> UpdateAsync(Product product)
    {
        product.UpdatedAt = DateTime.UtcNow;
        _db.Products.Update(product);
        await _db.SaveChangesAsync();
        return product;
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
        var product = await _db.Products.FindAsync(id);
        if (product == null) return false;

        // Valida ANTES de modificar para não deixar estado sujo na entidade
        var novoEstoque = product.StockQuantity + quantityDelta;
        if (novoEstoque < 0) return false;

        product.StockQuantity = novoEstoque;
        product.UpdatedAt     = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}
