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
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory  _scopeFactory;

    public AuthService(
        AppDbContext db,
        CatalogDbContext catalog,
        IOptions<JwtSettings> jwt,
        ILogger<AuthService> logger,
        IComandaService comandaService,
        IEmailService email,
        ITenantContext tenant,
        IServiceScopeFactory scopeFactory)
    {
        _db             = db;
        _catalog        = catalog;
        _jwt            = jwt.Value;
        _logger         = logger;
        _comandaService = comandaService;
        _email          = email;
        _tenant         = tenant;
        _scopeFactory   = scopeFactory;
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
        {
            // Essa checagem só pega o caso em que o PRÓPRIO _db (já no schema
            // desta requisição) enxergou a linha — ou seja, quando a pessoa já
            // está no domínio raiz por acaso mas de algum jeito bateu um User
            // de Role=PlatformOwner (não deveria acontecer na prática, é
            // defesa em profundidade). O caso real (tentando logar como dono
            // no subdomínio de uma loja) é pego abaixo, porque a linha do dono
            // fica invisível pra esse _db — ver comentário mais adiante.
            if (user.Role == UserRole.PlatformOwner && _tenant.TenantId != TenantConstants.TenantZeroId)
                throw new WrongDomainLoginException("Essa conta é do Dono da Plataforma — acesse pelo domínio principal.");

            return await GenerateAuthResponseAsync(user);
        }

        // PlatformOwner é um User do schema "public" — se a requisição caiu no
        // subdomínio de uma loja, a busca acima nem ACHA essa linha (schema
        // errado), então o `if` de cima nunca dispara. Sem essa checagem
        // cruzada, o login "funcionava" perfeitamente do ponto de vista de
        // quem tentou (token emitido de verdade) e só quebrava um passo
        // depois: o JWT carrega o tenant do domínio ERRADO onde a pessoa
        // logou, e TenantClaimGuardMiddleware rejeitava a primeira chamada
        // autenticada seguinte com um erro obscuro. Só roda quando a busca
        // normal já falhou E não estamos no domínio raiz — 1 query extra por
        // tentativa de senha errada num subdomínio, não um loop caro.
        if (_tenant.TenantId != TenantConstants.TenantZeroId
            && await IsValidPlatformOwnerPasswordAsync(request.Email, request.Password))
        {
            throw new WrongDomainLoginException("Essa conta é do Dono da Plataforma — acesse pelo domínio principal.");
        }

        // Contador não é um User de tenant nenhum — vive só no catálogo (schema
        // "public"), então cai aqui como segunda tentativa antes de recusar o login.
        var email = request.Email.Trim().ToLowerInvariant();
        var conta = await _catalog.ContadorAccounts.FirstOrDefaultAsync(c => c.Email == email);
        if (conta != null && BCrypt.Net.BCrypt.Verify(request.Password, conta.PasswordHash))
        {
            // Mesmo raciocínio do PlatformOwner acima: Contador é uma conta
            // cross-tenant, só faz sentido pelo domínio raiz.
            if (_tenant.TenantId != TenantConstants.TenantZeroId)
                throw new WrongDomainLoginException("Essa conta é de Contador — acesse pelo domínio principal.");

            return await GenerateContadorAuthResponseAsync(conta);
        }

        throw new UnauthorizedAccessException("E-mail ou senha inválidos.");
    }

    /// <summary>Confirma a senha contra o User de Role=PlatformOwner no schema
    /// "public", num escopo de DI isolado (mesmo padrão de
    /// PlatformController.RunInTenantScopeAsync) — usado só quando a busca normal
    /// de LoginAsync já falhou e não estamos no domínio raiz, pra distinguir
    /// "senha errada de verdade" de "senha certa, domínio errado".</summary>
    private async Task<bool> IsValidPlatformOwnerPasswordAsync(string email, string password)
    {
        using var scope = _scopeFactory.CreateScope();
        var tc = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tc.Set(TenantConstants.TenantZeroId, TenantConstants.TenantZeroSchema, Array.Empty<string>());

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive && u.Role == UserRole.PlatformOwner);

        return owner != null && owner.PasswordHash != null && BCrypt.Net.BCrypt.Verify(password, owner.PasswordHash);
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
        user.LastLoginAt        = DateTime.UtcNow;
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

    /// <summary>
    /// Sessão de impersonação pro dono da plataforma acessar o admin de um tenant
    /// com um clique. Roda no request que já bateu no subdomínio do tenant alvo —
    /// _db já está no schema certo (via ITenantContext/TenantConnectionInterceptor),
    /// não precisa de troca de schema manual aqui.
    ///
    /// `sub` do JWT = Id do PRÓPRIO dono da plataforma, NUNCA o do admin real do
    /// tenant. Se usasse o Id do admin real, qualquer ação self-service durante a
    /// impersonação (trocar senha, editar perfil) mexeria na conta de verdade
    /// dele, e um logout normal (AuthService.LogoutAsync, que revoga token por
    /// sub) derrubaria a sessão real do admin sem querer. Como o Guid do dono é
    /// estranho a qualquer schema de tenant, endpoints que fazem
    /// _db.Users.FindAsync(sub) simplesmente não acham nada e degradam bem
    /// (padrão já usado em UserController — retorna NotFound).
    /// </summary>
    public async Task<AuthResponse> ImpersonateAsync(Guid platformOwnerId, string platformOwnerName, string? platformOwnerEmail)
    {
        var temAdmin = await _db.Users.AnyAsync(u => u.Role == UserRole.Admin && u.IsActive);
        if (!temAdmin)
            throw new InvalidOperationException("Esta loja não tem nenhum admin ativo.");

        var extraClaims = new List<Claim>
        {
            new("imp", "1"),
            new("imp_owner", platformOwnerName),
        };

        var accessToken = GenerateJwt(
            platformOwnerId, platformOwnerName, platformOwnerEmail, UserRole.Admin,
            permissions: null, extraClaims: extraClaims, expiresIn: TimeSpan.FromMinutes(20));

        // Sem refresh token — sessão de impersonação não se renova sozinha depois
        // dos 20min. Quando expira, o interceptor de 401 do frontend já manda pro
        // /login normalmente, sem precisar de lógica nova.
        return new AuthResponse(accessToken, string.Empty, DateTime.UtcNow.AddMinutes(20), UserRole.Admin, platformOwnerName, platformOwnerId);
    }

    public async Task<AuthResponse> CompleteLoginRedirectAsync(string targetKind, Guid accountId)
    {
        if (targetKind == LoginRedirectTargetKind.Contador)
        {
            var conta = await _catalog.ContadorAccounts.FindAsync(accountId)
                ?? throw new InvalidOperationException("Conta de contador não encontrada.");
            return await GenerateContadorAuthResponseAsync(conta);
        }

        // Tenant (Admin/Operator) e PlatformOwner são ambos User — _db já está no
        // schema certo, porque esta própria requisição (o redeem) já resolveu o
        // tenant pelo Host antes de chegar aqui.
        var user = await _db.Users.FindAsync(accountId)
            ?? throw new InvalidOperationException("Usuário não encontrado.");
        return await GenerateAuthResponseAsync(user);
    }

    private string GenerateJwt(
        Guid id, string name, string? email, string role, string[]? permissions = null,
        IEnumerable<Claim>? extraClaims = null, TimeSpan? expiresIn = null)
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

        if (extraClaims != null)
            claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            issuer:             _jwt.Issuer,
            audience:           _jwt.Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(_jwt.AccessTokenExpirationMinutes)),
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

        // Convites cegos: algum lojista já convidou este e-mail antes de a
        // conta existir (FiscalController.ConvidarContador). Consome todos —
        // vínculo nasce direto Approved, sem passar por Pending, já que o
        // lojista já autorizou explicitamente ao convidar.
        var convitesCegos = await _catalog.ContadorConvitesEmail
            .Where(c => c.Email == email)
            .ToListAsync();

        foreach (var convite in convitesCegos)
        {
            _catalog.ContadorTenantLinks.Add(new ContadorTenantLink
            {
                ContadorAccountId = conta.Id,
                TenantId          = convite.TenantId,
                Status            = ContadorLinkStatus.Approved,
            });
        }
        _catalog.ContadorConvitesEmail.RemoveRange(convitesCegos);

        // Vínculo Pending pro slug digitado no cadastro — só se esse tenant
        // não acabou de ganhar um vínculo Approved via convite cego acima
        // (senão violaria o índice único de (ContadorAccountId, TenantId)).
        if (!convitesCegos.Any(c => c.TenantId == tenant.Id))
        {
            _catalog.ContadorTenantLinks.Add(new ContadorTenantLink
            {
                ContadorAccountId = conta.Id,
                TenantId          = tenant.Id,
                Status            = ContadorLinkStatus.Pending,
            });
        }

        await _catalog.SaveChangesAsync();

        _logger.LogInformation(
            "Novo contador cadastrado: {Name} ({Email}), solicitando acesso à loja '{Slug}' ({ConvitesCegos} convite(s) cego(s) consumido(s))",
            conta.Name, conta.Email, slug, convitesCegos.Count);

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

/// <summary>Senha certa, mas conta cross-tenant (PlatformOwner/Contador) logando
/// fora do domínio raiz. Distinto de UnauthorizedAccessException pra o
/// controller devolver uma mensagem específica em vez do "e-mail ou senha
/// incorretos" genérico — a senha estava certa, só o domínio que não.</summary>
public class WrongDomainLoginException : Exception
{
    public WrongDomainLoginException(string message) : base(message) { }
}
