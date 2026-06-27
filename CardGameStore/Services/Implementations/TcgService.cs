// =============================================================================
// TcgService.cs — Implementação do serviço TCG com estratégia Cache-First
//
// PADRÃO: Cache-Aside (Lazy Loading)
//   1. Tenta ler do MongoDB
//   2. Se miss ou expirado → busca da API externa → salva no MongoDB → retorna
//
// Registrado como Singleton no DI (o cliente MongoDB é thread-safe).
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Models.MongoDB;
using CardGameStore.Services.Interfaces;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace CardGameStore.Services.Implementations;

/// <summary>
/// Implementação do <see cref="ITcgService"/> com cache MongoDB (Cache-First).
/// </summary>
public class TcgService : ITcgService
{
    private readonly IMongoCollection<CardCache> _cardCollection;
    private readonly ITcgApiClient               _apiClient;
    private readonly ILogger<TcgService>         _logger;

    // Nome da coleção no MongoDB
    private const string CollectionName = "card_cache";

    public TcgService(
        IMongoDatabase    mongoDatabase,
        ITcgApiClient     apiClient,
        ILogger<TcgService> logger)
    {
        _cardCollection = mongoDatabase.GetCollection<CardCache>(CollectionName);
        _apiClient      = apiClient;
        _logger         = logger;

        // Garante que os índices existam (idempotente — seguro chamar múltiplas vezes)
        EnsureIndexesAsync().GetAwaiter().GetResult();
    }

    // =========================================================================
    // GetCardByIdAsync — Cache-First por ID
    // =========================================================================

    /// <inheritdoc/>
    public async Task<CardCache?> GetCardByIdAsync(string tcgCardId)
    {
        if (string.IsNullOrWhiteSpace(tcgCardId))
            throw new ArgumentException("ID da carta não pode ser vazio.", nameof(tcgCardId));

        // PASSO 1: Tenta o cache MongoDB
        var cached = await _cardCollection
            .Find(c => c.TcgCardId == tcgCardId)
            .FirstOrDefaultAsync();

        if (cached != null && !cached.IsExpired)
        {
            _logger.LogDebug("Cache HIT para carta {CardId}", tcgCardId);
            return cached;
        }

        // PASSO 2: Cache MISS ou expirado — busca na API externa
        _logger.LogInformation("Cache MISS para carta {CardId}. Buscando na API TCG...", tcgCardId);

        var apiResponse = await _apiClient.FetchCardByIdAsync(tcgCardId);
        if (apiResponse == null)
        {
            _logger.LogWarning("Carta {CardId} não encontrada na API TCG.", tcgCardId);
            return null;
        }

        // PASSO 3: Mapeia a resposta da API para o modelo de cache
        var cardToCache = MapApiResponseToCache(apiResponse);

        // PASSO 4: Upsert no MongoDB (insere se não existir, atualiza se existir)
        await UpsertCardCacheAsync(cardToCache);

        return cardToCache;
    }

    // =========================================================================
    // SearchCardsByNameAsync — Busca textual com cache
    // =========================================================================

    /// <inheritdoc/>
    public async Task<PagedResult<CardCache>> SearchCardsByNameAsync(
        string name,
        string? game    = null,
        int    page     = 1,
        int    pageSize = 20)
    {
        pageSize = Math.Min(pageSize, 50); // Limita o tamanho máximo da página
        var skip = (page - 1) * pageSize;

        // Monta filtro flexível
        var filterBuilder = Builders<CardCache>.Filter;
        // Regex.Escape evita ReDoS e regex injection com input do usuário
        var filter = filterBuilder.Regex(c => c.Name, new MongoDB.Bson.BsonRegularExpression(Regex.Escape(name), "i"));

        if (!string.IsNullOrWhiteSpace(game))
            filter &= filterBuilder.Eq(c => c.Game, game);

        // Busca no cache que ainda não expirou
        filter &= filterBuilder.Gt(c => c.ExpiresAt, DateTime.UtcNow);

        var totalCount = await _cardCollection.CountDocumentsAsync(filter);

        if (totalCount == 0)
        {
            // Cache sem resultados → chama a API
            _logger.LogInformation("Nenhum resultado em cache para '{Name}'. Buscando na API...", name);
            var apiResult = await _apiClient.SearchCardsAsync(name, game, page, pageSize);

            // Salva todos os resultados no cache em background
            _ = Task.Run(() => CacheApiSearchResultsAsync(apiResult.Cards));

            return new PagedResult<CardCache>
            {
                Items      = apiResult.Cards.Select(MapApiResponseToCache).ToList(),
                TotalCount = apiResult.TotalCount,
                Page       = page,
                PageSize   = pageSize
            };
        }

        // Retorna do cache
        var items = await _cardCollection
            .Find(filter)
            .Skip(skip)
            .Limit(pageSize)
            .SortBy(c => c.Name)
            .ToListAsync();

        return new PagedResult<CardCache>
        {
            Items      = items,
            TotalCount = (int)totalCount,
            Page       = page,
            PageSize   = pageSize
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<CardCache>> GetCardsBySetAsync(string setCode, string game)
    {
        var filter = Builders<CardCache>.Filter.And(
            Builders<CardCache>.Filter.Eq(c => c.SetCode, setCode),
            Builders<CardCache>.Filter.Eq(c => c.Game, game),
            Builders<CardCache>.Filter.Gt(c => c.ExpiresAt, DateTime.UtcNow)
        );

        var cached = await _cardCollection.Find(filter).ToListAsync();

        if (cached.Any())
            return cached;

        // Cache frio para este set → busca na API
        var apiResult = await _apiClient.SearchCardsAsync($"set:{setCode}", game, 1, 50);
        _ = Task.Run(() => CacheApiSearchResultsAsync(apiResult.Cards));
        return apiResult.Cards.Select(MapApiResponseToCache);
    }

    // =========================================================================
    // Gerenciamento de Cache
    // =========================================================================

    /// <inheritdoc/>
    public async Task<CardCache?> RefreshCardCacheAsync(string tcgCardId)
    {
        _logger.LogInformation("Forçando atualização do cache para carta {CardId}", tcgCardId);
        await InvalidateCacheAsync(tcgCardId);
        return await GetCardByIdAsync(tcgCardId);
    }

    /// <inheritdoc/>
    public async Task InvalidateCacheAsync(string tcgCardId)
    {
        await _cardCollection.DeleteOneAsync(c => c.TcgCardId == tcgCardId);
        _logger.LogInformation("Cache invalidado para carta {CardId}", tcgCardId);
    }

    /// <inheritdoc/>
    public async Task<int> PurgeExpiredCacheAsync()
    {
        var result = await _cardCollection.DeleteManyAsync(
            c => c.ExpiresAt < DateTime.UtcNow
        );

        _logger.LogInformation("Purga de cache: {Count} documentos expirados removidos", result.DeletedCount);
        return (int)result.DeletedCount;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TcgSetDto>> GetAvailableSetsAsync(string game)
    {
        return await _apiClient.FetchSetsAsync(game);
    }

    // =========================================================================
    // HELPERS PRIVADOS
    // =========================================================================

    /// <summary>Cria índices MongoDB necessários para performance e unicidade.</summary>
    private async Task EnsureIndexesAsync()
    {
        // Índice único no TcgCardId (evita duplicatas no cache)
        var uniqueIndex = new CreateIndexModel<CardCache>(
            Builders<CardCache>.IndexKeys.Ascending(c => c.TcgCardId),
            new CreateIndexOptions { Unique = true, Name = "ix_tcgCardId_unique" }
        );

        // Índice textual para buscas por nome
        var textIndex = new CreateIndexModel<CardCache>(
            Builders<CardCache>.IndexKeys.Text(c => c.Name),
            new CreateIndexOptions { Name = "ix_name_text" }
        );

        // TTL Index: MongoDB remove automaticamente documentos expirados
        // (alternativa ao PurgeExpiredCacheAsync, usando recurso nativo do MongoDB)
        var ttlIndex = new CreateIndexModel<CardCache>(
            Builders<CardCache>.IndexKeys.Ascending(c => c.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "ix_expiresAt_ttl" }
        );

        await _cardCollection.Indexes.CreateManyAsync(new[] { uniqueIndex, textIndex, ttlIndex });
    }

    /// <summary>Insere ou atualiza um documento de carta no MongoDB.</summary>
    private async Task UpsertCardCacheAsync(CardCache card)
    {
        card.UpdatedAt = DateTime.UtcNow;

        await _cardCollection.ReplaceOneAsync(
            filter:      c => c.TcgCardId == card.TcgCardId,
            replacement: card,
            options:     new ReplaceOptions { IsUpsert = true }
        );
    }

    /// <summary>Cacheia múltiplos resultados de busca em background.</summary>
    private async Task CacheApiSearchResultsAsync(IEnumerable<TcgApiCardResponse> responses)
    {
        foreach (var response in responses)
        {
            try
            {
                var card = MapApiResponseToCache(response);
                await UpsertCardCacheAsync(card);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao cachear carta {CardId}", response.Id);
            }
        }
    }

    /// <summary>
    /// Mapeia o DTO da API externa para o modelo de cache MongoDB.
    /// Adapte este método conforme o schema real da apitcg.com.
    /// </summary>
    private static CardCache MapApiResponseToCache(TcgApiCardResponse response)
    {
        static CardPrices? ToPrices(TcgCardPricesApi? p) => p == null ? null : new CardPrices
        {
            Low = p.Low, Mid = p.Mid, High = p.High, Market = p.Market, DirectLow = p.DirectLow
        };

        return new CardCache
        {
            TcgCardId            = response.Id,
            Name                 = response.Name,
            Game                 = response.Game,
            SetName              = response.SetName,
            SetCode              = response.SetCode,
            Number               = response.Number,
            Rarity               = response.Rarity,
            Type                 = response.Type,
            Subtypes             = response.Subtypes ?? new List<string>(),
            Types                = response.Types    ?? new List<string>(),
            Hp                   = response.Hp,
            Artist               = response.Artist,
            FlavorText           = response.FlavorText,
            RegulationMark       = response.RegulationMark,
            Attacks              = response.Attacks?.Select(a => new CardAttackCache
            {
                Name                = a.Name,
                Cost                = a.Cost,
                ConvertedEnergyCost = a.ConvertedEnergyCost,
                Damage              = a.Damage,
                Text                = a.Text,
            }).ToList() ?? new(),
            Weaknesses           = response.Weaknesses?.Select(w => new CardWeaknessCache { Type = w.Type, Value = w.Value }).ToList() ?? new(),
            Resistances          = response.Resistances?.Select(r => new CardWeaknessCache { Type = r.Type, Value = r.Value }).ToList() ?? new(),
            RetreatCost          = response.RetreatCost ?? new(),
            ConvertedRetreatCost = response.ConvertedRetreatCost,
            ImageUrlSmall        = response.Images?.Small,
            ImageUrlLarge        = response.Images?.Large,
            AllPrices            = response.AllPrices == null ? null : new CardAllPricesCache
            {
                Normal               = ToPrices(response.AllPrices.Normal),
                Holofoil             = ToPrices(response.AllPrices.Holofoil),
                ReverseHolofoil      = ToPrices(response.AllPrices.ReverseHolofoil),
                FirstEditionNormal   = ToPrices(response.AllPrices.FirstEditionNormal),
                FirstEditionHolofoil = ToPrices(response.AllPrices.FirstEditionHolofoil),
            },
            MarketPrices         = ToPrices(response.Prices),
            CachedAt             = DateTime.UtcNow,
            UpdatedAt            = DateTime.UtcNow,
            ExpiresAt            = DateTime.UtcNow.AddDays(7),
            SourceApi            = response.Game == "Pokemon" ? "pokemontcg.io" : "various",
        };
    }
}
