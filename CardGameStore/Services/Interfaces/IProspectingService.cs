using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IProspectingService
{
    /// <summary>Busca negócios por categoria+cidade via OpenStreetMap
    /// (Nominatim + Overpass API) e já classifica cada um (presença digital,
    /// score, faixa de faturamento) sem gastar IA. Lança ArgumentException se
    /// a cidade não for encontrada, ou InvalidOperationException se o
    /// Overpass/Nominatim falhar.</summary>
    Task<List<ProspectCandidateDto>> SearchAsync(string categoria, string cidade);

    /// <summary>Enriquece um candidato específico via Gemini (chave dedicada de
    /// prospecção, separada da usada pelo Assistente de IA das lojas) — gera
    /// uma faixa de faturamento mais fina e uma sugestão de abordagem
    /// personalizada. Só roda quando chamado explicitamente (nunca automático
    /// durante a busca, pra não gastar IA à toa).</summary>
    Task<ProspectingEnrichResponse> EnrichWithAiAsync(ProspectingEnrichRequest request);
}
