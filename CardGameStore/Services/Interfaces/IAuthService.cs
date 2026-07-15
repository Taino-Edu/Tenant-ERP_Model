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

    /// <summary>Busca cliente por CPF — retorna nome e se já tem senha.</summary>
    Task<CpfLookupResponse> LookupByCpfAsync(string cpf);

    /// <summary>Ativa conta de cliente existente: define email + senha.</summary>
    Task<AuthResponse> SetupAccountAsync(SetupAccountRequest request);

    /// <summary>Login de cliente pelo site (email + senha).</summary>
    Task<AuthResponse> ClientLoginAsync(ClientLoginRequest request);

    /// <summary>Cria uma conta nova de cliente pelo site, sem depender de CPF pré-cadastrado.</summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Cadastro público de Contador — cria a conta cross-tenant (catálogo) e já
    /// solicita acesso (Pending) à loja informada pelo slug, sujeito à aprovação do lojista.
    /// </summary>
    Task<AuthResponse> RegisterContadorAsync(ContadorRegisterRequest request);

    /// <summary>
    /// Gera uma sessão de impersonação pro dono da plataforma acessar o admin de
    /// um tenant sem senha separada. O `sub` do JWT é o Id do PRÓPRIO dono, nunca
    /// o do admin real do tenant — ver PlatformImpersonationTicket/AuthController
    /// pra entender por quê. Roda no contexto do tenant já resolvido pelo Host da
    /// requisição (o AppDbContext injetado aqui já está no schema certo).
    /// </summary>
    Task<AuthResponse> ImpersonateAsync(Guid platformOwnerId, string platformOwnerName, string? platformOwnerEmail);

    /// <summary>Completa um login de verdade (sessão normal, com refresh token) depois
    /// que AccountLocatorService já confirmou a senha e o redeem do LoginRedirectTicket
    /// já pousou no domínio certo — _db/_catalog aqui já estão no schema certo (tenant
    /// resolvido pelo Host desta própria requisição). Diferente de ImpersonateAsync:
    /// aqui é a própria conta logando, não alguém disfarçado de outra pessoa.</summary>
    Task<AuthResponse> CompleteLoginRedirectAsync(string targetKind, Guid accountId);
}
