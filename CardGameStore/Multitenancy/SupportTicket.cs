// =============================================================================
// SupportTicket.cs — Chamado de suporte entre o lojista e o dono da
// plataforma. Vive no catálogo (schema "public"), igual ContadorAviso —
// precisa ser lido pelo dono cross-tenant, então não pode morar dentro do
// schema de um tenant específico.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

public enum SupportTicketStatus
{
    Aberto,
    EmAndamento,
    Resolvido,
    Fechado,
}

public enum SupportTicketAuthorRole
{
    Tenant,
    Platform,
}

[Table("support_tickets")]
public class SupportTicket
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required, MaxLength(150)]
    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    [Column("status")]
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Aberto;

    /// <summary>Sem FK — User vive no schema do tenant, catálogo não alcança lá.
    /// Nome denormalizado no momento da criação, mesmo padrão de
    /// PlatformImpersonationTicket.</summary>
    [Column("created_by_user_id")]
    public Guid CreatedByUserId { get; set; }

    [Required, MaxLength(150)]
    [Column("created_by_user_name")]
    public string CreatedByUserName { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SupportTicketMessage> Messages { get; set; } = new List<SupportTicketMessage>();
}

[Table("support_ticket_messages")]
public class SupportTicketMessage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("ticket_id")]
    public Guid TicketId { get; set; }

    [Column("author_role")]
    public SupportTicketAuthorRole AuthorRole { get; set; }

    [Column("author_user_id")]
    public Guid AuthorUserId { get; set; }

    [Required, MaxLength(150)]
    [Column("author_name")]
    public string AuthorName { get; set; } = string.Empty;

    [Required]
    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
