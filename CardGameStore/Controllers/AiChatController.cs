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
            return Ok(await _ai.ChatAsync(request.Message));
        }
        catch (Exception ex)
        {
            // M15: Error carregava ex.Message pro cliente — frontend nunca leu esse campo
            // (só Reply/Success/Action), então era vazamento puro de detalhe interno (stack
            // trace, mensagem de driver de banco etc.) sem nenhum uso legítimo do outro lado.
            // O log já tem o detalhe completo; a resposta ao cliente fica só com o texto genérico.
            _logger.LogError(ex, "AiChatController: erro inesperado.");
            return Ok(new AiChatResponse
            {
                Reply   = "Ocorreu um erro ao processar sua pergunta. Tente novamente.",
                Success = false,
            });
        }
    }
}
