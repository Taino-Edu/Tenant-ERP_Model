using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

[Table("referral_commissions")]
public class ReferralCommission
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("referral_id")]
    public Guid ReferralId { get; set; }

    [Column("tenant_charge_id")]
    public Guid TenantChargeId { get; set; }

    [Column("charge_kind")]
    public TenantChargeKind ChargeKind { get; set; }

    [Precision(10, 2), Column("base_amount")]
    public decimal BaseAmount { get; set; }

    [Precision(5, 2), Column("commission_percent")]
    public decimal CommissionPercent { get; set; }

    [Precision(10, 2), Column("amount")]
    public decimal Amount { get; set; }

    [Column("reference_month")]
    public DateTime ReferenceMonth { get; set; }

    /// <summary>Momento em que o cliente pagou e a comissão se tornou devida.</summary>
    [Column("earned_at")]
    public DateTime EarnedAt { get; set; }

    [Column("due_date")]
    public DateTime DueDate { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [MaxLength(100), Column("fiscal_document_reference")]
    public string? FiscalDocumentReference { get; set; }

    [MaxLength(500), Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
