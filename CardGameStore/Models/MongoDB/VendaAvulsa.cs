using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CardGameStore.Models.MongoDB;

/// <summary>
/// Evento de caixa — venda imediata no balcão sem QR Code.
/// Documento autocontido: todos os dados são snapshot no momento da venda.
/// Nenhuma FK para PostgreSQL — propositalmente desacoplado.
/// </summary>
[BsonIgnoreExtraElements]
public class VendaAvulsa
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public List<VendaAvulsaItem> Items { get; set; } = new();

    public int TotalInCents { get; set; }

    /// <summary>Pix | Dinheiro | CartaoCredito | CartaoDebito</summary>
    public string PaymentMethod { get; set; } = PaymentMethod.Pix;

    public string? ClientName { get; set; }

    public DateTime SoldAt { get; set; } = DateTime.UtcNow;

    // Snapshot do admin no momento da venda
    public Guid   SoldByAdminId   { get; set; }
    public string SoldByAdminName { get; set; } = string.Empty;

    public int DiscountPercent { get; set; } = 0;
    public int DiscountInCents { get; set; } = 0;

    [BsonIgnore]
    public decimal TotalInReais => TotalInCents / 100m;

    [BsonIgnore]
    public decimal DiscountInReais => DiscountInCents / 100m;
}

public class VendaAvulsaItem
{
    public Guid    ProductId        { get; set; }
    public string  ProductName      { get; set; } = string.Empty;
    public string? ProductCategory  { get; set; }
    public int     Quantity         { get; set; }
    public int     UnitPriceInCents { get; set; }
    public int     SubtotalInCents  { get; set; }

    [BsonIgnore]
    public decimal SubtotalInReais => SubtotalInCents / 100m;
}

/// <summary>Constantes de forma de pagamento aceitas no sistema.</summary>
public static class PaymentMethod
{
    public const string Pix           = "Pix";
    public const string Dinheiro      = "Dinheiro";
    public const string CartaoCredito = "CartaoCredito";
    public const string CartaoDebito  = "CartaoDebito";

    public static readonly string[] All = [Pix, Dinheiro, CartaoCredito, CartaoDebito];
    public static bool IsValid(string? method) => All.Contains(method);
}
