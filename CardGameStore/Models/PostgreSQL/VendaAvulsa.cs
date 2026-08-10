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

    /// <summary>Numerário efetivamente entregue pelo cliente. Null quando não houve dinheiro.</summary>
    [Column("cash_received_in_cents")]
    public int? CashReceivedInCents { get; set; }

    /// <summary>Troco efetivamente devolvido ao cliente.</summary>
    [Column("change_in_cents")]
    public int ChangeInCents { get; set; }

    /// <summary>Parcela do desconto concedida para viabilizar o troco físico, sempre a favor do cliente.</summary>
    [Column("cash_rounding_discount_in_cents")]
    public int CashRoundingDiscountInCents { get; set; }

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

    [Column("fiscal_effects_captured_at")]
    public DateTime? FiscalEffectsCapturedAt { get; set; }

    [Column("points_debited_at_sale")]
    public int PointsDebitedAtSale { get; set; }

    [Column("cashback_debited_at_sale")]
    public int CashbackDebitedAtSale { get; set; }

    [Column("points_awarded_at_sale")]
    public int PointsAwardedAtSale { get; set; }

    [Column("crediario_id_at_sale")]
    public Guid? CrediarioIdAtSale { get; set; }

    [Column("crediario_amount_at_sale")]
    public int CrediarioAmountAtSale { get; set; }

    [Column("cancelado_em")]
    public DateTime? CanceladoEm { get; set; }

    [NotMapped]
    public decimal TotalInReais => TotalInCents / 100m;

    [NotMapped]
    public decimal DiscountInReais => DiscountInCents / 100m;

    // ── Decisão fiscal no fechamento (CON-003) ────────────────────────────────
    // Quem fechou a venda escolhe emitir NFC-e ou não. Sem registrar a escolha,
    // a conciliação enxerga "venda sem documento" e não sabe distinguir decisão
    // deliberada de falha do sistema — e o contador recebe uma lista sem
    // contexto, que é justamente o que a seção 36.5 do plano diz ser inviável.
    // Nulo = venda anterior a este registro (não é "não escolheu", é "não sabemos").

    /// <summary>True = pediu emissão no fechamento. False = optou por NÃO emitir.</summary>
    [Column("fiscal_emissao_escolhida")]
    public bool? FiscalEmissaoEscolhida { get; set; }

    /// <summary>Operador que tomou a decisão.</summary>
    [Column("fiscal_decisao_por_user_id")]
    public Guid? FiscalDecisaoPorUserId { get; set; }

    [Column("fiscal_decisao_em")]
    public DateTime? FiscalDecisaoEm { get; set; }
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
