using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

[Table("referral_partner_invitations")]
public class ReferralPartnerInvitation
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(64), Column("token_hash")] public string TokenHash { get; set; } = string.Empty;
    [MaxLength(150), Column("name")] public string? Name { get; set; }
    [MaxLength(255), Column("email")] public string? Email { get; set; }
    [MaxLength(30), Column("partner_kind")] public string PartnerKind { get; set; } = "Vendedor";
    [Precision(5, 2), Column("setup_commission_percent")] public decimal SetupCommissionPercent { get; set; } = 30m;
    [Precision(5, 2), Column("monthly_commission_percent")] public decimal MonthlyCommissionPercent { get; set; } = 5m;
    [Column("payment_grace_days")] public int PaymentGraceDays { get; set; } = 5;
    [Required, MaxLength(30), Column("contract_version")] public string ContractVersion { get; set; } = string.Empty;
    [Required, Column("contract_text", TypeName = "text")] public string ContractText { get; set; } = string.Empty;
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("sent_at")] public DateTime? SentAt { get; set; }
    [Column("accepted_at")] public DateTime? AcceptedAt { get; set; }
    [Column("revoked_at")] public DateTime? RevokedAt { get; set; }
    [Column("accepted_partner_id")] public Guid? AcceptedPartnerId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
