using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    public CategoryService(AppDbContext db) { _db = db; }

    public async Task<IEnumerable<ProductCategory>> GetAllAsync() =>
        await _db.ProductCategories
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

    public async Task<ProductCategory> CreateAsync(ProductCategory category)
    {
        _db.ProductCategories.Add(category);
        await _db.SaveChangesAsync();
        return category;
    }

    public async Task<ProductCategory> UpdateAsync(ProductCategory category)
    {
        _db.ProductCategories.Update(category);
        await _db.SaveChangesAsync();
        return category;
    }

    public async Task DeleteAsync(Guid id)
    {
        var category = await _db.ProductCategories.FindAsync(id);
        if (category != null)
        {
            _db.ProductCategories.Remove(category);
            await _db.SaveChangesAsync();
        }
    }
}
