// =============================================================================
// AuditLog.cs — Registro de auditoria de ações sobre dados pessoais
// Trilha de auditoria exigida pela LGPD para demonstrar conformidade.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>Gravidade do evento — permite priorizar triagem (ex: filtrar só
/// "Critical" numa investigação de fraude) sem precisar ler o Details JSON.</summary>
public enum AuditSeverity
{
    Info,
    Warning,
    Critical,
}

/// <summary>
/// Registra todas as ações relevantes sobre dados pessoais realizadas no sistema.
/// Permite auditorias, rastreabilidade e comprovação de conformidade com a LGPD.
/// </summary>
[Table("audit_logs")]
public class AuditLog
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // -------------------------------------------------------------------------
    // Ator (quem realizou a ação)
    // -------------------------------------------------------------------------

    /// <summary>ID do usuário que realizou a ação. Nulo para ações anônimas/sistema.</summary>
    [Column("actor_user_id")]
    public string? ActorUserId { get; set; }

    /// <summary>Nome do usuário que realizou a ação (snapshot no momento).</summary>
    [MaxLength(200)]
    [Column("actor_user_name")]
    public string? ActorUserName { get; set; }

    // -------------------------------------------------------------------------
    // Ação
    // -------------------------------------------------------------------------

    /// <summary>
    /// Descrição da ação realizada.
    /// Exemplos: "Visualizou", "Editou", "Exportou", "Deletou", "Respondeu", "ConsentimentoRegistrado"
    /// </summary>
    [Required, MaxLength(50)]
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Tipo da entidade afetada.
    /// Exemplos: "User", "Comanda", "LgpdRequest", "CookieConsent"
    /// </summary>
    [Required, MaxLength(50)]
    [Column("entity_type")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>ID da entidade afetada.</summary>
    [MaxLength(100)]
    [Column("entity_id")]
    public string? EntityId { get; set; }

    /// <summary>JSON com informações adicionais de contexto — inclui o que o
    /// chamador passou mais o bloco "context" (user-agent parseado, geo do
    /// Cloudflare) que o AuditService acrescenta automaticamente.</summary>
    [Column("details")]
    public string? Details { get; set; }

    /// <summary>Usuário em nome de quem a ação foi feita — preenchido só em
    /// fluxos de impersonation (ex: admin/plataforma operando como outro
    /// usuário). Nulo no caso comum de ActorUserId agir por si mesmo.</summary>
    [MaxLength(100)]
    [Column("target_user_id")]
    public string? TargetUserId { get; set; }

    /// <summary>Origem da ação: "Web", "PDV", "API", "Cron"/"System" (job em
    /// background, sem HttpContext). Ver AuditService.InferChannel.</summary>
    [MaxLength(50)]
    [Column("channel")]
    public string? Channel { get; set; }

    [Column("severity")]
    public AuditSeverity Severity { get; set; } = AuditSeverity.Info;

    // -------------------------------------------------------------------------
    // Rastreamento
    // -------------------------------------------------------------------------

    /// <summary>HttpContext.TraceIdentifier da requisição — agrupa todos os
    /// logs (manuais + diff automático) gerados pela mesma requisição HTTP.</summary>
    [MaxLength(100)]
    [Column("trace_id")]
    public string? TraceId { get; set; }

    // -------------------------------------------------------------------------
    // Identificação da origem
    // -------------------------------------------------------------------------

    /// <summary>Hash SHA-256 do IP de origem — nunca armazenar o IP puro.</summary>
    [MaxLength(64)]
    [Column("ip_hash")]
    public string IpHash { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
