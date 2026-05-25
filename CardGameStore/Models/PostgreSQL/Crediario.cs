// =============================================================================
// Crediario.cs — Entidade de Crediário (PostgreSQL)
// Criado automaticamente quando o Admin fecha uma comanda com pagamento
// no crediário. Um cliente só pode ter UM crediário aberto por vez.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("crediarios")]
public class Crediario
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // ── Usuário ───────────────────────────────────────────────────────────────

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    // ── Comanda de origem ─────────────────────────────────────────────────────

    [Required]
    [Column("comanda_id")]
    public Guid ComandaId { get; set; }

    [ForeignKey(nameof(ComandaId))]
    public Comanda Comanda { get; set; } = null!;

    // ── Valor e datas ─────────────────────────────────────────────────────────

    /// <summary>Valor total a ser pago, em centavos (copiado do total da comanda).</summary>
    [Column("valor_em_centavos")]
    public int ValorEmCentavos { get; set; }

    /// <summary>Soma de todos os pagamentos parciais registrados, em centavos.</summary>
    [Column("valor_pago_em_centavos")]
    public int ValorPagoEmCentavos { get; set; } = 0;

    [Column("data_abertura")]
    public DateTime DataAbertura { get; set; } = DateTime.UtcNow;

    /// <summary>Vencimento automático: DataAbertura + 30 dias.</summary>
    [Column("data_vencimento")]
    public DateTime DataVencimento { get; set; }

    /// <summary>Preenchido quando o Admin marca como pago.</summary>
    [Column("data_pagamento")]
    public DateTime? DataPagamento { get; set; }

    // ── Status ────────────────────────────────────────────────────────────────

    [Required]
    [Column("status")]
    public CrediariosStatus Status { get; set; } = CrediariosStatus.Aberto;

    // ── Observações e responsáveis ────────────────────────────────────────────

    [MaxLength(500)]
    [Column("observacao")]
    public string? Observacao { get; set; }

    /// <summary>Admin que criou o crediário (fechou a comanda).</summary>
    [Column("aberto_por_admin_id")]
    public Guid AbertoPorAdminId { get; set; }

    /// <summary>Admin que registrou o pagamento.</summary>
    [Column("pago_por_admin_id")]
    public Guid? PagoPorAdminId { get; set; }

    // ── Pagamentos parciais ───────────────────────────────────────────────────

    public ICollection<PagamentoCrediario> Pagamentos { get; set; } = new List<PagamentoCrediario>();

    // ── Calculado ─────────────────────────────────────────────────────────────

    [NotMapped]
    public decimal ValorEmReais => ValorEmCentavos / 100m;

    [NotMapped]
    public decimal ValorPagoEmReais => ValorPagoEmCentavos / 100m;

    [NotMapped]
    public int SaldoRestanteEmCentavos => Math.Max(0, ValorEmCentavos - ValorPagoEmCentavos);

    [NotMapped]
    public decimal SaldoRestanteEmReais => SaldoRestanteEmCentavos / 100m;

    /// <summary>True se está Aberto e já passou do vencimento.</summary>
    [NotMapped]
    public bool Vencido => Status == CrediariosStatus.Aberto && DataVencimento < DateTime.UtcNow;
}

public enum CrediariosStatus
{
    Aberto,
    Pago
}
