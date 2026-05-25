// =============================================================================
// Product.cs — Estoque fixo da loja (PostgreSQL)
// Representa itens físicos: refrigerantes, salgadinhos, acessórios, etc.
// Cartas de TCG NÃO entram aqui — elas usam o CardCache (MongoDB) + serviço TCG.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Produto do estoque físico da loja.
/// CRUD simples gerenciado pelo Admin (Maikon).
/// </summary>
[Table("products")]
public class Product
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Identificação
    // -------------------------------------------------------------------------

    [Required, MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    // -------------------------------------------------------------------------
    // Categorização e identificação física
    // -------------------------------------------------------------------------

    /// <summary>
    /// Categoria do produto.
    /// Exemplos: "Bebida", "Salgadinho", "Acessório", "Carta Avulsa"
    /// </summary>
    [Required, MaxLength(100)]
    [Column("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>Código de barras (EAN-8, EAN-13, QR etc.) — opcional, leitura via scanner USB ou câmera.</summary>
    [MaxLength(100)]
    [Column("barcode")]
    public string? Barcode { get; set; }

    // -------------------------------------------------------------------------
    // Precificação e Estoque
    // -------------------------------------------------------------------------

    /// <summary>Preço de custo/aquisição (em centavos). Visível só para o admin — para controle de margem.</summary>
    [Column("cost_price_in_cents")]
    public int CostPriceInCents { get; set; } = 0;

    /// <summary>Preço de venda ao cliente (em centavos, para evitar float).</summary>
    [Column("price_in_cents")]
    public int PriceInCents { get; set; }

    /// <summary>Quantidade atual no estoque.</summary>
    [Column("stock_quantity")]
    public int StockQuantity { get; set; }

    /// <summary>Quantidade mínima antes de alertar o Admin sobre reposição.</summary>
    [Column("minimum_stock")]
    public int MinimumStock { get; set; } = 5;

    // -------------------------------------------------------------------------
    // Metadados
    // -------------------------------------------------------------------------

    /// <summary>URL da imagem do produto (pode ser local ou CDN).</summary>
    [MaxLength(500)]
    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Se true, o produto aparece em destaque na landing page.</summary>
    [Column("is_featured")]
    public bool IsFeatured { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // -------------------------------------------------------------------------
    // Propriedade calculada (não mapeada no banco)
    // -------------------------------------------------------------------------

    /// <summary>Preço em reais para exibição na interface.</summary>
    [NotMapped]
    public decimal PriceInReais => PriceInCents / 100m;

    /// <summary>Preço de custo em reais.</summary>
    [NotMapped]
    public decimal CostPriceInReais => CostPriceInCents / 100m;

    /// <summary>Margem de lucro em reais.</summary>
    [NotMapped]
    public decimal MarginInReais => PriceInReais - CostPriceInReais;

    /// <summary>Margem percentual.</summary>
    [NotMapped]
    public decimal MarginPercent => CostPriceInCents > 0
        ? Math.Round((MarginInReais / CostPriceInReais) * 100, 1)
        : 0;

    /// <summary>Verdadeiro se o estoque estiver abaixo do mínimo.</summary>
    [NotMapped]
    public bool IsLowStock => StockQuantity <= MinimumStock;

    // -------------------------------------------------------------------------
    // Navegação
    // -------------------------------------------------------------------------

    public ICollection<ComandaItem> ComandaItems { get; set; } = new List<ComandaItem>();
}
