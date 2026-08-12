using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

public enum CrmOpportunityStage
{
    Qualificacao,
    Diagnostico,
    Proposta,
    Negociacao,
    Ganho,
    Perdido,
}

public enum CrmActivityType
{
    Comentario,
    Tarefa,
    Ligacao,
    WhatsApp,
    Email,
    Reuniao,
    MudancaEtapa,
    MudancaResponsavel,
}

[Table("crm_opportunities")]
public sealed class CrmOpportunity
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("lead_id")]
    public Guid LeadId { get; set; }

    public Lead Lead { get; set; } = null!;

    [Column("stage")]
    public CrmOpportunityStage Stage { get; set; } = CrmOpportunityStage.Qualificacao;

    [Range(0, 100), Column("probability")]
    public int Probability { get; set; } = 20;

    [Column("value", TypeName = "numeric(18,2)")]
    public decimal? Value { get; set; }

    [Column("expected_close_date")]
    public DateTime? ExpectedCloseDate { get; set; }

    // Usuários da equipe ficam no AppDbContext; não há FK cruzada entre os
    // contextos. A API sempre valida que o id pertence a um PlatformOwner ativo.
    [Column("assigned_user_id")]
    public Guid? AssignedUserId { get; set; }

    [MaxLength(150), Column("assigned_user_name")]
    public string? AssignedUserName { get; set; }

    [MaxLength(500), Column("lost_reason")]
    public string? LostReason { get; set; }

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("stage_entered_at")]
    public DateTime StageEnteredAt { get; set; } = DateTime.UtcNow;

    public List<CrmActivity> Activities { get; set; } = [];
}

[Table("crm_activities")]
public sealed class CrmActivity
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("lead_id")]
    public Guid LeadId { get; set; }

    public Lead Lead { get; set; } = null!;

    [Column("opportunity_id")]
    public Guid? OpportunityId { get; set; }

    public CrmOpportunity? Opportunity { get; set; }

    [Column("type")]
    public CrmActivityType Type { get; set; }

    [Required, MaxLength(160), Column("title")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000), Column("description")]
    public string? Description { get; set; }

    [Column("due_at")]
    public DateTime? DueAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [MaxLength(1000), Column("outcome")]
    public string? Outcome { get; set; }

    [Column("created_by_user_id")]
    public Guid? CreatedByUserId { get; set; }

    [MaxLength(150), Column("created_by_user_name")]
    public string CreatedByUserName { get; set; } = "Sistema";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
