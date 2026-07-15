// =============================================================================
// PageViewEvent.cs — Analytics de uso: qual tela do admin foi acessada, por
// quanto tempo. Alimentado pelo beacon do frontend (UsageTracker), agregado
// sob demanda pelo dono da plataforma em /plataforma/tenants/{id}.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("page_view_events")]
public class PageViewEvent
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nulo se o usuário deslogou/expirou entre a navegação e o flush do beacon.</summary>
    [Column("user_id")]
    public Guid? UserId { get; set; }

    /// <summary>Snapshot do nome no momento — evita join só pra exibir na listagem.</summary>
    [MaxLength(200)]
    [Column("user_name")]
    public string? UserName { get; set; }

    [Required, MaxLength(200)]
    [Column("path")]
    public string Path { get; set; } = string.Empty;

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    /// <summary>Tempo que a tela ficou aberta — nulo se o beacon não conseguiu medir
    /// (ex: primeira tela da sessão, ainda sem "saída" registrada).</summary>
    [Column("duration_ms")]
    public int? DurationMs { get; set; }
}
