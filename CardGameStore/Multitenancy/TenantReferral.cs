using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

[Table("tenant_referrals")]
public class TenantReferral
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("partner_id")]
    public Guid PartnerId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("source_lead_id")]
    public Guid? SourceLeadId { get; set; }

    /// <summary>Snapshot do acordo. Alterar o padrão do vendedor não reescreve contratos antigos.</summary>
    [Precision(5, 2), Column("setup_commission_percent")]
    public decimal SetupCommissionPercent { get; set; }

    [Precision(5, 2), Column("monthly_commission_percent")]
    public decimal MonthlyCommissionPercent { get; set; }

    /// <summary>Null = recorrente enquanto o vínculo estiver ativo.</summary>
    [Column("monthly_commission_cycles")]
    public int? MonthlyCommissionCycles { get; set; }

    [Column("started_on")]
    public DateTime StartedOn { get; set; } = DateTime.UtcNow;

    [Column("active")]
    public bool Active { get; set; } = true;

    [MaxLength(1000), Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
