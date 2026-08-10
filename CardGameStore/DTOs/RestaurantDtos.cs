using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public class RestaurantProductionAreaDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

public class SaveRestaurantProductionAreaRequest
{
    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Cor inválida. Use o formato #RRGGBB.")]
    public string Color { get; set; } = "#3EC2F2";

    [Range(0, 1000)]
    public int DisplayOrder { get; set; }
}
