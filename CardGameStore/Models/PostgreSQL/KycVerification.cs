// =============================================================================
// KycVerification.cs — Verificação de identidade / maioridade (KYC)
//
// STATUS: ESQUELETO — NÃO IMPLEMENTADO
// Aguardando decisão de negócio sobre método de verificação.
// Ver: /KYC_PLANNING.md
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Registro de verificação de identidade de um usuário para acesso a produtos/serviços
/// com restrição etária (ex: bebida alcoólica), quando o tenant precisar disso.
/// Um usuário pode ter no máximo uma entrada ativa (última verificação).
/// </summary>
[Table("kyc_verifications")]
public class KycVerification
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>
    /// Método usado: "self_declaration" | "cpf_receita" | "document_photo"
    /// TODO: decidir qual implementar.
    /// </summary>
    [MaxLength(50)]
    [Column("method")]
    public string Method { get; set; } = "self_declaration";

    /// <summary>
    /// Status: "pending" | "approved" | "rejected" | "expired"
    /// </summary>
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Data de nascimento declarada ou verificada.
    /// </summary>
    [Column("birth_date")]
    public DateOnly? BirthDate { get; set; }

    /// <summary>
    /// Idade calculada na hora da verificação (cache para não recalcular).
    /// </summary>
    [Column("age_at_verification")]
    public int? AgeAtVerification { get; set; }

    /// <summary>
    /// Resultado da validação externa (JSON serializado).
    /// Ex: resposta da BrasilAPI, hash do documento, etc.
    /// Nunca armazenar CPF em texto puro aqui — usar hash SHA-256 + salt.
    /// </summary>
    [MaxLength(2000)]
    [Column("verification_result_json")]
    public string? VerificationResultJson { get; set; }

    /// <summary>
    /// Motivo da rejeição, se houver.
    /// </summary>
    [MaxLength(500)]
    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// Verificações expiram após este prazo e precisam ser renovadas.
    /// TODO: decidir prazo (sugestão: 1 ano).
    /// </summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }
}
