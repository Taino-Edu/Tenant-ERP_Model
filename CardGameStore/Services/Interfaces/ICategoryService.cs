using CardGameStore.Models.PostgreSQL;

namespace CardGameStore.Services.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<ProductCategory>> GetAllAsync();
    Task<ProductCategory>              CreateAsync(ProductCategory category);
    Task<ProductCategory>              UpdateAsync(ProductCategory category);
    Task                               DeleteAsync(Guid id);
}
