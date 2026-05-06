// =============================================================================
// AuthController.cs — Endpoints de Autenticação
//
// POST /api/auth/login         → Login do Admin (email + senha)
// POST /api/auth/quick-login   → Login do Cliente via QR Code (CPF + WhatsApp)
// POST /api/auth/refresh       → Renovar o access token usando o refresh token
// POST /api/auth/logout        → Invalidar o refresh token (encerrar sessão)
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService          _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger      = logger;
    }

    // =========================================================================
    // LOGIN COMPLETO — Admin (email + senha)
    // =========================================================================

    /// <summary>
    /// Login com e-mail e senha. Utilizado pelo Admin (Maikon).
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
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Tentativa de login inválida para {Email}: {Msg}", request.Email, ex.Message);
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
            _logger.LogInformation(
                "Quick-login realizado: {Name} | CPF: {Cpf} | Mesa: {Table} | Comanda: {ComandaId}",
                request.Name, request.Cpf, request.TableIdentifier, response.ComandaId);
            return Ok(response);
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
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var response = await _authService.RefreshTokenAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Refresh token inválido ou expirado: {Msg}", ex.Message);
            return Unauthorized(new { Message = "Refresh token inválido ou expirado. Faça login novamente." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no refresh de token");
            return StatusCode(500, new { Message = "Erro interno. Tente novamente." });
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
        _logger.LogInformation("Logout realizado para usuário {UserId}", userId);
        return NoContent();
    }
}
