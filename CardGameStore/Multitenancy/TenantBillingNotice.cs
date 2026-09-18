// =============================================================================
// TenantBillingNotice.cs — Registro de cada aviso de cobrança já enviado a uma
// loja.
//
// Existe para o aviso sair UMA vez: o job roda de 12 em 12 horas, e a mesma
// fatura vencida continua vencida em todas as rodadas até ser paga. Sem este
// registro o lojista receberia o mesmo "sua loja será suspensa" duas vezes por
// dia. O índice único (loja, tipo, referência) é o que decide — inclusive
// quando o webhook e o job tentam avisar ao mesmo tempo.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

[Table("tenant_billing_notices")]
public class TenantBillingNotice
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>"dados-faltando", "aviso-suspensao", "suspensa" ou "reativada".</summary>
    [Required, MaxLength(40)]
    [Column("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>O que torna o aviso único dentro do tipo: a cobrança avisada, a
    /// data de início da cobrança, o dia da suspensão.</summary>
    [Required, MaxLength(60)]
    [Column("reference")]
    public string Reference { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("sent_to")]
    public string? SentTo { get; set; }

    [Column("sent_at")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
