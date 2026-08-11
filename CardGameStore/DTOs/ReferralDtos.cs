using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public class SaveReferralPartnerRequest
{
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(30)] public string? Document { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [EmailAddress, MaxLength(255)] public string? Email { get; set; }
    [MaxLength(255)] public string? PixKey { get; set; }
    [Range(0, 100)] public decimal SetupCommissionPercent { get; set; }
    [Range(0, 100)] public decimal MonthlyCommissionPercent { get; set; }
    [Range(1, 31)] public int PaymentDay { get; set; } = 10;
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
    public decimal SetupCommissionPercent { get; set; }
    public decimal MonthlyCommissionPercent { get; set; }
    public int PaymentDay { get; set; }
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
}
