// =============================================================================
// IAiChatService.cs — Contrato do assistente IA conversacional
// =============================================================================

namespace CardGameStore.Services.Interfaces;

public interface IAiChatService
{
    /// <summary>
    /// Recebe a mensagem do admin, busca contexto real da loja e retorna
    /// uma resposta em linguagem natural gerada pelo Gemini 2.0 Flash.
    /// </summary>
    Task<string> ChatAsync(string userMessage);
}
