// =============================================================================
// IAuthService.cs — Interface do serviço de Autenticação
// =============================================================================

using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

/// <summary>Contrato para autenticação, geração e renovação de tokens JWT.</summary>
public interface IAuthService
{
    /// <summary>Login completo (Admin / jogadores de campeonato).</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request);

    /// <summary>
    /// Login rápido via QR Code (Customer).
    /// Cria o usuário se ainda não existir (baseado no CPF).
    /// </summary>
    Task<AuthResponse> QuickLoginAsync(QuickLoginRequest request);

    /// <summary>Renova o AccessToken usando o RefreshToken armazenado.</summary>
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);

    /// <summary>Invalida o RefreshToken (logout).</summary>
    Task LogoutAsync(Guid userId);

    /// <summary>
    /// Gera token de reset, persiste no banco e dispara email.
    /// Não revela se o email existe (evita user enumeration).
    /// </summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request);

    /// <summary>Valida o token e redefine a senha.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request);
}
