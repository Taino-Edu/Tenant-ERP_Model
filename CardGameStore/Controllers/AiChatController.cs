// =============================================================================
// AiChatController.cs — Assistente IA conversacional para o painel admin
//
// Endpoint: POST /api/ai/chat
// Acesso:   AdminOnly (JWT obrigatório)
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Policy = "AdminOnly")]
[EnableRateLimiting("api")]
public class AiChatController : ControllerBase
{
    private readonly IAiChatService          _ai;
    private readonly ILogger<AiChatController> _logger;

    public AiChatController(IAiChatService ai, ILogger<AiChatController> logger)
    {
        _ai     = ai;
        _logger = logger;
    }

    /// <summary>
    /// Envia uma pergunta ao assistente IA e recebe resposta em linguagem natural
    /// com base nos dados reais da loja (vendas, estoque, crediários, clientes).
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(AiChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AiChatResponse>> Chat([FromBody] AiChatRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var reply = await _ai.ChatAsync(request.Message);
            return Ok(new AiChatResponse { Reply = reply, Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiChatController: erro inesperado.");
            return Ok(new AiChatResponse
            {
                Reply   = "Ocorreu um erro ao processar sua pergunta. Tente novamente.",
                Success = false,
                Error   = ex.Message
            });
        }
    }
}
