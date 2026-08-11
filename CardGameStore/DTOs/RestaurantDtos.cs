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

public class AssignProductProductionAreaRequest
{
    public Guid? ProductionAreaId { get; set; }
}

public class RestaurantProductMappingDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Guid? ProductionAreaId { get; set; }
}

public class UpdateProductionStatusRequest
{
    [Required, RegularExpression("^(Recebido|Preparando|Pronto|Servido)$")]
    public string Status { get; set; } = string.Empty;
}

public class RestaurantProductionItemDto
{
    public Guid ComandaId { get; set; }
    public string? TableIdentifier { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public Guid ProductionAreaId { get; set; }
    public string ProductionAreaName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public DateTime? ProductionStartedAt { get; set; }
    public DateTime? ProductionReadyAt { get; set; }
    public DateTime? ProductionServedAt { get; set; }
    public string? ComandaNotes { get; set; }
}
