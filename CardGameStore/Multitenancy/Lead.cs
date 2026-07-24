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

    /// <summary>Presença digital do comércio. Valores: "SemSite", "SiteLegado",
    /// "ECommerce". Preenchido automaticamente pelo bot de prospecção (checa
    /// assinaturas de plataforma de e-commerce no HTML do site, se houver) ou
    /// manualmente pelo dono da plataforma.</summary>
    [MaxLength(20)]
    [Column("digital_presence")]
    public string? DigitalPresence { get; set; }

    /// <summary>Pontuação de oportunidade (0 a 100) — calculada pelo bot de
    /// prospecção a partir de nota/reviews/categoria/presença de site, ou
    /// ajustada manualmente pelo dono da plataforma.</summary>
    [Range(0, 100)]
    [Column("opportunity_score")]
    public int? OpportunityScore { get; set; }

    /// <summary>Place ID do Google Maps do estabelecimento, se o lead veio de
    /// prospecção local — permite abrir a ficha do lugar direto.</summary>
    [MaxLength(255)]
    [Column("place_id")]
    public string? PlaceId { get; set; }

    /// <summary>Faixa de faturamento estimado do negócio (ex: "R$10-30k/mês")
    /// — heurística de porte a partir de categoria+reviews, ou estimativa mais
    /// fina gerada pela IA quando o dono da plataforma pede enriquecimento.</summary>
    [MaxLength(60)]
    [Column("estimated_revenue_range")]
    public string? EstimatedRevenueRange { get; set; }

    /// <summary>Sugestão de abordagem pra esse lead específico — texto pronto
    /// por categoria (sem IA) ou gerado sob demanda pela IA. Nunca confundir
    /// com <see cref="Notas"/>, que é onde o dono da plataforma escreve à mão.</summary>
    [MaxLength(2000)]
    [Column("abordagem_sugerida")]
    public string? AbordagemSugerida { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Preenchido quando o lead vira tenant de verdade — trilha de
    /// conversão, sem FK (não é obrigatório o tenant continuar existindo).</summary>
    [Column("converted_tenant_id")]
    public Guid? ConvertedTenantId { get; set; }
}
