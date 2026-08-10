using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Vínculo aprendido entre o código usado por um fornecedor e um item do estoque.
/// Depois da primeira conferência, as próximas NF-e sugerem o produto automaticamente.
/// </summary>
[Table("supplier_product_links")]
public class SupplierProductLink
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(14), Column("supplier_cnpj")]
    public string SupplierCnpj { get; set; } = string.Empty;

    [Required, MaxLength(100), Column("supplier_product_code")]
    public string SupplierProductCode { get; set; } = string.Empty;

    [MaxLength(200), Column("supplier_description")]
    public string? SupplierDescription { get; set; }

    [MaxLength(30), Column("gtin")]
    public string? Gtin { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("product_variant_id")]
    public Guid? ProductVariantId { get; set; }

    [Column("last_unit_cost_in_cents")]
    public int LastUnitCostInCents { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
    public ProductVariant? ProductVariant { get; set; }
}

/// <summary>Item efetivamente confirmado no recebimento de uma NF-e.</summary>
[Table("nfe_receipt_items")]
public class NfeReceiptItem
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("nota_destinada_id")]
    public Guid NotaDestinadaId { get; set; }

    [Column("item_number")]
    public int ItemNumber { get; set; }

    [MaxLength(100), Column("supplier_product_code")]
    public string? SupplierProductCode { get; set; }

    [Required, MaxLength(200), Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("product_id")]
    public Guid? ProductId { get; set; }

    [Column("product_variant_id")]
    public Guid? ProductVariantId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_cost_in_cents")]
    public int UnitCostInCents { get; set; }

    [Column("ignored")]
    public bool Ignored { get; set; }

    [MaxLength(300), Column("ignore_reason")]
    public string? IgnoreReason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public NotaDestinada NotaDestinada { get; set; } = null!;
    public Product? Product { get; set; }
    public ProductVariant? ProductVariant { get; set; }
}

/// <summary>
/// Livro imutável de movimentos do estoque. QuantityDelta é positivo para entrada
/// e negativo para saída; saldos antes/depois permitem auditoria sem reconstrução.
/// </summary>
[Table("stock_movements")]
public class StockMovement
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("product_variant_id")]
    public Guid? ProductVariantId { get; set; }

    [Required, MaxLength(30), Column("movement_type")]
    public string MovementType { get; set; } = "entrada_nfe";

    [Column("quantity_delta")]
    public int QuantityDelta { get; set; }

    [Column("stock_before")]
    public int StockBefore { get; set; }

    [Column("stock_after")]
    public int StockAfter { get; set; }

    [Column("unit_cost_in_cents")]
    public int UnitCostInCents { get; set; }

    [MaxLength(50), Column("reference_type")]
    public string? ReferenceType { get; set; }

    [Column("reference_id")]
    public Guid? ReferenceId { get; set; }

    [MaxLength(44), Column("nfe_key")]
    public string? NfeKey { get; set; }

    [Column("source_item_number")]
    public int? SourceItemNumber { get; set; }

    [MaxLength(500), Column("notes")]
    public string? Notes { get; set; }

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
    public ProductVariant? ProductVariant { get; set; }
}
