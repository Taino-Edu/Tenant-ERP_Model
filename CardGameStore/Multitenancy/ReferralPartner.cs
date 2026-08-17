using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

[Table("referral_partners")]
public class ReferralPartner
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150), Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30), Column("document")]
    public string? Document { get; set; }

    [MaxLength(30), Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(255), Column("email")]
    public string? Email { get; set; }

    [MaxLength(255), Column("pix_key")]
    public string? PixKey { get; set; }

    [MaxLength(10), Column("person_type")]
    public string PersonType { get; set; } = "PF";

    [MaxLength(30), Column("partner_kind")]
    public string PartnerKind { get; set; } = "Parceiro de indicação";

    [MaxLength(50), Column("professional_registration")]
    public string? ProfessionalRegistration { get; set; }

    [MaxLength(20), Column("fiscal_document_type")]
    public string FiscalDocumentType { get; set; } = "RPA";

    [Precision(5, 2), Column("setup_commission_percent")]
    public decimal SetupCommissionPercent { get; set; }

    [Precision(5, 2), Column("monthly_commission_percent")]
    public decimal MonthlyCommissionPercent { get; set; }

    /// <summary>Dia habitual do repasse. Em mês curto, usa o último dia.</summary>
    [Range(1, 31), Column("payment_day")]
    public int PaymentDay { get; set; } = 10;

    [Range(0, 60), Column("payment_grace_days")]
    public int PaymentGraceDays { get; set; } = 5;

    [MaxLength(30), Column("contract_version")]
    public string? ContractVersion { get; set; }

    [Column("contract_text", TypeName = "text")]
    public string? ContractText { get; set; }

    [Column("contract_accepted_at")]
    public DateTime? ContractAcceptedAt { get; set; }

    [MaxLength(64), Column("contract_accepted_ip_hash")]
    public string? ContractAcceptedIpHash { get; set; }

    [MaxLength(500), Column("contract_accepted_user_agent")]
    public string? ContractAcceptedUserAgent { get; set; }

    [MaxLength(36), Column("contract_evidence_id")]
    public string? ContractEvidenceId { get; set; }

    [MaxLength(64), Column("contract_evidence_sha256")]
    public string? ContractEvidenceSha256 { get; set; }

    [MaxLength(64), Column("contract_pdf_sha256")]
    public string? ContractPdfSha256 { get; set; }

    [Column("contract_pdf", TypeName = "bytea")]
    public byte[]? ContractPdf { get; set; }

    [Column("contract_email_verified_at")]
    public DateTime? ContractEmailVerifiedAt { get; set; }

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
