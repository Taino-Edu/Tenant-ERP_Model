// =============================================================================
// LeadDtos.cs — DTOs de captação de lead (CTA da landing) e gestão pelo
// dono da plataforma.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CardGameStore.DTOs;

/// <summary>
/// De qual formulário público o lead veio.
///
/// Existe por causa do registro de privacidade, não de relatório: o controller
/// grava finalidade e origem do tratamento no lead E numa trilha de auditoria
/// (LeadPrivacyAudit), e esses textos precisam descrever o que a pessoa de fato
/// pediu. Quem se candidata ao programa de afiliados não está contratando a
/// plataforma — registrá-lo como possível cliente é um registro impreciso
/// justamente no campo que existe para ser preciso.
///
/// É um enum e não o `Campaign` (texto livre vindo do cliente) porque o texto
/// jurídico é derivado no servidor: campanha serve para marketing, não para
/// escolher a descrição da base legal.
///
/// O conversor de string é obrigatório, não estilo: a API não registra
/// JsonStringEnumConverter global, então sem ele o System.Text.Json só aceita o
/// número do enum e devolve 400 ("The JSON value could not be converted to
/// LeadKind") para o `"kind":"Institucional"` que os dois formulários do site
/// mandam — ou seja, o CTA da landing inteiro parava de captar lead.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeadKind
{
    Institucional,
    Afiliados,
}

public class CreateLeadRequest
{
    [Required, MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Formulário de origem. O padrão mantém o comportamento de quem
    /// já chama este endpoint sem informar nada.</summary>
    public LeadKind Kind { get; set; } = LeadKind.Institucional;

    [Required, MaxLength(30)]
    public string Telefone { get; set; } = string.Empty;

    [EmailAddress, MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(1000)]
    public string? Mensagem { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "É necessário confirmar a ciência da Política de Privacidade.")]
    public bool PrivacyNoticeAcknowledged { get; set; }
    [MaxLength(20)] public string PrivacyNoticeVersion { get; set; } = "2.2";
    [MaxLength(120)] public string? Campaign { get; set; }
    [MaxLength(120)] public string? UtmSource { get; set; }
    [MaxLength(120)] public string? UtmMedium { get; set; }
    [MaxLength(120)] public string? UtmCampaign { get; set; }
    [MaxLength(120)] public string? UtmTerm { get; set; }
    [MaxLength(120)] public string? UtmContent { get; set; }
    [MaxLength(500)] public string? ReferrerUrl { get; set; }
    [MaxLength(500)] public string? LandingPage { get; set; }
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
    public string? Campaign { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? UtmTerm { get; set; }
    public string? UtmContent { get; set; }
    public string? ReferrerUrl { get; set; }
    public string? LandingPage { get; set; }
    public Guid? ReferralPartnerId { get; set; }
    public string? ReferralPartnerName { get; set; }
    public string DataOriginDetails { get; set; } = string.Empty;
    public string ProcessingPurpose { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public string? PrivacyNoticeVersion { get; set; }
    public DateTime? PrivacyNoticeAcknowledgedAt { get; set; }
    public DateTime? LegitimateInterestAssessedAt { get; set; }
    public DateTime? RetentionReviewAt { get; set; }
    public DateTime? OpposedAt { get; set; }
    public string? OppositionReason { get; set; }
    public bool CanContact { get; set; }
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

public class CrmActivityDto
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

public sealed class CrmTaskDto : CrmActivityDto
{
    public string LeadName { get; set; } = string.Empty;
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
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
    [MaxLength(120)] public string? Campaign { get; set; }
    [MaxLength(120)] public string? UtmSource { get; set; }
    [MaxLength(120)] public string? UtmMedium { get; set; }
    [MaxLength(120)] public string? UtmCampaign { get; set; }
    [MaxLength(120)] public string? UtmTerm { get; set; }
    [MaxLength(120)] public string? UtmContent { get; set; }
    public Guid? ReferralPartnerId { get; set; }
    [MaxLength(500)] public string? DataOriginDetails { get; set; }
    [MaxLength(500)] public string? ProcessingPurpose { get; set; }
    [RegularExpression("^(NaoDefinida|ProcedimentosPreContratuais|LegitimoInteresse|Consentimento|ObrigacaoLegal)$")]
    public string? LegalBasis { get; set; }
}

public sealed class RegisterLeadOppositionRequest
{
    [Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
}

public sealed class ValidateLegitimateInterestRequest
{
    [Required, MaxLength(1000)] public string PurposeAssessment { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string NecessityAssessment { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string ExpectationAssessment { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string RiskAssessment { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Safeguards { get; set; } = string.Empty;
    [Range(typeof(bool), "true", "true", ErrorMessage = "A conclusão favorável deve ser confirmada.")]
    public bool Approved { get; set; }
}

public sealed class ReviewLeadRetentionRequest
{
    [Required, RegularExpression("^(Extend|Anonymize)$")] public string Action { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [Range(30, 730)] public int? ExtensionDays { get; set; }
}

public sealed class LeadPrivacyEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}";
    public string EventHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class CrmAnalyticsDto
{
    public DateTime GeneratedAt { get; set; }
    public int TotalLeads { get; set; }
    public int ConvertedLeads { get; set; }
    public decimal ConversionRate { get; set; }
    public int OpenOpportunities { get; set; }
    public decimal OpenPipeline { get; set; }
    public decimal WeightedPipeline { get; set; }
    public double AverageSalesCycleDays { get; set; }
    public int RetentionReviewsDue { get; set; }
    public int ContactBlocked { get; set; }
    public List<CrmAnalyticsBreakdownDto> ByStage { get; set; } = [];
    public List<CrmAnalyticsBreakdownDto> BySource { get; set; } = [];
    public List<CrmAnalyticsBreakdownDto> ByOwner { get; set; } = [];
    public List<CrmAnalyticsBreakdownDto> LostReasons { get; set; } = [];
    public List<CrmMonthlyTrendDto> MonthlyTrend { get; set; } = [];
}

public sealed class CrmAnalyticsBreakdownDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Value { get; set; }
    public double AverageAgeDays { get; set; }
}

public sealed class CrmMonthlyTrendDto
{
    public string Month { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Converted { get; set; }
}
