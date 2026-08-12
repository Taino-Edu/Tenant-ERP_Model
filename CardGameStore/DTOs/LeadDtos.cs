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
    public string? EstimatedRevenueRange { get; set; }
    public string? AbordagemSugerida { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? ConvertedTenantId { get; set; }
    public CrmOpportunityDto? Opportunity { get; set; }
}

public sealed class CrmAssigneeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class CrmOpportunityDto
{
    public Guid Id { get; set; }
    public Guid LeadId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public int Probability { get; set; }
    public decimal? Value { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public string? LostReason { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CrmActivityDto
{
    public Guid Id { get; set; }
    public Guid LeadId { get; set; }
    public Guid? OpportunityId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Outcome { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class CrmWorkspaceDto
{
    public CrmOpportunityDto? Opportunity { get; set; }
    public List<CrmActivityDto> Activities { get; set; } = [];
}

public sealed class SaveCrmOpportunityRequest
{
    [Required]
    public string Stage { get; set; } = string.Empty;

    [Range(0, 100)]
    public int Probability { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal? Value { get; set; }

    public DateTime? ExpectedCloseDate { get; set; }
    public Guid? AssignedUserId { get; set; }

    [MaxLength(500)]
    public string? LostReason { get; set; }
}

public sealed class CreateCrmActivityRequest
{
    [Required]
    public string Type { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime? DueAt { get; set; }
}

public sealed class CompleteCrmActivityRequest
{
    [MaxLength(1000)]
    public string? Outcome { get; set; }
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

    // Sem MaxLength de propósito — Google não define um tamanho máximo pro Place ID.
    public string? PlaceId { get; set; }

    [MaxLength(60)]
    public string? EstimatedRevenueRange { get; set; }

    [MaxLength(2000)]
    public string? AbordagemSugerida { get; set; }
}
