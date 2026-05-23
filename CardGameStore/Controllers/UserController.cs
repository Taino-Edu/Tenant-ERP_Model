// =============================================================================
// UserController.cs — Endpoints de Usuários e Pontos
// GET    /api/user            → lista clientes com pontos (Admin)
// GET    /api/user/me         → perfil do usuário logado
// PUT    /api/user/me         → titular corrige seus próprios dados (LGPD retificação)
// DELETE /api/user/me         → titular solicita exclusão/anonimização (LGPD Art. 18)
// GET    /api/user/{id}       → detalhe de um cliente (Admin)
// POST   /api/user/{id}/points → adiciona pontos (Admin)
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class UserController : ControllerBase
{
    private readonly IUserService  _service;
    private readonly IAuditService _audit;

    public UserController(IUserService service, IAuditService audit)
    {
        _service = service;
        _audit   = audit;
    }

    /// <summary>Lista todos os clientes ativos. Admin pode buscar por nome/CPF/WhatsApp.</summary>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<UserSummaryDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var users = await _service.GetAllAsync(search);
        return Ok(users);
    }

    /// <summary>Perfil completo do usuário logado (pontos, dados pessoais).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMe()
    {
        var userId  = GetUserId();
        var profile = await _service.GetProfileAsync(userId);
        return profile == null ? NotFound() : Ok(profile);
    }

    /// <summary>
    /// Permite ao titular corrigir seus próprios dados pessoais.
    /// LGPD — Direito de retificação (Art. 18, IV).
    /// </summary>
    [HttpPut("me")]
    [Authorize(Policy = "CustomerOrAdmin")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetUserId();
            var result = await _service.UpdateMeAsync(userId, request);

            // Audit log — LGPD: retificação de dados pelo titular
            await _audit.LogAsync("Editou", "User", userId.ToString(), httpContext: HttpContext);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Anonimiza os dados do titular (exclusão lógica).
    /// O registro é mantido para preservar o histórico de comandas e crediários,
    /// mas todos os dados pessoais identificáveis são removidos.
    /// LGPD — Direito de exclusão (Art. 18, VI).
    /// </summary>
    [HttpDelete("me")]
    [Authorize(Policy = "CustomerOrAdmin")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteMe()
    {
        try
        {
            var userId = GetUserId();

            // Audit log ANTES da anonimização — depois o userId ainda existe no banco
            await _audit.LogAsync("Exclusao", "User", userId.ToString(),
                details: "{\"motivo\":\"SolicitacaoTitular\"}", httpContext: HttpContext);

            await _service.AnonimizarAsync(userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>Detalhes de um cliente específico (Admin).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _service.GetByIdAsync(id);
        if (user == null)
            return NotFound(new { Message = "Usuário não encontrado." });

        // Audit log — Admin visualizando dados pessoais de cliente (LGPD rastreabilidade)
        await _audit.LogAsync("Visualizou", "User", id.ToString(), httpContext: HttpContext);

        return Ok(user);
    }

    /// <summary>Adiciona pontos ao saldo de um cliente (Admin).</summary>
    [HttpPost("{id:guid}/points")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddPoints(Guid id, [FromBody] AddPointsRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = GetUserId();
            var result  = await _service.AddPointsAsync(id, request, adminId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Ajusta o saldo monetário de um cliente (Admin).
    /// Positivo = crédito (recarga), negativo = débito (uso).
    /// </summary>
    [HttpPost("{id:guid}/balance")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AdjustBalance(Guid id, [FromBody] AdjustBalanceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = GetUserId();
            var result  = await _service.AdjustBalanceAsync(id, request, adminId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
