// =============================================================================
// EmailConfig.cs — SMTP próprio do tenant (ex: Gmail dele), opcional.
// Singleton lógico: uma única linha por schema, mesmo padrão de SiteConfig.
// Enquanto IsActive=false (ou não configurado), EmailService cai no SMTP
// global da plataforma — nada muda pro tenant que nunca mexeu nisso.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("email_config")]
public class EmailConfig
{
    public static readonly Guid SingletonId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Key]
    [Column("id")]
    public Guid Id { get; set; } = SingletonId;

    [MaxLength(200)]
    [Column("smtp_host")]
    public string? SmtpHost { get; set; }

    [Column("smtp_port")]
    public int? SmtpPort { get; set; }

    /// <summary>Endereço Gmail (ou outro provedor SMTP) do próprio tenant.</summary>
    [MaxLength(200)]
    [Column("smtp_username")]
    public string? SmtpUsername { get; set; }

    /// <summary>Senha de app (ou API key) criptografada via EncryptionService —
    /// nunca é devolvida em texto puro por nenhum endpoint.</summary>
    [MaxLength(500)]
    [Column("smtp_password_encrypted")]
    public string? SmtpPasswordEncrypted { get; set; }

    /// <summary>Nome de remetente exibido — cai pro SiteConfig.SiteName se nulo.</summary>
    [MaxLength(100)]
    [Column("from_name")]
    public string? FromName { get; set; }

    /// <summary>Liga/desliga o uso da credencial própria. Permite salvar e testar
    /// antes de ativar de vez, ou desativar sem perder o que já foi configurado.</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = false;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
