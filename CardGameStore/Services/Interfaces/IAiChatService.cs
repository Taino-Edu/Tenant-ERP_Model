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
}
