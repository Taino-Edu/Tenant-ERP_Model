// =============================================================================
// KycController.cs — Endpoints de verificação de maioridade (KYC)
//
// STATUS: ESQUELETO — NÃO IMPLEMENTADO
// Rotas definidas, lógica toda marcada como TODO.
// Aguardando decisão com Maikon sobre método de verificação.
// Ver: /KYC_PLANNING.md
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

// TODO: descomentar quando IKycService for implementado
// using CardGameStore.Services.Interfaces;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/kyc")]
[Produces("application/json")]
[Authorize]
public class KycController : ControllerBase
{
    // TODO: injetar IKycService quando implementado
    // private readonly IKycService _kyc;
    // public KycController(IKycService kyc) => _kyc = kyc;

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException();
        return id;
    }

    /// <summary>
    /// GET /api/kyc/status
    /// Retorna o status atual de verificação do usuário logado.
    /// Frontend usa para saber se bloqueia ou libera o Marketplace.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        // TODO: implementar
        // var userId = GetUserId();
        // var status = await _kyc.GetStatusAsync(userId);
        // return Ok(status);

        return StatusCode(501, new { Message = "KYC não implementado ainda. Ver KYC_PLANNING.md" });
    }

    /// <summary>
    /// POST /api/kyc/self-declaration
    /// Body: { birthDate: "YYYY-MM-DD" }
    /// Opção 1 (mais simples): usuário declara a própria data de nascimento.
    /// Armazenamos e calculamos a idade — sem validação externa.
    /// </summary>
    [HttpPost("self-declaration")]
    public IActionResult SelfDeclaration([FromBody] SelfDeclarationRequest req)
    {
        // TODO: implementar
        // var userId = GetUserId();
        // var result = await _kyc.VerifyBySelfDeclarationAsync(userId, req.BirthDate);
        // return result.IsApproved ? Ok(result) : BadRequest(result);

        return StatusCode(501, new { Message = "KYC não implementado ainda. Ver KYC_PLANNING.md" });
    }

    /// <summary>
    /// POST /api/kyc/cpf
    /// Body: { cpf: "000.000.000-00", birthDate: "YYYY-MM-DD" }
    /// Opção 2 (recomendada): valida CPF + data nascimento contra a Receita via BrasilAPI.
    /// Gratuito, sem custo por verificação.
    /// </summary>
    [HttpPost("cpf")]
    public IActionResult VerifyByCpf([FromBody] CpfVerificationRequest req)
    {
        // TODO: implementar
        // var userId = GetUserId();
        // var result = await _kyc.VerifyByCpfReceitaAsync(userId, req.Cpf, req.BirthDate);
        // return result.IsApproved ? Ok(result) : BadRequest(result);

        return StatusCode(501, new { Message = "KYC não implementado ainda. Ver KYC_PLANNING.md" });
    }

    /// <summary>
    /// GET /api/kyc/admin/list  [Admin only]
    /// Lista verificações para auditoria.
    /// </summary>
    [HttpGet("admin/list")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult AdminList([FromQuery] string? status = null, [FromQuery] int page = 1)
    {
        // TODO: implementar
        return StatusCode(501, new { Message = "KYC não implementado ainda. Ver KYC_PLANNING.md" });
    }

    /// <summary>
    /// POST /api/kyc/admin/{userId}/override  [Admin only]
    /// Admin pode aprovar ou rejeitar manualmente (ex: verificou presencialmente na loja).
    /// </summary>
    [HttpPost("admin/{userId:guid}/override")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult AdminOverride(Guid userId, [FromBody] AdminOverrideRequest req)
    {
        // TODO: implementar
        return StatusCode(501, new { Message = "KYC não implementado ainda. Ver KYC_PLANNING.md" });
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record SelfDeclarationRequest(DateOnly BirthDate);

public record CpfVerificationRequest(string Cpf, DateOnly BirthDate);

public record AdminOverrideRequest(
    string Approved,       // "approved" | "rejected"
    string? Reason = null
);
