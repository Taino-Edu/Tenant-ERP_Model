using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Variante de produto para grade de tamanhos/cores (ex: camisas).
/// Quando um produto tem HasVariants=true, o estoque é gerenciado pelas variantes.
/// </summary>
[Table("product_variants")]
public class ProductVariant
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    /// <summary>Tamanho: "PP", "P", "M", "G", "GG", "XGG", "U" (Único), etc. Null se não aplicável.</summary>
    [MaxLength(50)]
    [Column("size")]
    public string? Size { get; set; }

    /// <summary>Cor: "Preto", "Branco", "Azul", etc. Null se não aplicável.</summary>
    [MaxLength(100)]
    [Column("color")]
    public string? Color { get; set; }

    [Column("stock_quantity")]
    public int StockQuantity { get; set; } = 0;

    /// <summary>Preço específico desta variante. Null = usa o preço do produto pai.</summary>
    [Column("price_in_cents")]
    public int? PriceInCents { get; set; }

    [MaxLength(100)]
    [Column("sku")]
    public string? Sku { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string Label => string.Join(" / ", new[] { Size, Color }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
