// =============================================================================
// LeadDtos.cs — DTOs de captação de lead (CTA da landing) e gestão pelo
// dono da plataforma.
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public class CreateLeadRequest
{
    [Required, MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Telefone { get; set; } = string.Empty;

    [EmailAddress, MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(1000)]
    public string? Mensagem { get; set; }
}

public class LeadDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Mensagem { get; set; }
    public string Origem { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notas { get; set; }
    public string? DigitalPresence { get; set; }
    public int? OpportunityScore { get; set; }
    public string? PlaceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? ConvertedTenantId { get; set; }
}

public class UpdateLeadRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notas { get; set; }

    public Guid? ConvertedTenantId { get; set; }

    [RegularExpression("^(SemSite|SiteLegado|ECommerce)$",
        ErrorMessage = "DigitalPresence deve ser SemSite, SiteLegado ou ECommerce.")]
    public string? DigitalPresence { get; set; }

    [Range(0, 100)]
    public int? OpportunityScore { get; set; }

    [MaxLength(255)]
    public string? PlaceId { get; set; }
}
