// =============================================================================
// AuthService.cs — Implementação de Autenticação
// =============================================================================
using CardGameStore.Configuration;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CardGameStore.Services.Implementations;

/// <summary>
/// Implementação do serviço de autenticação.
/// Responsável por: login completo, login rápido (QR Code), refresh tokens e logout.
/// </summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext          _db;
    private readonly CatalogDbContext      _catalog;
    private readonly JwtSettings           _jwt;
    private readonly ILogger<AuthService>  _logger;
    private readonly IComandaService       _comandaService;
    private readonly IEmailService         _email;
    private readonly ITenantContext        _tenant;

    public AuthService(
        AppDbContext db,
        CatalogDbContext catalog,
        IOptions<JwtSettings> jwt,
        ILogger<AuthService> logger,
        IComandaService comandaService,
        IEmailService email,
        ITenantContext tenant)
    {
        _db             = db;
        _catalog        = catalog;
        _jwt            = jwt.Value;
        _logger         = logger;
        _comandaService = comandaService;
        _email          = email;
        _tenant         = tenant;
    }

    // =========================================================================
    // LOGIN COMPLETO — Admin, jogadores de campeonato e Contador (conta cross-tenant)
    // =========================================================================
    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        // PasswordHash pode ser null para clientes de quick-login.
        // Verificar null antes de chamar BCrypt.Verify evita NullReferenceException.
        if (user != null && user.PasswordHash != null && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return await GenerateAuthResponseAsync(user);

        // Contador não é um User de tenant nenhum — vive só no catálogo (schema
        // "public"), então cai aqui como segunda tentativa antes de recusar o login.
        var email = request.Email.Trim().ToLowerInvariant();
        var conta = await _catalog.ContadorAccounts.FirstOrDefaultAsync(c => c.Email == email);
        if (conta != null && BCrypt.Net.BCrypt.Verify(request.Password, conta.PasswordHash))
            return await GenerateContadorAuthResponseAsync(conta);

        throw new UnauthorizedAccessException("E-mail ou senha inválidos.");
    }

    // =========================================================================
    // LOGIN RÁPIDO — Customer via QR Code (CPF + WhatsApp)
    // =========================================================================
    public async Task<AuthResponse> QuickLoginAsync(QuickLoginRequest request)
    {
        var cpf = request.Cpf?.Trim();
        var hasCpf = !string.IsNullOrEmpty(cpf);

        // Busca por CPF (preferido) ou WhatsApp quando CPF não informado
        var user = hasCpf
            ? await _db.Users.FirstOrDefaultAsync(u => u.Cpf == cpf)
            : await _db.Users.FirstOrDefaultAsync(u => u.WhatsApp == request.WhatsApp && u.IsActive);

        if (user == null)
        {
            user = new User
            {
                Name     = request.Name,
                Cpf      = hasCpf ? cpf : null,
                WhatsApp = request.WhatsApp,
                Role     = UserRole.Customer,
                IsActive = true
            };
            _db.Users.Add(user);
            _logger.LogInformation("Novo cliente criado via QR Code: {Name}", request.Name);
        }
        else
        {
            user.Name      = request.Name;
            user.WhatsApp  = request.WhatsApp;
            // Preenche CPF caso tenha sido informado agora e estava vazio
            if (hasCpf && user.Cpf == null) user.Cpf = cpf;
            user.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            user = hasCpf
                ? await _db.Users.FirstOrDefaultAsync(u => u.Cpf == cpf && u.IsActive)
                : await _db.Users.FirstOrDefaultAsync(u => u.WhatsApp == request.WhatsApp && u.IsActive);
            if (user == null) throw;
        }

        var comanda = await _comandaService.OpenComandaAsync(user.Id, request.TableIdentifier);
        _logger.LogInformation("Comanda {ComandaId} associada ao quick-login de {Name}", comanda.Id, user.Name);

        return await GenerateAuthResponseAsync(user, comanda.Id);
    }

    // =========================================================================
    // REFRESH TOKEN
    // =========================================================================
    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var hashedToken = HashRefreshToken(request.RefreshToken);
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.RefreshToken == hashedToken && u.IsActive
        );

        if (user != null)
        {
            if (user.RefreshTokenExpiry < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Refresh token inválido ou expirado.");
            return await GenerateAuthResponseAsync(user);
        }

        var conta = await _catalog.ContadorAccounts.FirstOrDefaultAsync(c => c.RefreshToken == hashedToken);
        if (conta == null || conta.RefreshTokenExpiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token inválido ou expirado.");

        return await GenerateContadorAuthResponseAsync(conta);
    }

    // =========================================================================
    // LOGOUT
    // =========================================================================
    public async Task LogoutAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.RefreshToken       = null;
            user.RefreshTokenExpiry = null;
            user.UpdatedAt          = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return;
        }

        var conta = await _catalog.ContadorAccounts.FindAsync(userId);
        if (conta != null)
        {
            conta.RefreshToken       = null;
            conta.RefreshTokenExpiry = null;
            conta.UpdatedAt          = DateTime.UtcNow;
            await _catalog.SaveChangesAsync();
        }
    }

    // =========================================================================
    // HELPERS PRIVADOS
    // =========================================================================

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user, Guid? comandaId = null)
    {
        // Carrega perfil do Operator para incluir permissões no JWT
        string[]? permissions = null;
        if (user.Role == UserRole.Operator && user.PerfilId.HasValue)
        {
            var perfil = await _db.Perfis.FindAsync(user.PerfilId.Value);
            if (perfil != null)
            {
                try { permissions = System.Text.Json.JsonSerializer.Deserialize<string[]>(perfil.PermissoesJson); }
                catch { permissions = []; }
            }
        }

        var accessToken  = GenerateJwt(user, permissions);
        var refreshToken = GenerateRefreshToken();
        var expiresAt    = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes);

        // Armazena somente o hash SHA-256 — o token bruto só sai no cookie HttpOnly.
        // Se o banco for comprometido, os hashes não são diretamente utilizáveis.
        user.RefreshToken       = HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays);
        user.UpdatedAt          = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new AuthResponse(accessToken, refreshToken, expiresAt, user.Role, user.Name, user.Id, comandaId, permissions);
    }

    /// <summary>
    /// Gera o AuthResponse do Contador — mesmo formato do User, mas a conta e o
    /// refresh token vivem no CatalogDbContext (schema "public"), não no AppDbContext
    /// do tenant. Role é sempre "Contador" — não existe PerfilId/permissions aqui.
    /// </summary>
    private async Task<AuthResponse> GenerateContadorAuthResponseAsync(ContadorAccount conta)
    {
        var accessToken  = GenerateJwt(conta.Id, conta.Name, conta.Email, UserRole.Contador);
        var refreshToken = GenerateRefreshToken();
        var expiresAt    = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes);

        conta.RefreshToken       = HashRefreshToken(refreshToken);
        conta.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays);
        conta.UpdatedAt          = DateTime.UtcNow;
        await _catalog.SaveChangesAsync();

        return new AuthResponse(accessToken, refreshToken, expiresAt, UserRole.Contador, conta.Name, conta.Id);
    }

    private string GenerateJwt(User user, string[]? permissions = null) =>
        GenerateJwt(user.Id, user.Name, user.Email, user.Role, permissions);

    private string GenerateJwt(Guid id, string name, string? email, string role, string[]? permissions = null)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   id.ToString()),
            new(JwtRegisteredClaimNames.Name,  name),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(ClaimTypes.Role,               role),
            // Guid do tenant (não o schema) resolvido pra esta requisição — o
            // TenantClaimGuardMiddleware compara contra o tenant resolvido por
            // Host na requisição seguinte (defesa em profundidade; o schema que
            // roteia a conexão é sempre o resolvido por Host, não esta claim).
            new(TenantConstants.TenantIdClaimType, _tenant.TenantId.ToString())
        };

        if (!string.IsNullOrEmpty(email))
            claims.Add(new(JwtRegisteredClaimNames.Email, email));

        if (permissions != null && permissions.Length > 0)
            claims.Add(new("permissions", System.Text.Json.JsonSerializer.Serialize(permissions)));

        var token = new JwtSecurityToken(
            issuer:             _jwt.Issuer,
            audience:           _jwt.Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Gera um refresh token aleatório e seguro (256 bits).</summary>
    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Retorna SHA-256 hex do token — o que é persistido no banco.
    /// O token bruto trafega apenas no cookie HttpOnly.
    /// </summary>
    private static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // =========================================================================
    // ACESSO DO CLIENTE PELO SITE
    // =========================================================================

    public async Task<CpfLookupResponse> LookupByCpfAsync(string cpf)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Cpf == cpf && u.IsActive);
        if (user == null)
            throw new KeyNotFoundException("CPF não encontrado. Acesse a loja e escaneie o QR Code para criar sua conta.");

        return new CpfLookupResponse(user.Name, user.PasswordHash != null);
    }

    public async Task<AuthResponse> SetupAccountAsync(SetupAccountRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Cpf == request.Cpf && u.IsActive);
        if (user == null)
            throw new KeyNotFoundException("CPF não encontrado.");

        var emailInUse = await _db.Users.AnyAsync(u => u.Email == request.Email.ToLowerInvariant() && u.Id != user.Id);
        if (emailInUse)
            throw new InvalidOperationException("Este e-mail já está em uso por outra conta.");

        user.Email        = request.Email.ToLowerInvariant();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        user.UpdatedAt    = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Conta ativada para cliente {Name}", user.Name);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var emailInUse = await _db.Users.AnyAsync(u => u.Email == email);
        if (emailInUse)
            throw new InvalidOperationException("Este e-mail já está em uso. Tente fazer login.");

        var cpf = string.IsNullOrWhiteSpace(request.Cpf) ? null : request.Cpf;
        if (cpf is not null)
        {
            var cpfInUse = await _db.Users.AnyAsync(u => u.Cpf == cpf);
            if (cpfInUse)
                throw new InvalidOperationException("Este CPF já está cadastrado. Tente fazer login ou use \"Esqueci minha senha\".");
        }

        var user = new User
        {
            Name         = request.Name.Trim(),
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            WhatsApp     = string.IsNullOrWhiteSpace(request.WhatsApp) ? null : request.WhatsApp,
            Cpf          = cpf,
            Role         = UserRole.Customer,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Nova conta criada via cadastro público: {Name} ({Email})", user.Name, user.Email);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> ClientLoginAsync(ClientLoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Email == request.Email.ToLowerInvariant() && u.IsActive && u.Role == UserRole.Customer);

        if (user == null || user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("E-mail ou senha inválidos.");

        return await GenerateAuthResponseAsync(user);
    }

    // =========================================================================
    // RECUPERAÇÃO DE SENHA
    // =========================================================================

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        // Sempre retorna sem erro — não revelar se email existe (evita user enumeration)
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

        // Gera token seguro (256 bits), reaproveitado pra User e ContadorAccount.
        static string NovoToken()
        {
            var tokenBytes = new byte[32];
            using var rng  = RandomNumberGenerator.Create();
            rng.GetBytes(tokenBytes);
            return Convert.ToBase64String(tokenBytes);
        }

        if (user != null)
        {
            var token = NovoToken();
            user.PasswordResetToken       = token;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2);
            user.UpdatedAt                = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _email.SendPasswordResetAsync(user.Email!, user.Name, token);
            _logger.LogInformation("Solicitação de reset de senha para {Email}", MaskEmail(request.Email));
            return;
        }

        var conta = await _catalog.ContadorAccounts.FirstOrDefaultAsync(c => c.Email == email);
        if (conta != null)
        {
            var token = NovoToken();
            conta.PasswordResetToken       = token;
            conta.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2);
            conta.UpdatedAt                = DateTime.UtcNow;
            await _catalog.SaveChangesAsync();

            await _email.SendPasswordResetAsync(conta.Email, conta.Name, token);
            _logger.LogInformation("Solicitação de reset de senha (contador) para {Email}", MaskEmail(request.Email));
            return;
        }

        await Task.Delay(Random.Shared.Next(200, 500)); // timing equalization
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == request.Token &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow &&
            u.IsActive);

        if (user != null)
        {
            user.PasswordHash             = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.PasswordResetToken       = null;
            user.PasswordResetTokenExpiry = null;
            user.RefreshToken             = null; // invalida sessões ativas
            user.RefreshTokenExpiry       = null;
            user.UpdatedAt                = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation("Senha redefinida para usuário {UserId}", user.Id);
            return;
        }

        var conta = await _catalog.ContadorAccounts.FirstOrDefaultAsync(c =>
            c.PasswordResetToken == request.Token &&
            c.PasswordResetTokenExpiry > DateTime.UtcNow);

        if (conta == null)
            throw new UnauthorizedAccessException("Token inválido ou expirado.");

        conta.PasswordHash             = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        conta.PasswordResetToken       = null;
        conta.PasswordResetTokenExpiry = null;
        conta.RefreshToken             = null;
        conta.RefreshTokenExpiry       = null;
        conta.UpdatedAt                = DateTime.UtcNow;

        await _catalog.SaveChangesAsync();
        _logger.LogInformation("Senha redefinida para contador {ContadorId}", conta.Id);
    }

    // =========================================================================
    // CADASTRO PÚBLICO DE CONTADOR — cria a conta cross-tenant e já solicita
    // acesso (Pending) à loja informada pelo slug. Aprovação fica a cargo do
    // lojista em /admin/fiscal.
    // =========================================================================
    public async Task<AuthResponse> RegisterContadorAsync(ContadorRegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var emailInUse = await _catalog.ContadorAccounts.AnyAsync(c => c.Email == email);
        if (emailInUse)
            throw new InvalidOperationException("Este e-mail já está cadastrado. Tente fazer login.");

        var slug = request.TenantSlug.Trim().ToLowerInvariant();
        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Slug == slug);
        if (tenant is null)
            throw new KeyNotFoundException("Loja não encontrada. Confira o código/slug informado com o lojista.");

        var conta = new ContadorAccount
        {
            Name         = request.Name.Trim(),
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        };
        _catalog.ContadorAccounts.Add(conta);

        _catalog.ContadorTenantLinks.Add(new ContadorTenantLink
        {
            ContadorAccountId = conta.Id,
            TenantId          = tenant.Id,
            Status            = ContadorLinkStatus.Pending,
        });

        await _catalog.SaveChangesAsync();

        _logger.LogInformation(
            "Novo contador cadastrado: {Name} ({Email}), solicitando acesso à loja '{Slug}'",
            conta.Name, conta.Email, slug);

        return await GenerateContadorAuthResponseAsync(conta);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var local = email[..at];
        var visible = local.Length > 1 ? local[0] + new string('*', Math.Min(local.Length - 1, 3)) : "*";
        return visible + email[at..];
    }
}
