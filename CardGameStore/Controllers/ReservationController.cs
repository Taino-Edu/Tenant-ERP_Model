using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Middleware;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/reservations")]
[Produces("application/json")]
[RequireOperatorPermission(Permissao.Estoque)]
public class ReservationController : ControllerBase
{
    private readonly AppDbContext        _db;
    private readonly IVendaAvulsaService _vendaService;
    private readonly IComandaService     _comandaService;

    public ReservationController(AppDbContext db, IVendaAvulsaService vendaService, IComandaService comandaService)
    {
        _db             = db;
        _vendaService   = vendaService;
        _comandaService = comandaService;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id)) throw new UnauthorizedAccessException();
        return id;
    }

    /// <summary>Lista as reservas do cliente logado, mais recentes primeiro.</summary>
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

    /// <summary>
    /// Cria uma reserva de produto (via site) — bloqueia estoque na hora, descontando
    /// reservas ativas já existentes; a venda só entra no financeiro quando o admin
    /// homologar. Expira em 48h.
    /// </summary>
    /// <param name="req">Produto/variante, quantidade e observações opcionais.</param>
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

    /// <summary>Cancela uma reserva ativa — o próprio dono ou um Admin.</summary>
    /// <param name="id">Id da reserva.</param>
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

    /// <summary>Lista todas as reservas da loja, paginado (30 por página), com filtros (Admin).</summary>
    /// <param name="status">Filtro por status ("active", "fulfilled", "cancelled").</param>
    /// <param name="userId">Filtro por cliente.</param>
    /// <param name="page">Página (30 itens cada, base 1).</param>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [RequireModule("estoque")]
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

    /// <summary>
    /// Muda o status de uma reserva ativa diretamente (Admin). Ao marcar
    /// "fulfilled" decrementa o estoque do produto/variante na hora; ao marcar
    /// "cancelled" registra CancelledAt. Prefira <see cref="Homologar"/> pra
    /// homologações que também lançam a venda no PDV/comanda.
    /// </summary>
    /// <param name="id">Id da reserva. Deve estar ativa.</param>
    /// <param name="req">Novo status.</param>
    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    [RequireModule("estoque")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateReservationStatusRequest req)
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

    /// <summary>Quantidade atualmente reservada (ativa e não-expirada) de um produto/variante — endpoint público.</summary>
    /// <param name="productId">Id do produto.</param>
    /// <param name="variantId">Id da variante, se o produto tiver grade.</param>
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

    /// <summary>
    /// Homologa uma reserva ativa: lança a venda de verdade no PDV (modo "pdv") ou
    /// como item numa comanda aberta (modo "comanda", exige ComandaId), e marca a
    /// reserva como "fulfilled". É o caminho normal pra confirmar uma reserva —
    /// diferente de <see cref="UpdateStatus"/>, que só muda o status sem lançar nada.
    /// </summary>
    /// <param name="id">Id da reserva. Deve estar ativa.</param>
    /// <param name="req">Modo de homologação ("pdv" ou "comanda") e dados específicos do modo.</param>
    [HttpPost("{id:guid}/homologar")]
    [Authorize(Policy = "AdminOnly")]
    [RequireModule("estoque")]
    public async Task<IActionResult> Homologar(Guid id, [FromBody] HomologarRequest req)
    {
        var res = await _db.ProductReservations
            .Include(r => r.User)
            .Include(r => r.Product)
            .Include(r => r.Variant)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (res is null) return NotFound();

        // Validação de entrada ANTES de reivindicar: recusar o modo aqui evita
        // ter que desfazer uma reivindicação por um erro que nem chegou a tocar
        // estoque.
        if (req.Mode is not ("pdv" or "comanda"))
            return BadRequest(new { Message = "Mode inválido. Use 'pdv' ou 'comanda'." });
        if (req.Mode == "comanda" && !req.ComandaId.HasValue)
            return BadRequest(new { Message = "ComandaId é obrigatório no modo comanda." });

        // Reivindica a reserva ANTES de vender, num UPDATE condicional que o
        // Postgres resolve sozinho: `WHERE id = @id AND status = 'active'`.
        //
        // O `if (res.Status != "active")` que existia aqui era um check-then-act
        // clássico. Duas homologações simultâneas liam "active" as duas, passavam
        // as duas, e cada uma registrava a venda — uma reserva virava duas vendas
        // e o estoque era debitado em dobro. É o que o M4 da auditoria descreve.
        //
        // Não dá pra envolver isto numa transação junto com a venda:
        // VendaAvulsaService.RegisterAsync abre a própria (CreateExecutionStrategy
        // + BeginTransactionAsync, do C7), e o EF recusa estratégia de execução
        // aninhada. Então o padrão aqui é reivindicar primeiro e compensar se a
        // venda falhar — quem perde a corrida recebe 0 linhas afetadas e para.
        var claimedAt = DateTime.UtcNow;
        var claimed = await _db.ProductReservations
            .Where(r => r.Id == id && r.Status == "active")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, "fulfilled")
                .SetProperty(r => r.FulfilledAt, claimedAt));

        if (claimed == 0)
            return BadRequest(new { Message = "Reserva não está ativa." });

        var adminId   = GetUserId();
        var adminName = User.FindFirst(ClaimTypes.Name)?.Value
                     ?? User.FindFirst("name")?.Value
                     ?? "Admin";

        try
        {
            if (req.Mode == "pdv")
            {
                var vendaReq = new VendaAvulsaRequest
                {
                    ClientName    = res.User?.Name,
                    UserId        = res.UserId,
                    PaymentMethod = req.PaymentMethod ?? PaymentMethod.Dinheiro,
                    DiscountInCents = req.CashRoundingDiscountInCents,
                    CashRoundingDiscountInCents = req.CashRoundingDiscountInCents,
                    CashReceivedInCents = req.CashReceivedInCents,
                    Items         = [new VendaAvulsaItemRequest
                    {
                        ProductId = res.ProductId,
                        VariantId = res.VariantId,
                        Quantity = res.Quantity,
                    }],
                };
                await _vendaService.RegisterAsync(vendaReq, adminId, adminName);
            }
            else
            {
                await _comandaService.AdminAddItemAsync(req.ComandaId!.Value, adminId,
                    new AddItemToComandaRequest { ProductId = res.ProductId, Quantity = res.Quantity });
            }
        }
        catch (Exception ex)
        {
            // A venda não aconteceu, então a reserva não foi cumprida — devolve
            // ela para "active" em vez de deixá-la marcada como atendida sem
            // venda nenhuma por trás. Sem esta compensação, a proteção contra a
            // corrida teria criado um problema pior que o original: estoque
            // preso numa reserva que ninguém consegue mais homologar.
            //
            // O `Where` repete o status esperado: se outra requisição já mexeu
            // no registro nesse intervalo, quem manda é ela, não este rollback.
            await _db.ProductReservations
                .Where(r => r.Id == id && r.Status == "fulfilled" && r.FulfilledAt == claimedAt)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, "active")
                    .SetProperty(r => r.FulfilledAt, (DateTime?)null));

            if (ex is InvalidOperationException)
                return BadRequest(new { Message = ex.Message });
            throw;
        }

        // Sem `res.Status = ...` aqui: o ExecuteUpdateAsync lá em cima já gravou,
        // e o `res` rastreado ainda carrega o estado antigo em memória. Reatribuir
        // e salvar seria escrever duas vezes o que já está no banco.
        return Ok(new { message = "Reserva homologada com sucesso.", reservationId = id, mode = req.Mode });
    }

    /// <summary>Estende o prazo de expiração de uma reserva ativa em mais 48h (Admin).</summary>
    /// <param name="id">Id da reserva. Deve estar ativa.</param>
    [HttpPut("{id:guid}/extend")]
    [Authorize(Policy = "AdminOnly")]
    [RequireModule("estoque")]
    public async Task<IActionResult> Extend(Guid id)
    {
        var res = await _db.ProductReservations.FindAsync(id);
        if (res is null) return NotFound();
        if (res.Status != "active") return BadRequest(new { Message = "Reserva não está ativa." });

        res.ExpiresAt = res.ExpiresAt.AddHours(48);
        await _db.SaveChangesAsync();
        return Ok(ToDto(res));
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
        unitPriceInCents = r.Variant?.PriceInCents
            ?? (r.Product?.IsOnPromo == true && r.Product.DiscountPriceInCents.HasValue
                ? r.Product.DiscountPriceInCents.Value : r.Product?.PriceInCents ?? 0),
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

public class UpdateReservationStatusRequest
{
    public string Status { get; init; } = "";
}

public class HomologarRequest
{
    /// <summary>"pdv" | "comanda"</summary>
    public string Mode { get; init; } = "pdv";

    /// <summary>Forma de pagamento para o modo PDV. Padrão: Dinheiro.</summary>
    public string? PaymentMethod { get; init; }

    /// <summary>ID da comanda aberta (obrigatório no modo comanda).</summary>
    public Guid? ComandaId { get; init; }

    public int? CashReceivedInCents { get; init; }
    public int CashRoundingDiscountInCents { get; init; }
}
