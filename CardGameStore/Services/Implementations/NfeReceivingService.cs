using System.Data;
using System.Globalization;
using System.Xml.Linq;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

/// <summary>
/// Conferência e recebimento de mercadorias a partir do XML procNFe já obtido
/// pela Distribuição DF-e. O recebimento é atômico e idempotente por nota/item.
/// </summary>
public class NfeReceivingService
{
    private readonly AppDbContext _db;

    public NfeReceivingService(AppDbContext db) => _db = db;

    public async Task<NfeReceiptPreviewDto> PreviewAsync(Guid notaId, CancellationToken ct = default)
    {
        var nota = await _db.NotasDestinadas.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notaId, ct)
            ?? throw new KeyNotFoundException("NF-e recebida não encontrada.");

        if (string.IsNullOrWhiteSpace(nota.XmlProc))
            throw new InvalidOperationException("O XML completo desta NF-e ainda não está disponível.");

        var parsed = ParseXml(nota.XmlProc);
        var supplierCnpj = Digits(nota.EmitenteCnpj ?? parsed.SupplierCnpj);
        var products = await _db.Products.AsNoTracking().Where(p => p.IsActive)
            .OrderBy(p => p.Name).ToListAsync(ct);
        var variants = await _db.ProductVariants.AsNoTracking()
            .Where(v => products.Select(p => p.Id).Contains(v.ProductId))
            .OrderBy(v => v.Size).ThenBy(v => v.Color).ToListAsync(ct);
        var links = string.IsNullOrWhiteSpace(supplierCnpj)
            ? []
            : await _db.SupplierProductLinks.AsNoTracking()
                .Where(l => l.SupplierCnpj == supplierCnpj).ToListAsync(ct);

        foreach (var item in parsed.Items)
        {
            var link = links.FirstOrDefault(l =>
                string.Equals(l.SupplierProductCode, item.SupplierProductCode, StringComparison.OrdinalIgnoreCase));
            if (link is not null && products.Any(p => p.Id == link.ProductId))
            {
                item.SuggestedProductId = link.ProductId;
                item.SuggestedVariantId = link.ProductVariantId;
                item.MatchReason = "Vínculo usado anteriormente";
                continue;
            }

            var gtin = NormalizeGtin(item.Gtin);
            var byBarcode = !string.IsNullOrWhiteSpace(gtin)
                ? products.FirstOrDefault(p => NormalizeGtin(p.Barcode) == gtin)
                : null;
            if (byBarcode is not null)
            {
                item.SuggestedProductId = byBarcode.Id;
                item.MatchReason = "Código de barras (GTIN/EAN)";
                continue;
            }

            var code = item.SupplierProductCode.Trim();
            var bySku = variants.FirstOrDefault(v =>
                !string.IsNullOrWhiteSpace(v.Sku) &&
                string.Equals(v.Sku.Trim(), code, StringComparison.OrdinalIgnoreCase));
            if (bySku is not null)
            {
                item.SuggestedProductId = bySku.ProductId;
                item.SuggestedVariantId = bySku.Id;
                item.MatchReason = "SKU da variante";
            }
        }

        return new NfeReceiptPreviewDto
        {
            NotaId = nota.Id,
            ChaveAcesso = nota.ChaveAcesso,
            SupplierCnpj = supplierCnpj,
            SupplierName = nota.EmitenteNome ?? parsed.SupplierName,
            IssuedAt = nota.DataEmissao,
            Total = nota.Valor,
            AlreadyReceivedAt = nota.EstoqueRecebidoEm,
            Items = parsed.Items,
            Products = products.Select(p => new NfeReceiptProductOptionDto
            {
                Id = p.Id,
                Name = p.Name,
                Barcode = p.Barcode,
                StockQuantity = p.HasVariants
                    ? variants.Where(v => v.ProductId == p.Id).Sum(v => v.StockQuantity)
                    : p.StockQuantity,
                CostPriceInCents = p.CostPriceInCents,
                HasVariants = p.HasVariants,
                Variants = variants.Where(v => v.ProductId == p.Id).Select(v => new NfeReceiptVariantOptionDto
                {
                    Id = v.Id,
                    Label = v.Label,
                    Sku = v.Sku,
                    StockQuantity = v.StockQuantity,
                }).ToList(),
            }).ToList(),
        };
    }

    public async Task<NfeReceiptResultDto> ReceiveAsync(
        Guid notaId, ReceiveNfeRequest request, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var nota = await _db.NotasDestinadas.FirstOrDefaultAsync(n => n.Id == notaId, ct)
            ?? throw new KeyNotFoundException("NF-e recebida não encontrada.");
        if (nota.Status == NotaDestinadaStatus.Cancelada || nota.Situacao == 3)
            throw new InvalidOperationException("Uma NF-e cancelada não pode dar entrada no estoque.");
        if (nota.EstoqueRecebidoEm.HasValue ||
            await _db.NfeReceiptItems.AnyAsync(i => i.NotaDestinadaId == notaId, ct))
            throw new InvalidOperationException("A mercadoria desta NF-e já foi recebida.");
        if (string.IsNullOrWhiteSpace(nota.XmlProc))
            throw new InvalidOperationException("O XML completo desta NF-e ainda não está disponível.");

        var xml = ParseXml(nota.XmlProc);
        var byNumber = xml.Items.ToDictionary(i => i.ItemNumber);
        var requestedNumbers = request.Items.Select(i => i.ItemNumber).ToList();
        if (requestedNumbers.Count == 0 || requestedNumbers.Distinct().Count() != requestedNumbers.Count ||
            requestedNumbers.Any(n => !byNumber.ContainsKey(n)) || requestedNumbers.Count != byNumber.Count)
            throw new InvalidOperationException("Confira todos os itens da NF-e uma única vez antes de confirmar.");

        var productIds = request.Items.Where(i => !i.Ignore).Select(i => i.ProductId)
            .Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync(ct);
        var variants = await _db.ProductVariants
            .Where(v => productIds.Contains(v.ProductId)).ToListAsync(ct);
        var supplierCnpj = Digits(nota.EmitenteCnpj ?? xml.SupplierCnpj);
        var now = DateTime.UtcNow;
        var receivedUnits = 0;
        var receivedLines = 0;

        foreach (var input in request.Items.OrderBy(i => i.ItemNumber))
        {
            var source = byNumber[input.ItemNumber];
            if (input.Ignore)
            {
                _db.NfeReceiptItems.Add(new NfeReceiptItem
                {
                    NotaDestinadaId = nota.Id,
                    ItemNumber = source.ItemNumber,
                    SupplierProductCode = source.SupplierProductCode,
                    Description = source.Description,
                    Ignored = true,
                    IgnoreReason = string.IsNullOrWhiteSpace(input.IgnoreReason)
                        ? "Item sem controle de estoque" : input.IgnoreReason.Trim(),
                });
                continue;
            }

            if (!input.ProductId.HasValue)
                throw new InvalidOperationException($"Selecione o produto do item {source.ItemNumber}: {source.Description}.");
            if (input.Quantity <= 0 || input.UnitCostInCents < 0)
                throw new InvalidOperationException($"Quantidade ou custo inválido no item {source.ItemNumber}.");

            var product = products.FirstOrDefault(p => p.Id == input.ProductId.Value)
                ?? throw new InvalidOperationException($"Produto do item {source.ItemNumber} não encontrado ou inativo.");
            ProductVariant? variant = null;
            if (product.HasVariants)
            {
                if (!input.ProductVariantId.HasValue)
                    throw new InvalidOperationException($"Selecione a variante do item {source.ItemNumber}.");
                variant = variants.FirstOrDefault(v => v.Id == input.ProductVariantId && v.ProductId == product.Id)
                    ?? throw new InvalidOperationException($"Variante do item {source.ItemNumber} não pertence ao produto selecionado.");
            }
            else if (input.ProductVariantId.HasValue)
            {
                throw new InvalidOperationException($"O produto do item {source.ItemNumber} não usa variantes.");
            }

            var aggregateStockBefore = product.HasVariants
                ? variants.Where(v => v.ProductId == product.Id).Sum(v => v.StockQuantity)
                : product.StockQuantity;
            var stockBefore = variant?.StockQuantity ?? product.StockQuantity;
            var stockAfter = checked(stockBefore + input.Quantity);
            var weightedCost = CalculateWeightedAverageCost(
                aggregateStockBefore, product.CostPriceInCents, input.Quantity, input.UnitCostInCents);

            if (variant is not null)
            {
                variant.StockQuantity = stockAfter;
                variant.UpdatedAt = now;
            }
            else
            {
                product.StockQuantity = stockAfter;
            }
            product.CostPriceInCents = weightedCost;
            product.UpdatedAt = now;

            _db.NfeReceiptItems.Add(new NfeReceiptItem
            {
                NotaDestinadaId = nota.Id,
                ItemNumber = source.ItemNumber,
                SupplierProductCode = source.SupplierProductCode,
                Description = source.Description,
                ProductId = product.Id,
                ProductVariantId = variant?.Id,
                Quantity = input.Quantity,
                UnitCostInCents = input.UnitCostInCents,
            });
            _db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                ProductVariantId = variant?.Id,
                MovementType = "entrada_nfe",
                QuantityDelta = input.Quantity,
                StockBefore = stockBefore,
                StockAfter = stockAfter,
                UnitCostInCents = input.UnitCostInCents,
                ReferenceType = "nota_destinada",
                ReferenceId = nota.Id,
                NfeKey = nota.ChaveAcesso,
                SourceItemNumber = source.ItemNumber,
                Notes = $"Entrada pela NF-e de {nota.EmitenteNome ?? "fornecedor"}",
                OccurredAt = now,
            });

            if (!string.IsNullOrWhiteSpace(supplierCnpj) && !string.IsNullOrWhiteSpace(source.SupplierProductCode))
            {
                var link = await _db.SupplierProductLinks.FirstOrDefaultAsync(l =>
                    l.SupplierCnpj == supplierCnpj && l.SupplierProductCode == source.SupplierProductCode, ct);
                if (link is null)
                {
                    link = new SupplierProductLink
                    {
                        SupplierCnpj = supplierCnpj,
                        SupplierProductCode = source.SupplierProductCode,
                        CreatedAt = now,
                    };
                    _db.SupplierProductLinks.Add(link);
                }
                link.SupplierDescription = source.Description;
                link.Gtin = NormalizeGtin(source.Gtin);
                link.ProductId = product.Id;
                link.ProductVariantId = variant?.Id;
                link.LastUnitCostInCents = input.UnitCostInCents;
                link.UpdatedAt = now;
            }

            receivedUnits += input.Quantity;
            receivedLines++;
        }

        nota.EstoqueRecebidoEm = now;
        nota.ItensEstoqueRecebidos = receivedUnits;
        nota.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new NfeReceiptResultDto
        {
            NotaId = nota.Id,
            ReceivedAt = now,
            ReceivedLines = receivedLines,
            ReceivedUnits = receivedUnits,
            IgnoredLines = request.Items.Count(i => i.Ignore),
        };
    }

    internal static ParsedNfeDto ParseXml(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.None);
        var inf = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "infNFe")
            ?? throw new InvalidOperationException("XML inválido: grupo infNFe não encontrado.");
        var emit = inf.Elements().FirstOrDefault(e => e.Name.LocalName == "emit");
        var items = new List<NfeReceiptSourceItemDto>();
        var fallbackNumber = 0;

        foreach (var det in inf.Elements().Where(e => e.Name.LocalName == "det"))
        {
            fallbackNumber++;
            var prod = det.Elements().FirstOrDefault(e => e.Name.LocalName == "prod");
            if (prod is null) continue;
            string? V(string name) => prod.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
            var quantity = Decimal(V("qCom"));
            var lineTotal = Decimal(V("vProd"));
            var discount = Decimal(V("vDesc"));
            var freight = Decimal(V("vFrete"));
            var insurance = Decimal(V("vSeg"));
            var other = Decimal(V("vOutro"));
            var unitCost = quantity > 0 ? Math.Max(0, lineTotal - discount + freight + insurance + other) / quantity : 0;

            items.Add(new NfeReceiptSourceItemDto
            {
                ItemNumber = int.TryParse(det.Attribute("nItem")?.Value, out var n) ? n : fallbackNumber,
                SupplierProductCode = V("cProd")?.Trim() ?? fallbackNumber.ToString(CultureInfo.InvariantCulture),
                Description = V("xProd")?.Trim() ?? "Item sem descrição",
                Gtin = NormalizeGtin(V("cEANTrib")) ?? NormalizeGtin(V("cEAN")),
                Ncm = Digits(V("NCM")),
                Cfop = Digits(V("CFOP")),
                Unit = V("uCom")?.Trim(),
                XmlQuantity = quantity,
                SuggestedQuantity = quantity > 0 && quantity == decimal.Truncate(quantity) && quantity <= int.MaxValue
                    ? (int)quantity : null,
                SuggestedUnitCostInCents = checked((int)Math.Round(unitCost * 100m, MidpointRounding.AwayFromZero)),
                LineTotal = lineTotal,
            });
        }

        if (items.Count == 0)
            throw new InvalidOperationException("O XML não possui itens de mercadoria.");

        string? EmitV(string name) => emit?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
        return new ParsedNfeDto
        {
            SupplierCnpj = Digits(EmitV("CNPJ")),
            SupplierName = EmitV("xNome")?.Trim(),
            Items = items,
        };
    }

    internal static int CalculateWeightedAverageCost(
        int currentStock, int currentUnitCostInCents, int incomingQuantity, int incomingUnitCostInCents)
    {
        if (incomingQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(incomingQuantity));
        var safeCurrentStock = Math.Max(0, currentStock);
        var totalStock = checked(safeCurrentStock + incomingQuantity);
        return (int)Math.Round(
            ((decimal)safeCurrentStock * Math.Max(0, currentUnitCostInCents) +
             (decimal)incomingQuantity * Math.Max(0, incomingUnitCostInCents)) / totalStock,
            MidpointRounding.AwayFromZero);
    }

    private static decimal Decimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : 0m;

    private static string Digits(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());

    private static string? NormalizeGtin(string? value)
    {
        var digits = Digits(value);
        return string.IsNullOrWhiteSpace(digits) || digits.All(c => c == '0') ? null : digits;
    }
}

public class ReceiveNfeRequest
{
    public List<ReceiveNfeItemRequest> Items { get; init; } = [];
}

public class ReceiveNfeItemRequest
{
    public int ItemNumber { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? ProductVariantId { get; init; }
    public int Quantity { get; init; }
    public int UnitCostInCents { get; init; }
    public bool Ignore { get; init; }
    public string? IgnoreReason { get; init; }
}

public class NfeReceiptPreviewDto
{
    public Guid NotaId { get; init; }
    public string ChaveAcesso { get; init; } = string.Empty;
    public string SupplierCnpj { get; init; } = string.Empty;
    public string? SupplierName { get; init; }
    public DateTime? IssuedAt { get; init; }
    public decimal Total { get; init; }
    public DateTime? AlreadyReceivedAt { get; init; }
    public List<NfeReceiptSourceItemDto> Items { get; init; } = [];
    public List<NfeReceiptProductOptionDto> Products { get; init; } = [];
}

public class ParsedNfeDto
{
    public string SupplierCnpj { get; init; } = string.Empty;
    public string? SupplierName { get; init; }
    public List<NfeReceiptSourceItemDto> Items { get; init; } = [];
}

public class NfeReceiptSourceItemDto
{
    public int ItemNumber { get; init; }
    public string SupplierProductCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Gtin { get; init; }
    public string Ncm { get; init; } = string.Empty;
    public string Cfop { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public decimal XmlQuantity { get; init; }
    public int? SuggestedQuantity { get; init; }
    public int SuggestedUnitCostInCents { get; init; }
    public decimal LineTotal { get; init; }
    public Guid? SuggestedProductId { get; set; }
    public Guid? SuggestedVariantId { get; set; }
    public string? MatchReason { get; set; }
}

public class NfeReceiptProductOptionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public int StockQuantity { get; init; }
    public int CostPriceInCents { get; init; }
    public bool HasVariants { get; init; }
    public List<NfeReceiptVariantOptionDto> Variants { get; init; } = [];
}

public class NfeReceiptVariantOptionDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Sku { get; init; }
    public int StockQuantity { get; init; }
}

public class NfeReceiptResultDto
{
    public Guid NotaId { get; init; }
    public DateTime ReceivedAt { get; init; }
    public int ReceivedLines { get; init; }
    public int ReceivedUnits { get; init; }
    public int IgnoredLines { get; init; }
}
