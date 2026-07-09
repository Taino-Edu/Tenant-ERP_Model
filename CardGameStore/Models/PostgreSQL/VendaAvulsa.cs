// =============================================================================
// VendaAvulsa.cs — Evento de caixa: venda imediata no balcão sem QR Code.
// Antes vivia no MongoDB (documento autocontido); migrado pro PostgreSQL como
// parte da consolidação multi-tenant (um único banco, isolado por schema).
// Items é serializado como JSONB — mesmo espírito do Crediario.ItensJson, mas
// mapeado direto na List<T> via conversor (ver AppDbContext.OnModelCreating).
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("vendas_avulsas")]
public class VendaAvulsa
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Snapshot dos itens vendidos. Mapeado como JSONB — ver OnModelCreating.</summary>
    public List<VendaAvulsaItem> Items { get; set; } = new();

    [Column("total_in_cents")]
    public int TotalInCents { get; set; }

    /// <summary>Pix | Dinheiro | CartaoCredito | CartaoDebito | Crediario | Pontos | Cashback</summary>
    [Column("payment_method")]
    public string PaymentMethod { get; set; } = CardGameStore.Models.PostgreSQL.PaymentMethod.Pix;

    /// <summary>Segundo método (Cashback ou Pontos) quando o pagamento é dividido. Nullable.</summary>
    [Column("second_payment_method")]
    public string? SecondPaymentMethod { get; set; }

    /// <summary>Valor pago no segundo método em centavos. Zero quando não há divisão.</summary>
    [Column("second_payment_amount_in_cents")]
    public int SecondPaymentAmountInCents { get; set; } = 0;

    [Column("client_name")]
    public string? ClientName { get; set; }

    [Column("sold_at")]
    public DateTime SoldAt { get; set; } = DateTime.UtcNow;

    // Snapshot do admin no momento da venda
    [Column("sold_by_admin_id")]
    public Guid SoldByAdminId { get; set; }

    [Column("sold_by_admin_name")]
    public string SoldByAdminName { get; set; } = string.Empty;

    /// <summary>Cliente identificado no momento da venda (nullable — vendas anônimas não têm UserId).</summary>
    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Column("user_name")]
    public string? UserName { get; set; }

    [Column("discount_percent")]
    public int DiscountPercent { get; set; } = 0;

    [Column("discount_in_cents")]
    public int DiscountInCents { get; set; } = 0;

    [NotMapped]
    public decimal TotalInReais => TotalInCents / 100m;

    [NotMapped]
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
    public int     UnitCostInCents  { get; set; }

    /// <summary>ID da variante escolhida (tamanho/cor). Null para produtos sem grade.</summary>
    public Guid?   VariantId    { get; set; }
    /// <summary>Snapshot do label da variante, ex: "M / Preto".</summary>
    public string? VariantLabel { get; set; }

    public decimal SubtotalInReais  => SubtotalInCents / 100m;
    public decimal TotalCostInReais => UnitCostInCents * Quantity / 100m;
}

/// <summary>Constantes de forma de pagamento aceitas no sistema.</summary>
public static class PaymentMethod
{
    public const string Pix           = "Pix";
    public const string Dinheiro      = "Dinheiro";
    public const string CartaoCredito = "CartaoCredito";
    public const string CartaoDebito  = "CartaoDebito";
    public const string Crediario     = "Crediario";
    public const string Pontos        = "Pontos";
    public const string Cashback      = "Cashback";

    public static readonly string[] All = [Pix, Dinheiro, CartaoCredito, CartaoDebito, Crediario, Pontos, Cashback];
    public static bool IsValid(string? method) => All.Contains(method);
}
