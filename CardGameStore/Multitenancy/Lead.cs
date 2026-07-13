// =============================================================================
// Lead.cs — Contato de quem quer contratar a plataforma (CTA da landing).
// Vive no catálogo (schema "public"), igual Tenant — nasce antes de qualquer
// tenant existir, então não pode morar dentro de um schema de loja.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

public enum LeadStatus
{
    Novo,
    Contatado,
    Convertido,
    Perdido,
}

[Table("leads")]
public class Lead
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150)]
    [Column("nome")]
    public string Nome { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    [Column("telefone")]
    public string Telefone { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("email")]
    public string? Email { get; set; }

    [MaxLength(1000)]
    [Column("mensagem")]
    public string? Mensagem { get; set; }

    /// <summary>De onde veio o lead — hoje só "landing", mas deixa espaço pra
    /// outras origens (indicação, anúncio) sem precisar de migration nova.</summary>
    [Required, MaxLength(30)]
    [Column("origem")]
    public string Origem { get; set; } = "landing";

    [Column("status")]
    public LeadStatus Status { get; set; } = LeadStatus.Novo;

    /// <summary>Anotações internas do dono da plataforma — nunca visível pro lead.</summary>
    [MaxLength(2000)]
    [Column("notas")]
    public string? Notas { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Preenchido quando o lead vira tenant de verdade — trilha de
    /// conversão, sem FK (não é obrigatório o tenant continuar existindo).</summary>
    [Column("converted_tenant_id")]
    public Guid? ConvertedTenantId { get; set; }
}
