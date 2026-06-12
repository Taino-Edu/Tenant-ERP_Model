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

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll() =>
        Ok(await _service.GetAllAsync());

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

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
