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

    [Precision(5, 2), Column("setup_commission_percent")]
    public decimal SetupCommissionPercent { get; set; }

    [Precision(5, 2), Column("monthly_commission_percent")]
    public decimal MonthlyCommissionPercent { get; set; }

    /// <summary>Dia habitual do repasse. Em mês curto, usa o último dia.</summary>
    [Range(1, 31), Column("payment_day")]
    public int PaymentDay { get; set; } = 10;

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
