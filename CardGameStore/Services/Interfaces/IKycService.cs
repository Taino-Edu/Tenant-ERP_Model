// =============================================================================
// IKycService.cs — Interface do serviço de verificação de maioridade (KYC)
//
// STATUS: ESQUELETO — NÃO IMPLEMENTADO
// Aguardando decisão de negócio sobre método de verificação.
// Ver: /KYC_PLANNING.md
// =============================================================================

namespace CardGameStore.Services.Interfaces;

/// <summary>
/// Resultado de uma verificação de maioridade.
/// </summary>
public record KycResult(
    bool   IsApproved,
    int?   Age,
    string Method,
    string? RejectionReason = null
);

/// <summary>
/// Status atual do KYC de um usuário.
/// </summary>
public record KycStatus(
    bool   IsVerified,
    bool   IsExpired,
    string Status,     // "none" | "pending" | "approved" | "rejected" | "expired"
    string Method,
    int?   Age,
    DateTime? VerifiedAt,
    DateTime? ExpiresAt
);

/// <summary>
/// Serviço de verificação de identidade/maioridade, para tenants que vendem
/// produto/serviço com restrição etária (ex: bebida alcoólica).
/// TODO: implementar após decisão de negócio.
/// </summary>
public interface IKycService
{
    /// <summary>
    /// Retorna o status atual de verificação do usuário.
    /// </summary>
    Task<KycStatus> GetStatusAsync(Guid userId);

    /// <summary>
    /// Verifica maioridade por autodeclaração (mínimo viável).
    /// Armazena data de nascimento declarada pelo usuário.
    /// NÃO valida externamente — confia na declaração.
    /// </summary>
    Task<KycResult> VerifyBySelfDeclarationAsync(Guid userId, DateOnly birthDate);

    /// <summary>
    /// Verifica maioridade consultando CPF na API da Receita Federal (via BrasilAPI).
    /// Compara CPF + nome + data de nascimento — rejeita se não bater.
    /// TODO: avaliar custo/benefício.
    /// </summary>
    Task<KycResult> VerifyByCpfReceitaAsync(Guid userId, string cpf, DateOnly birthDate);

    /// <summary>
    /// TODO (fase 3 — se necessário): verificação por documento com foto.
    /// Integraria com Idwall / Unico Check / Truora.
    /// Custo por verificação: R$ 2–8. Avaliar se vale.
    /// </summary>
    Task<KycResult> VerifyByDocumentAsync(Guid userId, string documentFrontBase64, string selfieBase64);

    /// <summary>
    /// Verifica se um usuário pode acessar produtos/serviços com restrição etária.
    /// Retorna false se: nunca verificou, verificação expirou ou foi rejeitada.
    /// </summary>
    Task<bool> CanAccessRestrictedContentAsync(Guid userId);
}
