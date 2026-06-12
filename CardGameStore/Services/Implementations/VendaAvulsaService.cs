using System.Text.Json;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.MongoDB;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace CardGameStore.Services.Implementations;

public class VendaAvulsaService : IVendaAvulsaService
{
    // Fuso horário de Brasília — funciona em Linux (IANA) e Windows (ID legado).
    private static readonly TimeZoneInfo BrazilZone = GetBrazilZone();
    private static TimeZoneInfo GetBrazilZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }

    private static (DateTime InicioUtc, DateTime FimUtc) DiaBrasil(DateTime? dia = null)
    {
        var agora    = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilZone);
        var dataBr   = dia.HasValue ? dia.Value.Date : agora.Date;
        var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(dataBr, DateTimeKind.Unspecified), BrazilZone);
        return (inicioUtc, inicioUtc.AddDays(1));
    }

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

            // Decremento atômico via ExecuteUpdateAsync — evita race condition em vendas simultâneas
            var updated = await _db.Products
                .Where(p => p.Id == product.Id && p.StockQuantity >= reqItem.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.StockQuantity, p => p.StockQuantity - reqItem.Quantity));
            if (updated == 0)
                throw new InvalidOperationException($"Estoque insuficiente para '{product.Name}' (venda simultânea detectada).");

            var effectivePrice = product.IsOnPromo ? product.DiscountPriceInCents!.Value : product.PriceInCents;
            var subtotal = effectivePrice * reqItem.Quantity;
            total += subtotal;

            vendaItems.Add(new VendaAvulsaItem
            {
                ProductId        = product.Id,
                ProductName      = product.Name,
                ProductCategory  = product.Category,
                Quantity         = reqItem.Quantity,
                UnitPriceInCents = effectivePrice,
                SubtotalInCents  = subtotal,
            });
        }

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

        // ── Pós-venda: operações que dependem de cliente cadastrado ──────────────
        var pm = request.PaymentMethod;
        if (pm is PaymentMethod.Crediario or PaymentMethod.Pontos or PaymentMethod.Cashback)
        {
            if (!request.UserId.HasValue)
                throw new InvalidOperationException(
                    "Crediário, Pontos e Cashback exigem um cliente cadastrado selecionado.");

            var userId = request.UserId.Value;
            var user   = await _db.Users.FindAsync(userId)
                ?? throw new InvalidOperationException("Cliente não encontrado.");

            if (pm == PaymentMethod.Crediario)
            {
                var crediarioExistente = await _db.Crediarios
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == CrediariosStatus.Aberto);
                var vencimento = DateTime.UtcNow.AddDays(30);

                // Snapshot dos itens desta venda para registrar no crediário
                var novosItens = vendaItems.Select(i => new ItemCrediarioDto
                {
                    ItemName         = i.ProductName,
                    Quantity         = i.Quantity,
                    UnitPriceInReais = i.UnitPriceInCents / 100m,
                    SubtotalInReais  = i.SubtotalInCents  / 100m,
                }).ToList();

                if (crediarioExistente != null)
                {
                    // Acumula itens anteriores + novos no JSON
                    var itensAtuais = string.IsNullOrWhiteSpace(crediarioExistente.ItensJson)
                        ? new List<ItemCrediarioDto>()
                        : JsonSerializer.Deserialize<List<ItemCrediarioDto>>(crediarioExistente.ItensJson)
                          ?? new List<ItemCrediarioDto>();

                    itensAtuais.AddRange(novosItens);
                    crediarioExistente.ItensJson        = JsonSerializer.Serialize(itensAtuais);
                    crediarioExistente.ValorEmCentavos += finalTotal;
                    crediarioExistente.DataVencimento   = vencimento;
                    _logger.LogInformation(
                        "Venda avulsa acumulada no crediário {CredId} do usuário {UserId} — novo total R$ {Valor:N2}",
                        crediarioExistente.Id, userId, crediarioExistente.ValorEmCentavos / 100m);
                }
                else
                {
                    var crediario = new Crediario
                    {
                        UserId           = userId,
                        ComandaId        = null,
                        ValorEmCentavos  = finalTotal,
                        DataAbertura     = DateTime.UtcNow,
                        DataVencimento   = vencimento,
                        Status           = CrediariosStatus.Aberto,
                        AbertoPorAdminId = adminId,
                        Observacao       = "Venda avulsa no balcão",
                        ItensJson        = JsonSerializer.Serialize(novosItens),
                    };
                    _db.Crediarios.Add(crediario);
                    _logger.LogInformation(
                        "Crediário {CredId} criado para usuário {UserId} via venda avulsa — R$ {Valor:N2}",
                        crediario.Id, userId, finalTotal / 100m);
                }
            }
            else if (pm == PaymentMethod.Pontos)
            {
                if (user.PointsBalance < finalTotal)
                    throw new InvalidOperationException(
                        $"Saldo de pontos insuficiente. Cliente tem {user.PointsBalance} pts, venda custa {finalTotal} pts.");

                user.PointsBalance -= finalTotal;
                user.UpdatedAt      = DateTime.UtcNow;
                _logger.LogInformation(
                    "Usuário {UserId} usou {Pts} pontos em venda avulsa. Saldo restante: {Saldo}",
                    userId, finalTotal, user.PointsBalance);
            }
            else if (pm == PaymentMethod.Cashback)
            {
                if (user.BalanceInCents < finalTotal)
                    throw new InvalidOperationException(
                        $"Saldo insuficiente. Cliente tem R$ {user.BalanceInCents / 100m:N2}, venda custa R$ {finalTotal / 100m:N2}.");

                user.BalanceInCents -= finalTotal;
                user.UpdatedAt       = DateTime.UtcNow;
                _logger.LogInformation(
                    "Usuário {UserId} usou R$ {Valor:N2} de cashback em venda avulsa. Saldo restante: R$ {Saldo:N2}",
                    userId, finalTotal / 100m, user.BalanceInCents / 100m);
            }

            await _db.SaveChangesAsync();
        }
        else if (request.UserId.HasValue)
        {
            // Pagamento normal (Pix / Dinheiro / Cartão) com cliente identificado
            // → acumula pontos de fidelidade: 1 ponto por R$1 gasto
            var userId = request.UserId.Value;
            var user   = await _db.Users.FindAsync(userId)
                ?? throw new InvalidOperationException("Cliente não encontrado.");

            var pontosGanhos = finalTotal / 100; // 1 ponto por real
            if (pontosGanhos > 0)
            {
                user.PointsBalance   += pontosGanhos;
                user.PointsExpiresAt  = DateTime.UtcNow.AddDays(30);
                user.UpdatedAt        = DateTime.UtcNow;
                _logger.LogInformation(
                    "Usuário {UserId} ganhou {Pontos} pontos em venda avulsa {VendaId}.",
                    userId, pontosGanhos, venda.Id);
                await _db.SaveChangesAsync();
            }
        }

        return MapToDto(venda);
    }

    public async Task<IEnumerable<VendaAvulsaDto>> GetRecentAsync(int limit = 50, DateTime? desde = null)
    {
        var filter = desde.HasValue
            ? Builders<VendaAvulsa>.Filter.Gte(v => v.SoldAt, desde.Value)
            : Builders<VendaAvulsa>.Filter.Empty;

        var vendas = await _collection
            .Find(filter)
            .SortByDescending(v => v.SoldAt)
            .Limit(limit)
            .ToListAsync();

        return vendas.Select(MapToDto);
    }

    public async Task<IEnumerable<VendaAvulsaDto>> GetByDateAsync(DateTime? date = null)
    {
        // Converte data BR → intervalo UTC para evitar o bug de timezone:
        // uma venda às 22h BR (= 01h UTC do dia seguinte) aparecia como "hoje".
        var (inicio, fim) = DiaBrasil(date);

        var filter = Builders<VendaAvulsa>.Filter.And(
            Builders<VendaAvulsa>.Filter.Gte(v => v.SoldAt, inicio),
            Builders<VendaAvulsa>.Filter.Lt(v => v.SoldAt, fim));

        var vendas = await _collection
            .Find(filter)
            .SortByDescending(v => v.SoldAt)
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
