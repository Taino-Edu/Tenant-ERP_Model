// =============================================================================
// LoginRedirectTicket.cs — Ticket de uso único pra completar um login de
// verdade no domínio certo, depois que POST /api/auth/locate-account já
// confirmou a senha contra uma conta específica (tenant, PlatformOwner ou
// Contador). Mesmo desenho de PlatformImpersonationTicket — linha de banco
// (não IMemoryCache), sobrevive a restart, RedeemedAt = trilha de auditoria.
//
// Diferença chave pro ticket de impersonação: este é um login de VERDADE da
// própria conta (AccountId = Id real do User/ContadorAccount), não alguém
// disfarçado de outra pessoa — o redeem gera um JWT com sub = AccountId,
// sessão normal com refresh token, não uma sessão de 20min sem renovação.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

public static class LoginRedirectTargetKind
{
    public const string Tenant        = "Tenant";
    public const string PlatformOwner = "PlatformOwner";
    public const string Contador      = "Contador";
}

[Table("login_redirect_tickets")]
public class LoginRedirectTicket
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Credencial de uso único — 32 bytes aleatórios, base64url.</summary>
    [Required, MaxLength(64)]
    [Column("ticket")]
    public string Ticket { get; set; } = string.Empty;

    /// <summary>"Tenant" | "PlatformOwner" | "Contador" — ver LoginRedirectTargetKind.</summary>
    [Required, MaxLength(20)]
    [Column("target_kind")]
    public string TargetKind { get; set; } = string.Empty;

    /// <summary>Id real da conta (User.Id pra Tenant/PlatformOwner, ContadorAccount.Id
    /// pra Contador) — o redeem loga como essa conta de verdade, não impersona ninguém.</summary>
    [Column("account_id")]
    public Guid AccountId { get; set; }

    /// <summary>Só preenchido quando TargetKind == Tenant — schema onde AccountId vive.</summary>
    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    /// <summary>Cópia denormalizada do slug — usada pra montar a URL de redirect no
    /// frontend sem precisar de outra consulta, e pra log/depuração.</summary>
    [MaxLength(63)]
    [Column("tenant_slug")]
    public string? TenantSlug { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>~90s pra clicar no link — não é a duração da sessão em si (essa é a
    /// expiração normal do JWT/refresh token, geradas no redeem).</summary>
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Null até ser usado.</summary>
    [Column("redeemed_at")]
    public DateTime? RedeemedAt { get; set; }
}
