// =============================================================================
// IAiChatService.cs — Contrato do assistente IA conversacional
// =============================================================================

using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IAiChatService
{
    /// <summary>
    /// Recebe a mensagem do admin, busca contexto real da loja e retorna
    /// uma resposta em linguagem natural gerada pelo Gemini 2.0 Flash.
    /// Pode incluir uma action (navegação, abrir wizard) detectada na resposta.
    /// </summary>
    Task<AiChatResponse> ChatAsync(string userMessage);

    /// <summary>
    /// Mesma coisa que ChatAsync, mas transmite a resposta aos pedaços (delta a
    /// delta) conforme o Gemini gera — reduz a sensação de espera do widget, que
    /// antes ficava parado até o texto inteiro estar pronto. O último evento
    /// (Done=true) traz a action já extraída/limpa dos marcadores [NAV:...]/[WIZARD].
    /// </summary>
    IAsyncEnumerable<AiStreamEvent> ChatStreamAsync(string userMessage, CancellationToken ct = default);
}
