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
                UnitCostInCents  = product.CostPriceInCents,
            });
        }

        var discountInCents = (int)Math.Round(total * request.DiscountPercent / 100.0);
        var finalTotal = total - discountInCents;

        // ── Validação do segundo método de pagamento ──────────────────────────
        var secondPm  = string.IsNullOrWhiteSpace(request.SecondPaymentMethod) ? null : request.SecondPaymentMethod;
        var secondAmt = secondPm != null ? request.SecondPaymentAmountInCents : 0;

        if (secondPm != null)
        {
            if (secondAmt <= 0 || secondAmt >= finalTotal)
                throw new InvalidOperationException("Valor do segundo pagamento deve ser positivo e menor que o total.");
            if (secondPm == request.PaymentMethod)
                throw new InvalidOperationException("O segundo método de pagamento não pode ser igual ao principal.");
            if (secondPm is PaymentMethod.Cashback or PaymentMethod.Pontos && !request.UserId.HasValue)
                throw new InvalidOperationException("Cashback e Pontos como segundo pagamento exigem um cliente cadastrado selecionado.");
        }

        // Valor cobrado pelo método principal (total menos a parcela do segundo método)
        var primaryAmt = finalTotal - secondAmt;

        // ── 3. Persistir evento de caixa no MongoDB ──────────────────────────────
        // Resolve nome do cliente: prioriza nome explícito, depois busca no banco pelo userId
        string? clientNameResolved = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName.Trim();
        if (clientNameResolved == null && request.UserId.HasValue)
        {
            var usr = await _db.Users.FindAsync(request.UserId.Value);
            clientNameResolved = usr?.Name;
        }

        var venda = new VendaAvulsa
        {
            Items                      = vendaItems,
            TotalInCents               = finalTotal,
            DiscountPercent            = request.DiscountPercent,
            DiscountInCents            = discountInCents,
            PaymentMethod              = request.PaymentMethod,
            SecondPaymentMethod        = secondPm,
            SecondPaymentAmountInCents = secondAmt,
            ClientName                 = clientNameResolved,
            UserId                     = request.UserId,
            UserName                   = clientNameResolved,
            SoldAt                     = DateTime.UtcNow,
            SoldByAdminId              = adminId,
            SoldByAdminName            = adminName,
        };

        await _collection.InsertOneAsync(venda);

        var paymentSummary = secondPm != null
            ? $"{request.PaymentMethod} + {secondPm} (R$ {secondAmt / 100m:N2})"
            : request.PaymentMethod;
        _logger.LogInformation(
            "Venda avulsa {Id} registrada por {Admin}: {Count} item(ns), R$ {Total:F2} (desconto {Disc}%), {Payment}",
            venda.Id, adminName, vendaItems.Count, finalTotal / 100m, request.DiscountPercent, paymentSummary);

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
                    var itensAtuais = string.IsNullOrWhiteSpace(crediarioExistente.ItensJson)
                        ? new List<ItemCrediarioDto>()
                        : JsonSerializer.Deserialize<List<ItemCrediarioDto>>(crediarioExistente.ItensJson)
                          ?? new List<ItemCrediarioDto>();

                    itensAtuais.AddRange(novosItens);
                    crediarioExistente.ItensJson        = JsonSerializer.Serialize(itensAtuais);
                    crediarioExistente.ValorEmCentavos += primaryAmt;
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
                        ValorEmCentavos  = primaryAmt,
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
                        crediario.Id, userId, primaryAmt / 100m);
                }
            }
            else if (pm == PaymentMethod.Pontos)
            {
                if (user.PointsExpiresAt.HasValue && user.PointsExpiresAt.Value < DateTime.UtcNow)
                    throw new InvalidOperationException("Os pontos deste cliente estão expirados.");

                if (user.PointsBalance < primaryAmt)
                    throw new InvalidOperationException(
                        $"Saldo de pontos insuficiente. Cliente tem {user.PointsBalance} pts, método principal custa {primaryAmt} pts.");

                user.PointsBalance -= primaryAmt;
                user.UpdatedAt      = DateTime.UtcNow;
                _logger.LogInformation(
                    "Usuário {UserId} usou {Pts} pontos (principal) em venda avulsa. Saldo restante: {Saldo}",
                    userId, primaryAmt, user.PointsBalance);
            }
            else if (pm == PaymentMethod.Cashback)
            {
                if (user.BalanceInCents < primaryAmt)
                    throw new InvalidOperationException(
                        $"Saldo insuficiente. Cliente tem R$ {user.BalanceInCents / 100m:N2}, método principal custa R$ {primaryAmt / 100m:N2}.");

                user.BalanceInCents -= primaryAmt;
                user.UpdatedAt       = DateTime.UtcNow;
                _logger.LogInformation(
                    "Usuário {UserId} usou R$ {Valor:N2} de cashback (principal) em venda avulsa. Saldo restante: R$ {Saldo:N2}",
                    userId, primaryAmt / 100m, user.BalanceInCents / 100m);
            }

            // Aplica o segundo método de pagamento (Cashback ou Pontos como complemento)
            if (secondPm == PaymentMethod.Cashback)
            {
                if (user.BalanceInCents < secondAmt)
                    throw new InvalidOperationException(
                        $"Saldo cashback insuficiente para o segundo pagamento. Disponível: R$ {user.BalanceInCents / 100m:N2}.");
                user.BalanceInCents -= secondAmt;
                user.UpdatedAt       = DateTime.UtcNow;
                _logger.LogInformation("Usuário {UserId} usou R$ {Amt:N2} de cashback como segundo pagamento.", userId, secondAmt / 100m);
            }
            else if (secondPm == PaymentMethod.Pontos)
            {
                if (user.PointsBalance < secondAmt)
                    throw new InvalidOperationException(
                        $"Saldo de pontos insuficiente para o segundo pagamento. Disponível: {user.PointsBalance} pts.");
                user.PointsBalance -= secondAmt;
                user.UpdatedAt      = DateTime.UtcNow;
                _logger.LogInformation("Usuário {UserId} usou {Pts} pontos como segundo pagamento.", userId, secondAmt);
            }

            await _db.SaveChangesAsync();
        }
        else if (request.UserId.HasValue)
        {
            // Pagamento normal (Pix / Dinheiro / Cartão) com cliente identificado
            var userId = request.UserId.Value;
            var user   = await _db.Users.FindAsync(userId)
                ?? throw new InvalidOperationException("Cliente não encontrado.");

            // Aplica o segundo método de pagamento se houver
            if (secondPm == PaymentMethod.Cashback)
            {
                if (user.BalanceInCents < secondAmt)
                    throw new InvalidOperationException(
                        $"Saldo cashback insuficiente para o segundo pagamento. Disponível: R$ {user.BalanceInCents / 100m:N2}.");
                user.BalanceInCents -= secondAmt;
                user.UpdatedAt       = DateTime.UtcNow;
                _logger.LogInformation("Usuário {UserId} usou R$ {Amt:N2} de cashback como segundo pagamento.", userId, secondAmt / 100m);
            }
            else if (secondPm == PaymentMethod.Pontos)
            {
                if (user.PointsBalance < secondAmt)
                    throw new InvalidOperationException(
                        $"Saldo de pontos insuficiente para o segundo pagamento. Disponível: {user.PointsBalance} pts.");
                user.PointsBalance -= secondAmt;
                user.UpdatedAt      = DateTime.UtcNow;
                _logger.LogInformation("Usuário {UserId} usou {Pts} pontos como segundo pagamento.", userId, secondAmt);
            }

            // Acumula pontos de fidelidade: 1 ponto por R$1 gasto (baseado no total da compra)
            var pontosGanhos = finalTotal / 100;
            if (pontosGanhos > 0)
            {
                if (user.PointsExpiresAt.HasValue && user.PointsExpiresAt.Value < DateTime.UtcNow)
                    user.PointsBalance = 0;
                user.PointsBalance   += pontosGanhos;
                user.PointsExpiresAt  = DateTime.UtcNow.AddDays(30);
                user.UpdatedAt        = DateTime.UtcNow;
                _logger.LogInformation(
                    "Usuário {UserId} ganhou {Pontos} pontos em venda avulsa {VendaId}.",
                    userId, pontosGanhos, venda.Id);
            }

            await _db.SaveChangesAsync();
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

    public async Task<IEnumerable<VendaAvulsaDto>> GetByUserAsync(Guid userId)
    {
        var filter = Builders<VendaAvulsa>.Filter.Eq(v => v.UserId, userId);
        var vendas = await _collection
            .Find(filter)
            .SortByDescending(v => v.SoldAt)
            .ToListAsync();
        return vendas.Select(MapToDto);
    }

    public async Task<int> BackfillCostsAsync()
    {
        // Carrega todos os produtos com custo > 0 de uma vez para evitar N queries
        var produtos = await _db.Products
            .Where(p => p.CostPriceInCents > 0)
            .Select(p => new { p.Id, p.CostPriceInCents })
            .ToListAsync();

        var custoMap = produtos.ToDictionary(p => p.Id, p => p.CostPriceInCents);

        // Busca todas as vendas avulsas (sem limite — backfill é operação administrativa)
        var todasVendas = await _collection.Find(Builders<VendaAvulsa>.Filter.Empty).ToListAsync();

        var totalAtualizados = 0;

        foreach (var venda in todasVendas)
        {
            var modificou = false;
            foreach (var item in venda.Items)
            {
                if (custoMap.TryGetValue(item.ProductId, out var custo) && item.UnitCostInCents != custo)
                {
                    item.UnitCostInCents = custo;
                    totalAtualizados++;
                    modificou = true;
                }
            }

            if (modificou)
            {
                await _collection.ReplaceOneAsync(
                    Builders<VendaAvulsa>.Filter.Eq(v => v.Id, venda.Id),
                    venda);
            }
        }

        _logger.LogInformation("BackfillCosts: {N} item(s) de venda avulsa atualizados com custo.", totalAtualizados);
        return totalAtualizados;
    }

    public async Task<VendaAvulsaDto> EditarPagamentoAsync(string id, EditarPagamentoVendaAvulsaRequest request)
    {
        if (!PaymentMethod.IsValid(request.PaymentMethod))
            throw new ArgumentException($"Forma de pagamento inválida: {request.PaymentMethod}");

        if (request.SecondPaymentMethod != null && !PaymentMethod.IsValid(request.SecondPaymentMethod))
            throw new ArgumentException($"Segundo pagamento inválido: {request.SecondPaymentMethod}");

        // Busca a venda para calcular novo total se desconto mudar
        var filter  = Builders<VendaAvulsa>.Filter.Eq(v => v.Id, id);
        var current = await _collection.Find(filter).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Venda avulsa {id} não encontrada.");

        var updateDef = Builders<VendaAvulsa>.Update
            .Set(v => v.PaymentMethod,              request.PaymentMethod)
            .Set(v => v.SecondPaymentMethod,        request.SecondPaymentMethod)
            .Set(v => v.SecondPaymentAmountInCents, request.SecondPaymentAmountInCents);

        // Nome do cliente
        if (request.ClearClientName)
            updateDef = updateDef.Set(v => v.ClientName, (string?)null);
        else if (!string.IsNullOrWhiteSpace(request.ClientName))
            updateDef = updateDef.Set(v => v.ClientName, request.ClientName.Trim());

        // Desconto — recalcula TotalInCents se mudar
        if (request.DiscountInCents.HasValue)
        {
            var originalTotal = current.TotalInCents + current.DiscountInCents;
            var newDiscount   = Math.Min(request.DiscountInCents.Value, originalTotal);
            updateDef = updateDef
                .Set(v => v.DiscountInCents,  newDiscount)
                .Set(v => v.DiscountPercent,  0)
                .Set(v => v.TotalInCents,     originalTotal - newDiscount);
        }

        var opts   = new FindOneAndUpdateOptions<VendaAvulsa> { ReturnDocument = ReturnDocument.After };
        var result = await _collection.FindOneAndUpdateAsync(filter, updateDef, opts)
            ?? throw new KeyNotFoundException($"Venda avulsa {id} não encontrada.");

        _logger.LogInformation("Venda avulsa {Id} atualizada: pagamento={PM}, cliente={CN}, desconto={Desc}.",
            id, request.PaymentMethod, result.ClientName, result.DiscountInCents);
        return MapToDto(result);
    }

    private static VendaAvulsaDto MapToDto(VendaAvulsa v) => new()
    {
        Id                         = v.Id,
        ClientName                 = v.ClientName,
        PaymentMethod              = v.PaymentMethod,
        SecondPaymentMethod        = v.SecondPaymentMethod,
        SecondPaymentAmountInCents = v.SecondPaymentAmountInCents,
        TotalInReais               = v.TotalInReais,
        DiscountPercent            = v.DiscountPercent,
        DiscountInReais            = v.DiscountInReais,
        SoldAt                     = v.SoldAt,
        SoldByAdminName            = v.SoldByAdminName,
        Items                      = v.Items.Select(i => new VendaAvulsaItemDto
        {
            ProductName      = i.ProductName,
            ProductCategory  = i.ProductCategory,
            Quantity         = i.Quantity,
            UnitPriceInReais = i.UnitPriceInCents / 100m,
            SubtotalInReais  = i.SubtotalInReais,
            UnitCostInCents  = i.UnitCostInCents,
        }).ToList(),
    };
}
