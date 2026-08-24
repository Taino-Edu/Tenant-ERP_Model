using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>Premissas gerenciais persistentes do tenant usadas nas simulacoes financeiras.</summary>
[Table("financial_config")]
public class FinancialConfig
{
    public static readonly Guid SingletonId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Key]
    [Column("id")]
    public Guid Id { get; set; } = SingletonId;

    [Column("card_fee_percent", TypeName = "numeric(7,4)")]
    public decimal CardFeePercent { get; set; }

    [Column("commission_percent", TypeName = "numeric(7,4)")]
    public decimal CommissionPercent { get; set; }

    [Column("freight_percent", TypeName = "numeric(7,4)")]
    public decimal FreightPercent { get; set; }

    [Column("expected_daily_net_cash", TypeName = "numeric(14,2)")]
    public decimal ExpectedDailyNetCash { get; set; }

    [Column("minimum_cash_reserve", TypeName = "numeric(14,2)")]
    public decimal MinimumCashReserve { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
