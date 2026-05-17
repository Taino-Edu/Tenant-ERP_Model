// =============================================================================
// CookieConsent.cs — Registro de consentimento de cookies
// Armazena evidência do consentimento conforme LGPD Art. 8°.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Registra o consentimento ou recusa de cookies pelo visitante.
/// O IP é armazenado como hash SHA-256 — nunca em texto puro — preservando a privacidade.
/// </summary>
[Table("cookie_consents")]
public class CookieConsent
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Usuário autenticado (se houver JWT no momento do consentimento).</summary>
    [Column("user_id")]
    public Guid? UserId { get; set; }

    /// <summary>Hash SHA-256 do endereço IP — nunca armazenar o IP puro.</summary>
    [Required, MaxLength(64)]
    [Column("ip_hash")]
    public string IpHash { get; set; } = string.Empty;

    /// <summary>User-Agent do navegador no momento do consentimento.</summary>
    [MaxLength(500)]
    [Column("user_agent")]
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>True = aceitou, False = recusou.</summary>
    [Column("accepted")]
    public bool Accepted { get; set; }

    /// <summary>Versão da política de privacidade aceita/recusada.</summary>
    [MaxLength(10)]
    [Column("policy_version")]
    public string PolicyVersion { get; set; } = "1.0";

    [Column("consent_at")]
    public DateTime ConsentAt { get; set; } = DateTime.UtcNow;

    // -------------------------------------------------------------------------
    // Navegação
    // -------------------------------------------------------------------------

    public User? User { get; set; }
}
