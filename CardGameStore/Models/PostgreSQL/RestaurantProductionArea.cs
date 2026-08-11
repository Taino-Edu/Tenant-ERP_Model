using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Setor que prepara itens de um restaurante (ex.: Cozinha, Bar, Confeitaria).
/// Produtos apontam para uma área; ao entrar na comanda, essa referência e o
/// nome viram snapshot no item para preservar o histórico operacional.
/// </summary>
[Table("restaurant_production_areas")]
public class RestaurantProductionArea
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(80)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    [Column("description")]
    public string? Description { get; set; }

    [Required, MaxLength(9)]
    [Column("color")]
    public string Color { get; set; } = "#3EC2F2";

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
