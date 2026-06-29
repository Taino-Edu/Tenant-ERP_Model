// =============================================================================
// CardListing.cs — Marketplace de cartas entre usuários
// Representa listagens de cartas para venda e os interesses de compradores.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Listagem de uma carta para venda por um usuário.
/// Status: "Available" | "Reserved" | "Sold"
/// </summary>
[Table("card_listings")]
public class CardListing
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required, MaxLength(200)]
    [Column("card_name")]
    public string CardName { get; set; } = "";

    [MaxLength(100)]
    [Column("card_game")]
    public string? CardGame { get; set; }

    [MaxLength(500)]
    [Column("card_image_url")]
    public string? CardImageUrl { get; set; }

    [Column("price_in_cents")]
    public int PriceInCents { get; set; }

    [MaxLength(50)]
    [Column("condition")]
    public string Condition { get; set; } = "NM";

    [MaxLength(1000)]
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>Status da listagem: "Available" | "Reserved" | "Sold"</summary>
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "Available";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ListingInterest> Interests { get; set; } = new List<ListingInterest>();
}

/// <summary>
/// Registro de interesse de um usuário em uma listagem de carta.
/// Constraint UNIQUE (listing_id, user_id) — um interesse por listagem por usuário.
/// </summary>
[Table("listing_interests")]
public class ListingInterest
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("listing_id")]
    public Guid ListingId { get; set; }

    [ForeignKey(nameof(ListingId))]
    public CardListing Listing { get; set; } = null!;

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [MaxLength(500)]
    [Column("message")]
    public string? Message { get; set; }

    /// <summary>
    /// O comprador autoriza explicitamente compartilhar seu WhatsApp com o vendedor (LGPD).
    /// Sem consentimento, o número não é exposto.
    /// </summary>
    [Column("share_contact")]
    public bool ShareContact { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
