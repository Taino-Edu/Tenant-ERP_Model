// =============================================================================
// PagamentoCrediario.cs — Registro de pagamento parcial ou total de crediário
// Cada linha representa um pagamento feito pelo cliente, registrado pelo Admin.
// O crediário é quitado automaticamente quando ValorPago >= ValorTotal.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("pagamentos_crediario")]
public class PagamentoCrediario
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // ── Crediário de origem ───────────────────────────────────────────────────

    [Required]
    [Column("crediario_id")]
    public Guid CrediarioId { get; set; }

    [ForeignKey(nameof(CrediarioId))]
    public Crediario Crediario { get; set; } = null!;

    // ── Valor e forma de pagamento ────────────────────────────────────────────

    /// <summary>Valor pago nesta parcela, em centavos.</summary>
    [Column("valor_em_centavos")]
    public int ValorEmCentavos { get; set; }

    /// <summary>Forma de pagamento usada nesta parcela.</summary>
    [MaxLength(50)]
    [Column("forma_pagamento")]
    public string FormaPagamento { get; set; } = "Dinheiro";

    [MaxLength(500)]
    [Column("observacao")]
    public string? Observacao { get; set; }

    // ── Auditoria ─────────────────────────────────────────────────────────────

    /// <summary>Admin que registrou este pagamento.</summary>
    [Column("admin_id")]
    public Guid AdminId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Calculado ─────────────────────────────────────────────────────────────

    [NotMapped]
    public decimal ValorEmReais => ValorEmCentavos / 100m;
}
