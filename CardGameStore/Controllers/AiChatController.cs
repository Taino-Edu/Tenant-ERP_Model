// =============================================================================
// AiChatController.cs — Assistente IA conversacional para o painel admin
//
// Endpoint: POST /api/ai/chat
// Acesso:   AdminOnly (JWT obrigatório)
// =============================================================================

using System.Text.Json;
using CardGameStore.DTOs;
using CardGameStore.Middleware;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Policy = "AdminOnly")]
[RequireModule("ia")]
[RequireOperatorPermission(Permissao.Ia)]
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

    /// <summary>
    /// Mesma coisa que POST /chat, mas devolve a resposta em streaming
    /// (Server-Sent Events) — o widget vai mostrando o texto conforme chega em
    /// vez de esperar a resposta inteira, que é o que fazia o assistente parecer
    /// lento mesmo quando o Gemini respondia rápido.
    /// </summary>
    [HttpPost("chat/stream")]
    public async Task ChatStream([FromBody] AiChatRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        // nginx: desliga o buffer de proxy pra esse response — sem isso o SSE
        // chega inteiro de uma vez só no cliente em produção, mesmo funcionando
        // certinho em dev (next dev não bufferiza).
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var evt in _ai.ChatStreamAsync(request.Message, ct))
            {
                await Response.WriteAsync($"data: {JsonSerializer.Serialize(evt)}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Cliente fechou a conexão (navegou pra outra tela, fechou o widget) — normal, não é erro.
        }
        catch (Exception ex)
        {
            // Mesmo motivo do catch em Chat(): nunca vaza ex.Message pro cliente.
            _logger.LogError(ex, "AiChatController: erro inesperado (stream).");
            var errEvt = new AiStreamEvent { Delta = "Ocorreu um erro ao processar sua pergunta. Tente novamente.", Done = true };
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(errEvt)}\n\n", ct);
        }
    }
}
