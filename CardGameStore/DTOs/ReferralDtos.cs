using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public class SaveReferralPartnerRequest
{
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(30)] public string? Document { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [EmailAddress, MaxLength(255)] public string? Email { get; set; }
    [MaxLength(255)] public string? PixKey { get; set; }
    [RegularExpression("PF|PJ")] public string PersonType { get; set; } = "PF";
    [MaxLength(30)] public string PartnerKind { get; set; } = "Vendedor";
    [MaxLength(50)] public string? ProfessionalRegistration { get; set; }
    [Range(0, 100)] public decimal SetupCommissionPercent { get; set; }
    [Range(0, 100)] public decimal MonthlyCommissionPercent { get; set; }
    [Range(1, 31)] public int PaymentDay { get; set; } = 10;
    [Range(0, 60)] public int PaymentGraceDays { get; set; } = 5;
    public bool Active { get; set; } = true;
}

public class ReferralPartnerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Document { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PixKey { get; set; }
    public string PersonType { get; set; } = "PF";
    public string PartnerKind { get; set; } = "Vendedor";
    public string? ProfessionalRegistration { get; set; }
    public string FiscalDocumentType { get; set; } = "RPA";
    public decimal SetupCommissionPercent { get; set; }
    public decimal MonthlyCommissionPercent { get; set; }
    public int PaymentDay { get; set; }
    public int PaymentGraceDays { get; set; }
    public string? ContractVersion { get; set; }
    public DateTime? ContractAcceptedAt { get; set; }
    public bool Active { get; set; }
    public int ReferredClients { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime? NextPaymentDate { get; set; }
}

public class SaveTenantReferralRequest
{
    [Required] public Guid PartnerId { get; set; }
    [Required] public Guid TenantId { get; set; }
    public Guid? SourceLeadId { get; set; }
    [Range(0, 100)] public decimal? SetupCommissionPercent { get; set; }
    [Range(0, 100)] public decimal? MonthlyCommissionPercent { get; set; }
    [Range(1, 600)] public int? MonthlyCommissionCycles { get; set; }
    public DateTime? StartedOn { get; set; }
    public bool Active { get; set; } = true;
    [MaxLength(1000)] public string? Notes { get; set; }
}

public class TenantReferralDto
{
    public Guid Id { get; set; }
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public Guid? SourceLeadId { get; set; }
    public decimal SetupCommissionPercent { get; set; }
    public decimal MonthlyCommissionPercent { get; set; }
    public int? MonthlyCommissionCycles { get; set; }
    public DateTime StartedOn { get; set; }
    public bool Active { get; set; }
    public string? Notes { get; set; }
}

public class ReferralCommissionDto
{
    public Guid Id { get; set; }
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal BaseAmount { get; set; }
    public decimal CommissionPercent { get; set; }
    public decimal Amount { get; set; }
    public DateTime ReferenceMonth { get; set; }
    public DateTime EarnedAt { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public string FiscalDocumentType { get; set; } = string.Empty;
    public string? FiscalDocumentReference { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ReferralSummaryDto
{
    public int ActivePartners { get; set; }
    public int ReferredClients { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal OverdueAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ReferredMrr { get; set; }
}

public class SetReferralCommissionPaymentRequest
{
    public DateTime? PaidAt { get; set; }
    [MaxLength(100)] public string? FiscalDocumentReference { get; set; }
}

public class CreateReferralInvitationRequest
{
    [MaxLength(150)] public string? Name { get; set; }
    [EmailAddress, MaxLength(255)] public string? Email { get; set; }
    [MaxLength(30)] public string PartnerKind { get; set; } = "Vendedor";
    [Range(0, 100)] public decimal SetupCommissionPercent { get; set; } = 30m;
    [Range(0, 100)] public decimal MonthlyCommissionPercent { get; set; } = 5m;
    [Range(0, 60)] public int PaymentGraceDays { get; set; } = 5;
    [Range(1, 60)] public int ExpiresInDays { get; set; } = 7;
    public bool SendEmail { get; set; }
}

public class ReferralInvitationDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string PartnerKind { get; set; } = "Vendedor";
    public decimal SetupCommissionPercent { get; set; }
    public decimal MonthlyCommissionPercent { get; set; }
    public int PaymentGraceDays { get; set; }
    public string ContractVersion { get; set; } = string.Empty;
    public string? ContractText { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? InviteUrl { get; set; }
}

public class AcceptReferralInvitationRequest
{
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(255)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Document { get; set; } = string.Empty;
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(255)] public string? PixKey { get; set; }
    [RegularExpression("PF|PJ")] public string PersonType { get; set; } = "PF";
    [MaxLength(50)] public string? ProfessionalRegistration { get; set; }
    public bool AcceptedTerms { get; set; }
}
