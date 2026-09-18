// =============================================================================
// TenantBillingDiscount.cs — Desconto negociado com uma loja, com vigência.
//
// Uma linha por desconto, e não um campo "desconto" no Tenant, porque a
// negociação real empilha: cortesia de boas-vindas nos dois primeiros meses,
// desconto de parceiro enquanto a indicação vale, ajuste pontual combinado por
// telefone. Cada um começa e termina num mês diferente, e o gerador precisa
// saber quais valem em cada competência. Como eles se combinam está em
// CondicoesComerciais.CalcularMensalidade.
//
// Só mensalidade. Negociar a implantação é mudar o valor ou o parcelamento dela
// direto — um percentual dividido por parcelas só tornaria a conta opaca.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

public enum TenantDiscountKind
{
    /// <summary>Percentual sobre a mensalidade base.</summary>
    Percentual,

    /// <summary>Valor fixo em reais abatido da mensalidade.</summary>
    ValorFixo,
}

[Table("tenant_billing_discounts")]
public class TenantBillingDiscount
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>O motivo, do jeito que vai aparecer na cobrança ("Parceiro",
    /// "Boas-vindas"). É o que explica o valor a quem abrir o histórico.</summary>
    [Required, MaxLength(60)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("kind")]
    public TenantDiscountKind Kind { get; set; }

    /// <summary>Percentual (0 a 100) ou valor em reais, conforme <see cref="Kind"/>.</summary>
    [Precision(10, 2)]
    [Column("value")]
    public decimal Value { get; set; }

    /// <summary>Primeira competência em que vale (dia 1, 00:00 UTC).</summary>
    [Column("start_month")]
    public DateTime StartMonth { get; set; }

    /// <summary>Última competência em que vale, inclusive. Null = sem fim.</summary>
    [Column("end_month")]
    public DateTime? EndMonth { get; set; }

    [MaxLength(200)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
