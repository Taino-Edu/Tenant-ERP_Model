// =============================================================================
// ComandaItem.cs — Linha de item dentro de uma Comanda (PostgreSQL)
// Suporta dois tipos: produto físico (FK para Product) ou carta TCG (referência MongoDB)
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Representa um item dentro de uma comanda.
/// Pode ser um produto físico do estoque OU uma carta TCG (via cache MongoDB).
/// </summary>
[Table("comanda_items")]
public class ComandaItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Relacionamento com a Comanda pai
    // -------------------------------------------------------------------------

    [Required]
    [Column("comanda_id")]
    public Guid ComandaId { get; set; }

    [ForeignKey(nameof(ComandaId))]
    public Comanda Comanda { get; set; } = null!;

    // -------------------------------------------------------------------------
    // Tipo de item: Produto físico OU Carta TCG
    // -------------------------------------------------------------------------

    /// <summary>
    /// FK para o produto físico (nullable).
    /// Preenchido apenas quando o item é do estoque fixo.
    /// </summary>
    [Column("product_id")]
    public Guid? ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public Product? Product { get; set; }

    /// <summary>
    /// ID da carta no MongoDB (nullable).
    /// Preenchido apenas quando o item é uma carta TCG.
    /// Ex: "tcg_pokemon_pikachu_xy1_001"
    /// </summary>
    [MaxLength(100)]
    [Column("card_cache_id")]
    public string? CardCacheId { get; set; }

    // -------------------------------------------------------------------------
    // Snapshot do item no momento da adição
    // -------------------------------------------------------------------------

    /// <summary>
    /// Nome do produto/carta no momento da adição.
    /// Snapshot para não perder o histórico se o produto for renomeado.
    /// </summary>
    [Required, MaxLength(200)]
    [Column("item_name_snapshot")]
    public string ItemNameSnapshot { get; set; } = string.Empty;

    /// <summary>Preço unitário em centavos no momento da adição.</summary>
    [Column("unit_price_in_cents")]
    public int UnitPriceInCents { get; set; }

    // -------------------------------------------------------------------------
    // Quantidade e total
    // -------------------------------------------------------------------------

    [Column("quantity")]
    public int Quantity { get; set; } = 1;

    /// <summary>Total desta linha (UnitPrice × Quantity), em centavos.</summary>
    [Column("subtotal_in_cents")]
    public int SubtotalInCents { get; set; }

    // -------------------------------------------------------------------------
    // Auditoria
    // -------------------------------------------------------------------------

    [Column("added_at")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Quem adicionou o item: o próprio cliente ou o Admin.</summary>
    [Column("added_by_user_id")]
    public Guid AddedByUserId { get; set; }

    // -------------------------------------------------------------------------
    // Propriedade calculada
    // -------------------------------------------------------------------------

    [NotMapped]
    public decimal SubtotalInReais => SubtotalInCents / 100m;
}
