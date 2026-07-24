// =============================================================================
// AiConfig.cs — Chave própria do Gemini (BYOK) pro tenant, opcional.
// Singleton lógico: uma única linha por schema, mesmo padrão de EmailConfig.
// Enquanto IsActive=false (ou não configurado), GeminiChatService cai na
// chave global da plataforma (GeminiSettings:ApiKey) — nada muda pro tenant
// que nunca mexeu nisso.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("ai_config")]
public class AiConfig
{
    public static readonly Guid SingletonId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Key]
    [Column("id")]
    public Guid Id { get; set; } = SingletonId;

    /// <summary>Chave própria do Gemini do tenant, criptografada via
    /// EncryptionService — nunca é devolvida em texto puro por nenhum endpoint.</summary>
    [MaxLength(500)]
    [Column("gemini_api_key_encrypted")]
    public string? GeminiApiKeyEncrypted { get; set; }

    /// <summary>Liga/desliga o uso da chave própria. Permite salvar e testar
    /// antes de ativar de vez, ou desativar sem perder o que já foi configurado
    /// (cai de volta na chave global da plataforma).</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = false;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
