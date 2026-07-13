// =============================================================================
// AuthController.cs — Endpoints de Autenticação
//
// POST /api/auth/login         → Login do Admin (email + senha)
// POST /api/auth/quick-login   → Login do Cliente via QR Code (CPF + WhatsApp)
// POST /api/auth/refresh       → Renovar o access token usando o refresh token
// POST /api/auth/logout        → Invalidar o refresh token (encerrar sessão)
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CardGameStore.Configuration;
using System.Text.Json;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService            _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly JwtSettings             _jwt;
    private readonly IWebHostEnvironment     _env;
    private readonly IConfiguration          _config;
    private readonly IEmailService           _emailService;
    private readonly IAuditService           _audit;
    private readonly CatalogDbContext        _catalog;
    private readonly ITenantContext          _tenant;

    public AuthController(
        IAuthService        authService,
        ILogger<AuthController> logger,
        IOptions<JwtSettings>   jwt,
        IWebHostEnvironment     env,
        IConfiguration      configuration,
        IEmailService       emailService,
        IAuditService       audit,
        CatalogDbContext    catalog,
        ITenantContext      tenant)
    {
        _authService  = authService;
        _logger       = logger;
        _jwt          = jwt.Value;
        _audit        = audit;
        _env          = env;
        _config       = configuration;
        _emailService = emailService;
        _catalog      = catalog;
        _tenant       = tenant;
    }

    // =========================================================================
    // HELPERS — Cookies HttpOnly (LGPD / Segurança)
    // =========================================================================

    /// <summary>
    /// Grava accessToken e refreshToken como cookies HttpOnly,
    /// impedindo acesso via JavaScript (proteção contra XSS).
    /// </summary>
    private void SetAuthCookies(string accessToken, string refreshToken)
    {
        // Secure = true em produção (HTTPS via Cloudflare). Em desenvolvimento HTTP local, false.
        // COOKIE_SECURE explícito tem prioridade — permite testar em produção via IP puro/HTTP
        // (sem domínio/HTTPS ainda) sem o cookie ser descartado pelo navegador.
        var secureCookies = _config.GetValue<bool?>("COOKIE_SECURE") ?? !_env.IsDevelopment();

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure   = secureCookies,
            SameSite = SameSiteMode.Lax, // Lax permite redirecionamentos cross-page
            Path     = "/",
        };

        Response.Cookies.Append("accessToken", accessToken, new CookieOptions
        {
            HttpOnly = cookieOptions.HttpOnly,
            Secure   = cookieOptions.Secure,
            SameSite = cookieOptions.SameSite,
            Path     = cookieOptions.Path,
            MaxAge   = TimeSpan.FromMinutes(_jwt.AccessTokenExpirationMinutes)
        });

        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = cookieOptions.HttpOnly,
            Secure   = cookieOptions.Secure,
            SameSite = cookieOptions.SameSite,
            Path     = cookieOptions.Path,
            MaxAge   = TimeSpan.FromDays(_jwt.RefreshTokenExpirationDays)
        });
    }

    /// <summary>
    /// Só o accessToken, sem refreshToken — usado pra sessão de impersonação, que
    /// não pode se renovar sozinha (expira de propósito depois de ~20min; quando
    /// isso acontece, o interceptor de 401 do frontend já manda pro /login).
    /// </summary>
    private void SetAccessCookieOnly(string accessToken, TimeSpan maxAge)
    {
        var secureCookies = _config.GetValue<bool?>("COOKIE_SECURE") ?? !_env.IsDevelopment();

        Response.Cookies.Append("accessToken", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure   = secureCookies,
            SameSite = SameSiteMode.Lax,
            Path     = "/",
            MaxAge   = maxAge,
        });
    }

    /// <summary>
    /// Cookies simples (não-HttpOnly) que a UI lê pro estado de sessão —
    /// espelha exatamente o que frontend/lib/auth.ts:saveAuth() grava depois de
    /// um login normal via JSON. Esse fluxo de impersonação é um redirect puro
    /// do servidor (sem round-trip de JSON pelo frontend), então precisa gravar
    /// esses cookies aqui mesmo, senão a API autentica mas a UI parece deslogada.
    /// </summary>
    private void SetUiSessionCookies(string role, string userName, Guid userId, string? impersonatingOwnerName, TimeSpan maxAge)
    {
        var options = new CookieOptions { Path = "/", MaxAge = maxAge };
        Response.Cookies.Append("userRole", role, options);
        Response.Cookies.Append("userName", userName, options);
        Response.Cookies.Append("userId",   userId.ToString(), options);
        if (impersonatingOwnerName != null)
            Response.Cookies.Append("impersonating", impersonatingOwnerName, options);
    }

    /// <summary>Remove os cookies de autenticação no logout.</summary>
    private void ClearAuthCookies()
    {
        Response.Cookies.Delete("accessToken");
        Response.Cookies.Delete("refreshToken");
    }

    // =========================================================================
    // LOGIN COMPLETO — Admin (email + senha)
    // =========================================================================

    /// <summary>
    /// Login com e-mail e senha. Usado pelo Admin/Operator da loja.
    /// Retorna um access token JWT (60 min) e um refresh token (30 dias).
    /// </summary>
    /// <response code="200">Login realizado com sucesso.</response>
    /// <response code="401">Credenciais inválidas.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var response = await _authService.LoginAsync(request);
            _logger.LogInformation("Login bem-sucedido para {Email}", request.Email);
            await _audit.LogAsync("LoginSucesso", "Auth", response.UserId.ToString(),
                details: JsonSerializer.Serialize(new { email = request.Email, role = response.Role }),
                httpContext: HttpContext);
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId, Permissions: response.Permissions));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Tentativa de login inválida para {Email}: {Msg}", request.Email, ex.Message);
            await _audit.LogAsync("LoginFalhou", "Auth", null,
                details: JsonSerializer.Serialize(new { email = request.Email, motivo = ex.Message }),
                httpContext: HttpContext);
            return Unauthorized(new { Message = "E-mail ou senha incorretos." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no login para {Email}", request.Email);
            return StatusCode(500, new { Message = "Erro interno. Tente novamente." });
        }
    }

    // =========================================================================
    // LOGIN RÁPIDO — Cliente via QR Code (CPF + WhatsApp)
    // =========================================================================

    /// <summary>
    /// Login rápido para clientes via QR Code nas mesas.
    /// Cria o usuário automaticamente se for a primeira visita (identificado pelo CPF).
    /// Abre (ou reutiliza) a comanda da mesa automaticamente.
    /// Retorna o access token + o ID da comanda aberta.
    /// </summary>
    /// <response code="200">Login e comanda abertura realizados com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    [HttpPost("quick-login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> QuickLogin([FromBody] QuickLoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var response = await _authService.QuickLoginAsync(request);
            // LGPD: CPF removido do log — apenas nome e mesa são necessários para auditoria
            _logger.LogInformation(
                "Quick-login realizado: {Name} | Mesa: {Table} | Comanda: {ComandaId}",
                request.Name, request.TableIdentifier, response.ComandaId);
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId, response.ComandaId, response.Permissions));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Quick-login com dados inválidos: {Msg}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Erro de operação no quick-login: {Msg}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no quick-login");
            return StatusCode(500, new { Message = "Erro interno. Tente novamente." });
        }
    }

    // =========================================================================
    // REFRESH TOKEN — Renovar o access token sem novo login
    // =========================================================================

    /// <summary>
    /// Renova o access token usando o refresh token.
    /// Use quando o access token expirar (após 60 minutos).
    /// O refresh token é válido por 30 dias.
    /// </summary>
    /// <response code="200">Novo access token gerado.</response>
    /// <response code="401">Refresh token inválido ou expirado.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? request)
    {
        // Cookie HttpOnly tem prioridade; fallback para o body (compatibilidade)
        var refreshToken = Request.Cookies["refreshToken"]
                           ?? request?.RefreshToken;

        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { Message = "Refresh token não encontrado." });

        try
        {
            var tokenRequest = new RefreshTokenRequest(refreshToken);
            var response = await _authService.RefreshTokenAsync(tokenRequest);
            // Renova os cookies com os novos tokens
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId, Permissions: response.Permissions));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Refresh token inválido ou expirado: {Msg}", ex.Message);
            ClearAuthCookies();
            return Unauthorized(new { Message = "Refresh token inválido ou expirado. Faça login novamente." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no refresh de token");
            return StatusCode(500, new { Message = "Erro interno. Tente novamente." });
        }
    }

    // =========================================================================
    // ACESSO DO CLIENTE PELO SITE
    // =========================================================================

    /// <summary>
    /// Busca um cliente pelo CPF pra saber se já tem cadastro (e nesse caso, se
    /// precisa só de senha ou de conta nova) antes do fluxo de login/cadastro
    /// pela área do cliente. 404 se o CPF não tem nenhum registro.
    /// </summary>
    [HttpPost("cpf-lookup")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> CpfLookup([FromBody] CpfLookupRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _authService.LookupByCpfAsync(request.Cpf);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { Message = ex.Message }); }
    }

    /// <summary>
    /// Ativa a conta de um cliente que já existe (criado via quick-login na mesa)
    /// mas nunca definiu e-mail/senha — define os dois de uma vez e já retorna
    /// login efetuado. 404 se o CPF não existe, 409 se o e-mail já está em uso.
    /// </summary>
    [HttpPost("setup-account")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SetupAccount([FromBody] SetupAccountRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var response = await _authService.SetupAccountAsync(request);
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId));
        }
        catch (KeyNotFoundException ex)    { return NotFound(new { Message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { Message = ex.Message }); }
    }

    /// <summary>Login de cliente já cadastrado com e-mail e senha (área do cliente).</summary>
    [HttpPost("client-login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ClientLogin([FromBody] ClientLoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var response = await _authService.ClientLoginAsync(request);
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId));
        }
        catch (UnauthorizedAccessException) { return Unauthorized(new { Message = "E-mail ou senha inválidos." }); }
    }

    /// <summary>Cadastro público de um novo cliente (nome, e-mail, senha). 409 se o e-mail já existe.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var response = await _authService.RegisterAsync(request);
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId));
        }
        catch (InvalidOperationException ex) { return Conflict(new { Message = ex.Message }); }
    }

    // =========================================================================
    // CADASTRO PÚBLICO DE CONTADOR — cria a conta cross-tenant e solicita acesso
    // (Pending) à loja pelo slug. Sem [Authorize] — acessível pelo domínio raiz.
    // =========================================================================
    /// <summary>
    /// Cadastro público de contador — cria a conta cross-tenant (uma única conta pra
    /// gerenciar várias lojas) e já cria uma solicitação de acesso (Pending) pra
    /// loja informada pelo slug, aguardando aprovação do lojista.
    /// </summary>
    [HttpPost("contador/register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterContador([FromBody] ContadorRegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var response = await _authService.RegisterContadorAsync(request);
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId));
        }
        catch (InvalidOperationException ex) { return Conflict(new { Message = ex.Message }); }
        catch (KeyNotFoundException ex)      { return NotFound(new { Message = ex.Message }); }
    }

    // =========================================================================
    // FORGOT PASSWORD — Solicitar reset por email
    // =========================================================================

    /// <summary>
    /// Envia email com link de redefinição de senha.
    /// Sempre retorna 204 para não revelar se o email existe.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _authService.ForgotPasswordAsync(request);
        return NoContent();
    }

    // =========================================================================
    // RESET PASSWORD — Redefinir senha com token do email
    // =========================================================================

    /// <summary>
    /// Redefine a senha usando o token recebido por email.
    /// O token expira em 2 horas e é de uso único.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _authService.ResetPasswordAsync(request);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { Message = ex.Message });
        }
    }

    // =========================================================================
    // LOGOUT — Invalidar o refresh token
    // =========================================================================

    /// <summary>
    /// Encerra a sessão do usuário autenticado.
    /// Invalida o refresh token — o próximo acesso exige novo login.
    /// </summary>
    /// <response code="204">Logout realizado com sucesso.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout()
    {
        var claim  = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            return Unauthorized();

        await _authService.LogoutAsync(userId);
        ClearAuthCookies();
        _logger.LogInformation("Logout realizado para usuário {UserId}", userId);
        return NoContent();
    }

    // =========================================================================
    // IMPERSONAÇÃO — Redeem do ticket gerado por PlatformController.Impersonate
    //
    // Sem [Authorize]: o próprio ticket É a credencial (mesmo modelo de
    // confiança de um magic link) — o dono da plataforma não tem sessão nenhuma
    // no domínio da loja alvo ainda, então não daria pra exigir [Authorize] aqui.
    // Essa requisição já bateu no subdomínio da loja — TenantResolutionMiddleware
    // já rodou e _tenant já está resolvido pro tenant certo.
    // =========================================================================
    /// <summary>
    /// Troca um ticket de impersonação (emitido por POST /api/platform/tenants/{id}/impersonate)
    /// por uma sessão autenticada como o dono da plataforma dentro desta loja — só
    /// funciona no subdomínio da loja pra qual o ticket foi emitido, é de uso único e
    /// expira em 90s se não for resgatado. Sempre redireciona (nunca retorna JSON).
    /// </summary>
    /// <param name="ticket">Ticket de uso único gerado pelo mint da impersonação.</param>
    [HttpGet("impersonate")]
    [AllowAnonymous]
    public async Task<IActionResult> Impersonate([FromQuery] string ticket)
    {
        var row = await _catalog.PlatformImpersonationTickets.FirstOrDefaultAsync(t => t.Ticket == ticket);

        if (row is null || row.RedeemedAt != null || row.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Tentativa de redeem de impersonação inválida (ticket inexistente/usado/expirado).");
            return Redirect("/login?error=impersonation_failed");
        }

        // Ticket de uma loja usado no subdomínio de outra — a requisição já
        // resolveu _tenant pelo Host de verdade, então isso pega replay/troca.
        if (row.TenantId != _tenant.TenantId)
        {
            _logger.LogWarning(
                "Ticket de impersonação da loja {TicketSlug} ({TicketTenantId}) usado no domínio de outro tenant ({RequestTenantId}) — bloqueado.",
                row.TenantSlug, row.TenantId, _tenant.TenantId);
            return Redirect("/login?error=impersonation_failed");
        }

        row.RedeemedAt = DateTime.UtcNow;
        await _catalog.SaveChangesAsync();

        try
        {
            var response = await _authService.ImpersonateAsync(row.PlatformOwnerUserId, row.PlatformOwnerName, null);
            var maxAge   = TimeSpan.FromMinutes(20);

            SetAccessCookieOnly(response.AccessToken, maxAge);
            SetUiSessionCookies(response.Role, response.UserName, response.UserId, row.PlatformOwnerName, maxAge);

            _logger.LogInformation("Impersonação resgatada — dono {OwnerId} entrou em {Slug}", row.PlatformOwnerUserId, row.TenantSlug);
            return Redirect("/admin/comanda");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Falha ao gerar sessão de impersonação pra {Slug}: {Msg}", row.TenantSlug, ex.Message);
            return Redirect("/login?error=impersonation_failed");
        }
    }

    // =========================================================================
    // DIAGNÓSTICO — Teste de Email
    // =========================================================================

    /// <summary>
    /// Envia um email de teste para verificar as configurações de SMTP.
    /// Apenas Admin pode disparar este teste.
    /// </summary>
    [HttpPost("test-email")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest request)
    {
        var success = await _emailService.SendDiagnosticEmailAsync(request.Email);
        
        return success 
            ? Ok(new { Message = $"Email de teste enviado com sucesso para {request.Email}. Verifique sua caixa de entrada e SPAM." })
            : BadRequest(new { Message = "Falha ao enviar email. Verifique os logs do servidor para detalhes do erro de SMTP." });
    }
}
