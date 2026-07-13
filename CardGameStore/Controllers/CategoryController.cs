// GET    /api/category       → lista todas (público)
// POST   /api/category       → cria (Admin)
// PUT    /api/category/{id}  → atualiza (Admin)
// DELETE /api/category/{id}  → remove (Admin)

using System.ComponentModel.DataAnnotations;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

public record CategoryRequest(
    [Required][MaxLength(100)] string Name,
    [MaxLength(10)]            string? Emoji,
    int  DisplayOrder,
    bool IsActive
);

[ApiController]
[Route("api/category")]
[Produces("application/json")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _service;
    public CategoryController(ICategoryService service) { _service = service; }

    /// <summary>Lista todas as categorias de produto da loja (ativas e inativas). Público.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll() =>
        Ok(await _service.GetAllAsync());

    /// <summary>Cria uma nova categoria de produto.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CategoryRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var category = new ProductCategory
        {
            Id           = Guid.NewGuid(),
            Name         = req.Name.Trim(),
            Emoji        = req.Emoji?.Trim(),
            DisplayOrder = req.DisplayOrder,
            IsActive     = req.IsActive,
            CreatedAt    = DateTime.UtcNow,
        };
        return Ok(await _service.CreateAsync(category));
    }

    /// <summary>Atualiza nome, emoji, ordem de exibição ou status ativo/inativo de uma categoria.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var category = new ProductCategory
        {
            Id           = id,
            Name         = req.Name.Trim(),
            Emoji        = req.Emoji?.Trim(),
            DisplayOrder = req.DisplayOrder,
            IsActive     = req.IsActive,
        };
        return Ok(await _service.UpdateAsync(category));
    }

    /// <summary>Remove uma categoria. Produtos vinculados não são apagados — ficam sem categoria.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
