// =============================================================================
// SignupController.cs — O lojista cria a própria loja pelo site, sem passar por
// ninguém. SEM autenticação.
//
//   GET  /api/signup/slug?slug=   → o endereço está livre?
//   POST /api/signup              → grava o pedido e manda o link por e-mail
//   POST /api/signup/confirmar    → o link foi clicado: cria a loja e devolve o
//                                   ticket de entrada no subdomínio dela
//
// Só responde no domínio raiz. Num subdomínio de loja isso não tem uso nenhum e
// só abriria mais uma porta anônima dentro de cada tenant.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/signup")]
[AllowAnonymous]
[Produces("application/json")]
public class SignupController : ControllerBase
{
    private readonly ITenantSignupService _signup;
    private readonly ITenantContext _tenant;

    public SignupController(ITenantSignupService signup, ITenantContext tenant)
    {
        _signup = signup;
        _tenant = tenant;
    }

    private bool ForaDoDominioRaiz => _tenant.TenantId != TenantConstants.TenantZeroId;

    /// <summary>Diz se um endereço de loja está livre e, se não estiver, sugere outro.</summary>
    [HttpGet("slug")]
    [EnableRateLimiting("public-signup-check")]
    public async Task<ActionResult<DisponibilidadeSlugDto>> VerificarSlug([FromQuery] string? slug, CancellationToken ct)
    {
        if (ForaDoDominioRaiz) return NotFound();
        return Ok(await _signup.VerificarSlugAsync(slug, ct));
    }

    /// <summary>Registra o pedido de loja e envia o link de confirmação.</summary>
    [HttpPost]
    [EnableRateLimiting("public-signup")]
    public async Task<IActionResult> Solicitar([FromBody] SolicitarLojaRequest request, CancellationToken ct)
    {
        if (ForaDoDominioRaiz) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var resultado = await _signup.SolicitarAsync(request, ct);
        return resultado.Status switch
        {
            SolicitacaoStatus.Enviada => Accepted(new { Message = "Enviamos o link de confirmação para o seu e-mail." }),
            SolicitacaoStatus.SlugInvalido => BadRequest(new { Message = resultado.Mensagem, ErrorCode = "slug_invalido" }),
            SolicitacaoStatus.PlanoInvalido => BadRequest(new { Message = resultado.Mensagem, ErrorCode = "plano_invalido" }),
            SolicitacaoStatus.SlugIndisponivel => Conflict(new
            {
                Message = resultado.Mensagem, ErrorCode = "slug_indisponivel", Sugestao = resultado.SugestaoSlug,
            }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { Message = resultado.Mensagem, ErrorCode = "indisponivel" }),
        };
    }

    /// <summary>Confirma o e-mail, cria a loja e devolve o ticket para entrar nela já logado.</summary>
    [HttpPost("confirmar")]
    [EnableRateLimiting("public-signup-check")]
    public async Task<IActionResult> Confirmar([FromBody] ConfirmarLojaRequest request)
    {
        if (ForaDoDominioRaiz) return NotFound();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Sem o CancellationToken da requisição de propósito: ver ConfirmarAsync.
        var resultado = await _signup.ConfirmarAsync(request.Token);
        return resultado.Status switch
        {
            ConfirmacaoStatus.Criada => Ok(new { resultado.Slug, resultado.Ticket }),
            ConfirmacaoStatus.TokenInvalido => NotFound(new { Message = "Link de confirmação inválido.", ErrorCode = "token_invalido" }),
            ConfirmacaoStatus.Expirado => StatusCode(StatusCodes.Status410Gone,
                new { Message = "O link de confirmação expirou.", ErrorCode = "expirado" }),
            ConfirmacaoStatus.JaConfirmada => Conflict(new { Message = "Esta loja já foi criada.", ErrorCode = "ja_confirmada", resultado.Slug }),
            ConfirmacaoStatus.EmAndamento => Conflict(new { Message = "A loja está sendo criada.", ErrorCode = "em_andamento", resultado.Slug }),
            ConfirmacaoStatus.SlugIndisponivel => Conflict(new { Message = resultado.Mensagem, ErrorCode = "slug_indisponivel", resultado.Slug }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { Message = resultado.Mensagem, ErrorCode = "indisponivel", resultado.Slug }),
        };
    }
}
