// =============================================================================
// UserController.cs — Endpoints de Usuários e Pontos
// GET  /api/user            → lista clientes com pontos (Admin)
// GET  /api/user/me         → perfil do usuário logado
// GET  /api/user/{id}       → detalhe de um cliente (Admin)
// POST /api/user/{id}/points → adiciona pontos (Admin)
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
    private readonly IUserService _service;

    public UserController(IUserService service)
    {
        _service = service;
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

    /// <summary>Detalhes de um cliente específico (Admin).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _service.GetByIdAsync(id);
        return user == null ? NotFound(new { Message = "Usuário não encontrado." }) : Ok(user);
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

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
