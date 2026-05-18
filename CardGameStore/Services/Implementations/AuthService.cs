// =============================================================================
// AuthService.cs — Stub da implementação de Autenticação
// TODO: Implementar na Fase 1.B
// =============================================================================
using CardGameStore.Configuration;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
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
    private readonly JwtSettings           _jwt;
    private readonly ILogger<AuthService>  _logger;
    private readonly IComandaService       _comandaService;
    private readonly IEmailService         _email;

    public AuthService(
        AppDbContext db,
        IOptions<JwtSettings> jwt,
        ILogger<AuthService> logger,
        IComandaService comandaService,
        IEmailService email)
    {
        _db             = db;
        _jwt            = jwt.Value;
        _logger         = logger;
        _comandaService = comandaService;
        _email          = email;
    }

    // =========================================================================
    // LOGIN COMPLETO — Admin e jogadores de campeonato
    // =========================================================================
    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        // PasswordHash pode ser null para clientes de quick-login.
        // Verificar null antes de chamar BCrypt.Verify evita NullReferenceException.
        if (user == null || user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("E-mail ou senha inválidos.");

        return await GenerateAuthResponseAsync(user);
    }

    // =========================================================================
    // LOGIN RÁPIDO — Customer via QR Code (CPF + WhatsApp)
    // =========================================================================
    public async Task<AuthResponse> QuickLoginAsync(QuickLoginRequest request)
    {
        // Busca por CPF primeiro
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Cpf == request.Cpf);

        if (user == null)
        {
            // Cria o cliente automaticamente na primeira visita
            user = new User
            {
                Name      = request.Name,
                Cpf       = request.Cpf,
                WhatsApp  = request.WhatsApp,
                Role      = UserRole.Customer,
                IsActive  = true
            };
            _db.Users.Add(user);
            // LGPD: CPF não é logado — dado sensível do titular
            _logger.LogInformation("Novo cliente criado via QR Code: {Name}", request.Name);
        }
        else
        {
            // Atualiza dados se necessário
            user.Name     = request.Name;
            user.WhatsApp = request.WhatsApp;
            user.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Abre (ou reutiliza) a comanda para esta mesa/sessão
        var comanda = await _comandaService.OpenComandaAsync(user.Id, request.TableIdentifier);
        _logger.LogInformation("Comanda {ComandaId} associada ao quick-login de {Name}", comanda.Id, user.Name);

        return await GenerateAuthResponseAsync(user, comanda.Id);
    }

    // =========================================================================
    // REFRESH TOKEN
    // =========================================================================
    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.RefreshToken == request.RefreshToken && u.IsActive
        );

        if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token inválido ou expirado.");

        return await GenerateAuthResponseAsync(user);
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
        }
    }

    // =========================================================================
    // HELPERS PRIVADOS
    // =========================================================================

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user, Guid? comandaId = null)
    {
        var accessToken  = GenerateJwt(user);
        var refreshToken = GenerateRefreshToken();
        var expiresAt    = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes);

        // Persiste o refresh token
        user.RefreshToken       = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays);
        user.UpdatedAt          = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new AuthResponse(accessToken, refreshToken, expiresAt, user.Role, user.Name, user.Id, comandaId);
    }

    private string GenerateJwt(User user)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name,  user.Name),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(ClaimTypes.Role,               user.Role)
        };

        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new(JwtRegisteredClaimNames.Email, user.Email));

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

    // =========================================================================
    // RECUPERAÇÃO DE SENHA
    // =========================================================================

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        // Sempre retorna sem erro — não revelar se email existe (evita user enumeration)
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && u.IsActive);

        if (user == null) return;

        // Gera token seguro e salva com expiração de 2h
        var tokenBytes = new byte[32];
        using var rng  = RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes);

        user.PasswordResetToken       = token;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2);
        user.UpdatedAt                = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _email.SendPasswordResetAsync(user.Email!, user.Name, token);
        _logger.LogInformation("Solicitação de reset de senha para {Email}", request.Email);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == request.Token &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow &&
            u.IsActive);

        if (user == null)
            throw new UnauthorizedAccessException("Token inválido ou expirado.");

        user.PasswordHash             = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken       = null;
        user.PasswordResetTokenExpiry = null;
        user.RefreshToken             = null; // invalida sessões ativas
        user.RefreshTokenExpiry       = null;
        user.UpdatedAt                = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Senha redefinida para usuário {UserId}", user.Id);
    }
}
