// =============================================================================
// ProductController.cs — CRUD de Produtos (Estoque Fixo)
// GET    /api/product            → lista todos ativos
// GET    /api/product/{id}       → busca por ID
// POST   /api/product            → cria (Admin)
// PUT    /api/product/{id}       → atualiza (Admin)
// DELETE /api/product/{id}       → desativa (Admin)
// PATCH  /api/product/{id}/stock → ajusta estoque (Admin)
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Middleware;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[RequireOperatorPermission(Permissao.Estoque)]
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
    [ProducesResponseType(typeof(IEnumerable<ProductPublicDto>), 200)]
    public IActionResult GetAll([FromQuery] string? category)
    {
        // M12: entidade completa vazava CostPriceInCents/MinimumStock (custo/margem interna)
        // pra qualquer visitante anônimo — endpoint público só devolve o DTO sem esses campos.
        // Projeção no banco + serialização assíncrona: o contrato continua
        // sendo um array JSON, sem materializar o catálogo inteiro no servidor.
        return Ok(_service.StreamAllActivePublicAsync(category));
    }

    /// <summary>Lista todos os produtos ativos para comanda do cliente (sem filtro de marketplace).</summary>
    [HttpGet("store")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ProductPublicDto>), 200)]
    public IActionResult GetAllStore()
    {
        // M12: usado pela comanda do CLIENTE (app/cliente) — mesmo motivo do GetAll acima,
        // Customer não pode ver custo/margem. Admin/Operator usam GetAllAdmin (entidade completa).
        return Ok(_service.StreamAllStorePublicAsync());
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
    [ProducesResponseType(typeof(ProductPublicDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        // M12: anônimo — mesmo motivo do GetAll, sem custo/margem interna.
        var product = await _service.GetByIdAsync(id);
        return product == null ? NotFound() : Ok(ProductPublicDto.FromEntity(product));
    }

    /// <summary>Busca produto por código de barras — usado pelo admin (Estoque/venda) pra editar
    /// custo/preço via scanner. M12: era [Authorize] genérico (Customer incluído) mas devolve a
    /// entidade completa com custo — restrito a Admin/Operator em vez de esconder o campo, já que
    /// a tela de Estoque legitimamente precisa dele aqui.</summary>
    /// <param name="code">Código de barras do produto.</param>
    [HttpGet("barcode/{code}")]
    [Authorize(Roles = "Admin,Operator")]
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
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] Product product)
    {
        Product created;
        // NCM/CEST/percentuais inválidos são erro do usuário — sem o catch a exceção subia
        // pro handler global e virava 500 "Erro interno", escondendo o que estava errado.
        try { created = await _service.CreateAsync(product); }
        catch (ArgumentException ex) { return BadRequest(new { Message = ex.Message }); }

        await _ibpt.TentarSincronizarProdutoAsync(created.Id, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Atualiza um produto. Apenas Admin.</summary>
    /// <param name="id">Id do produto.</param>
    /// <param name="product">Novos dados do produto.</param>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Product product)
    {
        product.Id = id;
        Product updated;
        try { updated = await _service.UpdateAsync(product); }
        catch (ArgumentException ex)     { return BadRequest(new { Message = ex.Message }); }
        catch (KeyNotFoundException ex)  { return NotFound(new { Message = ex.Message }); }

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
        bool ok;
        try { ok = await _service.AdjustStockAsync(id, req.Delta); }
        catch (ArgumentException ex) { return BadRequest(new { Message = ex.Message }); }

        return ok ? Ok(new { Message = "Estoque ajustado." }) : BadRequest(new { Message = "Estoque insuficiente." });
    }
}

public record StockAdjustRequest(int Delta);
