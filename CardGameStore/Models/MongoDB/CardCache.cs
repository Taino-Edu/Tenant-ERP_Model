// =============================================================================
// CardCache.cs — Documento de cache de cartas TCG (MongoDB)
// Armazena dados de cartas obtidos da API externa para evitar chamadas repetidas.
// Estratégia: Cache-First → busca no MongoDB primeiro; só chama a API se não existir.
// =============================================================================

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CardGameStore.Models.MongoDB;

/// <summary>
/// Documento MongoDB que representa uma carta de TCG em cache.
/// Mantém os dados brutos da API TCG e metadados de cache.
/// </summary>
public class CardCache
{
    // -------------------------------------------------------------------------
    // Identificação MongoDB
    // -------------------------------------------------------------------------

    /// <summary>ObjectId do MongoDB (gerado automaticamente).</summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? MongoId { get; set; }

    // -------------------------------------------------------------------------
    // Chave de negócio — usada para buscar antes de chamar a API
    // -------------------------------------------------------------------------

    /// <summary>
    /// ID único da carta na API TCG externa.
    /// Exemplo: "tcg_pokemon_pikachu_xy1-001" ou "magic_lightning-bolt_2ed"
    /// Indexado com unique:true no MongoDB para garantir unicidade.
    /// </summary>
    [BsonElement("tcgCardId")]
    public string TcgCardId { get; set; } = string.Empty;

    // -------------------------------------------------------------------------
    // Dados principais da carta
    // -------------------------------------------------------------------------

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Jogo: "Pokemon", "Magic: The Gathering", "Yu-Gi-Oh", etc.</summary>
    [BsonElement("game")]
    public string Game { get; set; } = string.Empty;

    /// <summary>Expansão/Set: "XY Base Set", "Core Set 2021", etc.</summary>
    [BsonElement("setName")]
    public string? SetName { get; set; }

    [BsonElement("setCode")]
    public string? SetCode { get; set; }

    [BsonElement("number")]
    public string? Number { get; set; }

    [BsonElement("rarity")]
    public string? Rarity { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; } // Criatura, Feitiço, Pokémon de Fogo, etc.

    [BsonElement("subtypes")]
    public List<string> Subtypes { get; set; } = new();

    // -------------------------------------------------------------------------
    // Imagens
    // -------------------------------------------------------------------------

    [BsonElement("imageUrlSmall")]
    public string? ImageUrlSmall { get; set; }

    [BsonElement("imageUrlLarge")]
    public string? ImageUrlLarge { get; set; }

    // -------------------------------------------------------------------------
    // Preços de mercado (snapshot da API)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Preços do mercado no momento do cache (em USD, conforme retornado pela API).
    /// Use CardPrices para acessar de forma estruturada.
    /// </summary>
    [BsonElement("marketPrices")]
    public CardPrices? MarketPrices { get; set; }

    // -------------------------------------------------------------------------
    // Dados brutos da API (flexibilidade para APIs diferentes)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Payload JSON original retornado pela API, serializado como BsonDocument.
    /// Permite armazenar campos extras sem precisar alterar o modelo.
    /// </summary>
    [BsonElement("rawApiData")]
    public BsonDocument? RawApiData { get; set; }

    // -------------------------------------------------------------------------
    // Controle de cache
    // -------------------------------------------------------------------------

    /// <summary>Data/hora em que o documento foi cacheado pela primeira vez.</summary>
    [BsonElement("cachedAt")]
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data/hora da última atualização do cache.
    /// Útil para re-sincronizar preços periodicamente.
    /// </summary>
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// TTL do cache. Após esta data, o documento pode ser atualizado.
    /// Default: 7 dias. Preços podem precisar de TTL menor.
    /// </summary>
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    /// <summary>Indica se o cache ainda é válido.</summary>
    [BsonIgnore]
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>Qual API gerou este cache (para multi-game futuramente).</summary>
    [BsonElement("sourceApi")]
    public string SourceApi { get; set; } = "apitcg.com";
}

/// <summary>
/// Preços de mercado estruturados de uma carta.
/// Valores em USD (formato da maioria das APIs TCG).
/// </summary>
public class CardPrices
{
    [BsonElement("low")]
    public decimal? Low { get; set; }

    [BsonElement("mid")]
    public decimal? Mid { get; set; }

    [BsonElement("high")]
    public decimal? High { get; set; }

    [BsonElement("market")]
    public decimal? Market { get; set; }

    [BsonElement("directLow")]
    public decimal? DirectLow { get; set; }
}
