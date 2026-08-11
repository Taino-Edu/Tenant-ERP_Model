using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IProspectingService
{
    /// <summary>Busca negócios por categoria+cidade via OpenStreetMap
    /// (Nominatim + Overpass API) e já classifica cada um (presença digital,
    /// score, faixa de faturamento) sem gastar IA. Lança ArgumentException se
    /// a cidade não for encontrada, ou InvalidOperationException se o
    /// Overpass/Nominatim falhar.</summary>
    Task<ProspectingSearchResultDto> SearchAsync(string categoria, string cidade, bool forceRefresh = false);

    Task<List<ProspectingSearchSummaryDto>> ListSearchesAsync(int limit = 20);

    Task<ProspectingSearchResultDto?> GetSearchAsync(Guid id);

    IReadOnlyList<string> ListSupportedCategories();

    /// <summary>Enriquece um candidato específico via Gemini (chave dedicada de
    /// prospecção, separada da usada pelo Assistente de IA das lojas) — gera
    /// somente uma sugestão de abordagem personalizada. Dados empresariais e
    /// financeiros não são inventados pela IA. Só roda quando chamado
    /// explicitamente (nunca automático durante a busca).</summary>
    Task<ProspectingEnrichResponse> EnrichWithAiAsync(ProspectingEnrichRequest request);
}
