// =============================================================================
// ITcgService.cs — Interface do serviço de integração com a API TCG
//
// ESTRATÉGIA CACHE-FIRST:
//   1. Busca no MongoDB (rápido, offline-capable)
//   2. Se não encontrar OU estiver expirado → chama a API externa
//   3. Salva/atualiza no MongoDB → retorna ao chamador
//
// Princípio de Inversão de Dependência (SOLID-D):
//   Controllers e outros serviços dependem DESTA interface, nunca da implementação.
//   Facilita troca de provider (apitcg.com → TCGplayer, Scryfall, etc.) sem alterar chamadores.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Models.MongoDB;

namespace CardGameStore.Services.Interfaces;

/// <summary>
/// Contrato para o serviço de integração com APIs de TCG.
/// Abstrai a lógica de cache MongoDB + chamada à API externa.
/// </summary>
public interface ITcgService
{
    // -------------------------------------------------------------------------
    // Busca e Cache
    // -------------------------------------------------------------------------

    /// <summary>
    /// Busca uma carta pelo ID único da API TCG.
    /// Aplica a estratégia Cache-First: MongoDB → API externa.
    /// </summary>
    /// <param name="tcgCardId">ID da carta na API TCG (ex: "pokemon-base1-4").</param>
    /// <returns>Dados da carta ou null se não encontrada.</returns>
    Task<CardCache?> GetCardByIdAsync(string tcgCardId);

    /// <summary>
    /// Pesquisa cartas por nome (busca textual).
    /// Prioriza cache; chama API se não houver resultados no MongoDB.
    /// </summary>
    /// <param name="name">Nome ou parte do nome da carta.</param>
    /// <param name="game">Filtro opcional de jogo (ex: "Pokemon", "Magic").</param>
    /// <param name="page">Página para paginação (default: 1).</param>
    /// <param name="pageSize">Itens por página (default: 20, máx: 50).</param>
    /// <returns>Lista paginada de cartas encontradas.</returns>
    Task<PagedResult<CardCache>> SearchCardsByNameAsync(
        string name,
        string? game    = null,
        int    page     = 1,
        int    pageSize = 20,
        string? setId   = null,
        string? rarity  = null
    );

    /// <summary>
    /// Busca cartas de um set/expansão específico.
    /// Exemplo: todas as cartas do set "Base Set" de Pokémon.
    /// </summary>
    Task<IEnumerable<CardCache>> GetCardsBySetAsync(string setCode, string game);

    // -------------------------------------------------------------------------
    // Gerenciamento do Cache
    // -------------------------------------------------------------------------

    /// <summary>
    /// Força a atualização do cache de uma carta específica, mesmo que não esteja expirado.
    /// Útil para quando o Admin suspeita que o preço está desatualizado.
    /// </summary>
    Task<CardCache?> RefreshCardCacheAsync(string tcgCardId);

    /// <summary>
    /// Remove uma carta do cache MongoDB.
    /// Na próxima busca, ela será re-buscada da API.
    /// </summary>
    Task InvalidateCacheAsync(string tcgCardId);

    /// <summary>
    /// Remove do cache todas as cartas cujo TTL expirou.
    /// Pode ser chamado por um Background Service periódico.
    /// </summary>
    Task<int> PurgeExpiredCacheAsync();

    // -------------------------------------------------------------------------
    // Sets / Expansões
    // -------------------------------------------------------------------------

    /// <summary>
    /// Lista todos os sets disponíveis para um jogo específico.
    /// </summary>
    Task<IEnumerable<TcgSetDto>> GetAvailableSetsAsync(string game);
}

// =============================================================================
// ITcgApiClient — Abstração da chamada HTTP bruta à API externa
// Separa a responsabilidade: ITcgService faz orquestração/cache,
// ITcgApiClient faz apenas o transporte HTTP.
// =============================================================================

/// <summary>
/// Abstração do cliente HTTP que comunica com a API TCG externa.
/// Implementação concreta usa IHttpClientFactory.
/// </summary>
public interface ITcgApiClient
{
    /// <summary>Busca dados brutos de uma carta na API externa pelo ID.</summary>
    Task<TcgApiCardResponse?> FetchCardByIdAsync(string cardId);

    /// <summary>Pesquisa cartas por nome na API externa.</summary>
    Task<TcgApiSearchResponse> SearchCardsAsync(string name, string? game, int page, int pageSize);

    /// <summary>Busca todos os sets de um jogo na API externa.</summary>
    Task<IEnumerable<TcgSetDto>> FetchSetsAsync(string game);
}
