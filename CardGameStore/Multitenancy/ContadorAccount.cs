// =============================================================================
// ContadorAccount.cs — Conta do contador, cross-tenant (schema-per-tenant não
// se aplica aqui). Vive no CatalogDbContext, sempre no schema "public" — um
// contador atende vários tenants (seus clientes) com UMA conta só, ligada a
// cada loja via ContadorTenantLink. Login pelo domínio raiz (mesma regra que
// já vale pro PlatformOwner — TenantClaimGuardMiddleware rejeita esse token
// em qualquer subdomínio de tenant de graça, sem código novo).
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

[Table("contador_accounts")]
public class ContadorAccount
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Token de reset de senha (mesmo padrão de User.PasswordResetToken) — expira em 2h, uso único.</summary>
    [Column("password_reset_token")]
    public string? PasswordResetToken { get; set; }

    [Column("password_reset_token_expiry")]
    public DateTime? PasswordResetTokenExpiry { get; set; }

    /// <summary>Hash SHA-256 do refresh token — mesmo padrão de User.RefreshToken.</summary>
    [Column("refresh_token")]
    public string? RefreshToken { get; set; }

    [Column("refresh_token_expiry")]
    public DateTime? RefreshTokenExpiry { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
