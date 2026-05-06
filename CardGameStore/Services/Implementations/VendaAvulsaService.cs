using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.MongoDB;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace CardGameStore.Services.Implementations;

public class VendaAvulsaService : IVendaAvulsaService
{
    private readonly AppDbContext                    _db;
    private readonly IMongoCollection<VendaAvulsa>  _collection;
    private readonly ILogger<VendaAvulsaService>    _logger;

    private const string CollectionName = "vendas_avulsas";

    public VendaAvulsaService(AppDbContext db, IMongoDatabase mongo, ILogger<VendaAvulsaService> logger)
    {
        _db         = db;
        _collection = mongo.GetCollection<VendaAvulsa>(CollectionName);
        _logger     = logger;
    }

    public async Task<VendaAvulsaDto> RegisterAsync(VendaAvulsaRequest request, Guid adminId, string adminName)
    {
        // Valida tudo antes de qualquer escrita: falha rápida evita decremento parcial de estoque
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products   = await _db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToListAsync();

        foreach (var item in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId)
                ?? throw new InvalidOperationException($"Produto '{item.ProductId}' não encontrado ou inativo.");

            if (product.StockQuantity < item.Quantity)
                throw new InvalidOperationException(
                    $"Estoque insuficiente para '{product.Name}'. Disponível: {product.StockQuantity}, solicitado: {item.Quantity}.");
        }

        // ── 2. Decrementar estoque no PostgreSQL (única transação relacional) ────
        var vendaItems = new List<VendaAvulsaItem>();
        var total      = 0;

        foreach (var reqItem in request.Items)
        {
            var product = products.First(p => p.Id == reqItem.ProductId);
            product.StockQuantity -= reqItem.Quantity;

            var subtotal = product.PriceInCents * reqItem.Quantity;
            total += subtotal;

            vendaItems.Add(new VendaAvulsaItem
            {
                ProductId        = product.Id,
                ProductName      = product.Name,
                ProductCategory  = product.Category,
                Quantity         = reqItem.Quantity,
                UnitPriceInCents = product.PriceInCents,
                SubtotalInCents  = subtotal,
            });
        }

        await _db.SaveChangesAsync(); // atômico — ou tudo ou nada no PG

        var discountInCents = (int)Math.Round(total * request.DiscountPercent / 100.0);
        var finalTotal = total - discountInCents;

        // ── 3. Persistir evento de caixa no MongoDB ──────────────────────────────
        var venda = new VendaAvulsa
        {
            Items           = vendaItems,
            TotalInCents    = finalTotal,
            DiscountPercent = request.DiscountPercent,
            DiscountInCents = discountInCents,
            PaymentMethod   = request.PaymentMethod,
            ClientName      = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName.Trim(),
            SoldAt          = DateTime.UtcNow,
            SoldByAdminId   = adminId,
            SoldByAdminName = adminName,
        };

        await _collection.InsertOneAsync(venda);

        _logger.LogInformation(
            "Venda avulsa {Id} registrada por {Admin}: {Count} item(ns), R$ {Total:F2} (desconto {Disc}%), {Payment}",
            venda.Id, adminName, vendaItems.Count, finalTotal / 100m, request.DiscountPercent, request.PaymentMethod);

        return MapToDto(venda);
    }

    public async Task<IEnumerable<VendaAvulsaDto>> GetRecentAsync(int limit = 50)
    {
        var vendas = await _collection
            .Find(Builders<VendaAvulsa>.Filter.Empty)
            .SortByDescending(v => v.SoldAt)
            .Limit(limit)
            .ToListAsync();

        return vendas.Select(MapToDto);
    }

    private static VendaAvulsaDto MapToDto(VendaAvulsa v) => new()
    {
        Id              = v.Id,
        ClientName      = v.ClientName,
        PaymentMethod   = v.PaymentMethod,
        TotalInReais    = v.TotalInReais,
        DiscountPercent = v.DiscountPercent,
        DiscountInReais = v.DiscountInReais,
        SoldAt          = v.SoldAt,
        SoldByAdminName = v.SoldByAdminName,
        Items           = v.Items.Select(i => new VendaAvulsaItemDto
        {
            ProductName      = i.ProductName,
            ProductCategory  = i.ProductCategory,
            Quantity         = i.Quantity,
            UnitPriceInReais = i.UnitPriceInCents / 100m,
            SubtotalInReais  = i.SubtotalInReais,
        }).ToList(),
    };
}
