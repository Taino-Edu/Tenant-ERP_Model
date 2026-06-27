using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("decks")]
public class Deck
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required, MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    [Column("game")]
    public string Game { get; set; } = "Pokemon";

    [MaxLength(20)]
    [Column("format")]
    public string Format { get; set; } = "Standard";

    /// <summary>
    /// JSON: [{"id":"pokemon:sv8pt5-1","name":"Weedle","quantity":4,
    ///         "setCode":"SV8PT5","setName":"...","number":"1",
    ///         "imageSmall":"...","type":"Pokémon","hp":"50"}]
    /// </summary>
    [Required]
    [Column("cards_json")]
    public string CardsJson { get; set; } = "[]";

    [Column("is_public")]
    public bool IsPublic { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
