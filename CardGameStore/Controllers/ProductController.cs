// =============================================================================
// ProductController.cs — CRUD de Produtos (Estoque Fixo)
// GET    /api/product            → lista todos ativos
// GET    /api/product/{id}       → busca por ID
// POST   /api/product            → cria (Admin)
// PUT    /api/product/{id}       → atualiza (Admin)
// DELETE /api/product/{id}       → desativa (Admin)
// PATCH  /api/product/{id}/stock → ajusta estoque (Admin)
// =============================================================================

using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductController : ControllerBase
{
    private readonly IProductService _service;
    private readonly IbptTaxService _ibpt;

    public ProductController(IProductService service, IbptTaxService ibpt)
    {
        _service = service;
        _ibpt = ibpt;
    }

    /// <summary>Lista todos os produtos ativos. Acessível por todos.</summary>
    /// <param name="category">Filtro opcional por categoria.</param>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<Product>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] string? category)
    {
        var products = category != null
            ? await _service.GetByCategoryAsync(category)
            : await _service.GetAllActiveAsync();
        return Ok(products);
    }

    /// <summary>Lista todos os produtos ativos para comanda do cliente (sem filtro de marketplace).</summary>
    [HttpGet("store")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<Product>), 200)]
    public async Task<IActionResult> GetAllStore()
    {
        var products = await _service.GetAllForAdminAsync();
        return Ok(products);
    }

    /// <summary>Lista TODOS os produtos ativos (incluindo ocultos do site). Só Admin/Operator.</summary>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin,Operator")]
    [ProducesResponseType(typeof(IEnumerable<Product>), 200)]
    public async Task<IActionResult> GetAllAdmin()
    {
        var products = await _service.GetAllForAdminAsync();
        return Ok(products);
    }

    /// <summary>Busca produto por ID.</summary>
    /// <param name="id">Id do produto.</param>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _service.GetByIdAsync(id);
        return product == null ? NotFound() : Ok(product);
    }

    /// <summary>Busca produto por código de barras. Acessível por todos autenticados.</summary>
    /// <param name="code">Código de barras do produto.</param>
    [HttpGet("barcode/{code}")]
    [Authorize]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByBarcode(string code)
    {
        var product = await _service.GetByBarcodeAsync(code);
        return product == null ? NotFound(new { Message = "Produto não encontrado para este código de barras." }) : Ok(product);
    }

    /// <summary>Produtos com estoque abaixo do mínimo. Apenas Admin.</summary>
    [HttpGet("low-stock")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetLowStock()
    {
        return Ok(await _service.GetLowStockAsync());
    }

    /// <summary>Cria um novo produto. Apenas Admin.</summary>
    /// <param name="product">Dados do produto a criar.</param>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Product), 201)]
    public async Task<IActionResult> Create([FromBody] Product product)
    {
        var created = await _service.CreateAsync(product);
        await _ibpt.TentarSincronizarProdutoAsync(created.Id, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Atualiza um produto. Apenas Admin.</summary>
    /// <param name="id">Id do produto.</param>
    /// <param name="product">Novos dados do produto.</param>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Product), 200)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Product product)
    {
        product.Id = id;
        var updated = await _service.UpdateAsync(product);
        await _ibpt.TentarSincronizarProdutoAsync(updated.Id, HttpContext.RequestAborted);
        return Ok(await _service.GetByIdAsync(updated.Id));
    }

    /// <summary>Desativa um produto (soft delete). Apenas Admin.</summary>
    /// <param name="id">Id do produto.</param>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _service.DeactivateAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Ajusta o estoque. Positivo = entrada, negativo = saída.
    /// Exemplo: { "delta": -1 } para vender 1 unidade.
    /// </summary>
    /// <param name="id">Id do produto.</param>
    /// <param name="req">Delta a aplicar no estoque (positivo ou negativo).</param>
    [HttpPatch("{id:guid}/stock")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdjustStock(Guid id, [FromBody] StockAdjustRequest req)
    {
        var ok = await _service.AdjustStockAsync(id, req.Delta);
        return ok ? Ok(new { Message = "Estoque ajustado." }) : BadRequest(new { Message = "Estoque insuficiente." });
    }
}

public record StockAdjustRequest(int Delta);
