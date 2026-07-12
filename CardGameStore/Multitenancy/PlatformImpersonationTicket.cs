// =============================================================================
// PlatformImpersonationTicket.cs — Ticket de uso único pro dono da plataforma
// entrar direto no admin de um tenant, sem digitar subdomínio nem logar
// separado. Vive no catálogo (schema "public"), igual Tenant.
//
// É uma linha de banco, não um IMemoryCache, de propósito: sobrevive a um
// restart no meio do fluxo, e RedeemedAt já serve de trilha de auditoria
// sozinho (preenchido = o dono realmente usou, não só pediu) — sem precisar
// de uma tabela de audit log separada pra isso.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

[Table("platform_impersonation_tickets")]
public class PlatformImpersonationTicket
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Credencial de uso único — 32 bytes aleatórios, base64url. Não é
    /// um identificador sequencial/previsível, é o próprio "segredo" do ticket.</summary>
    [Required, MaxLength(64)]
    [Column("ticket")]
    public string Ticket { get; set; } = string.Empty;

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>Cópia denormalizada do slug — só pra log/depuração sem precisar de join.</summary>
    [MaxLength(63)]
    [Column("tenant_slug")]
    public string TenantSlug { get; set; } = string.Empty;

    [Column("platform_owner_user_id")]
    public Guid PlatformOwnerUserId { get; set; }

    [MaxLength(150)]
    [Column("platform_owner_name")]
    public string PlatformOwnerName { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Quanto tempo o dono tem pra clicar no link e abrir a aba — não é a
    /// duração da sessão de impersonação em si (essa é fixada no JWT, 20min).</summary>
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Null até ser usado. Preenchido = trilha de auditoria (o dono
    /// realmente entrou naquela loja, não só gerou o link).</summary>
    [Column("redeemed_at")]
    public DateTime? RedeemedAt { get; set; }
}
