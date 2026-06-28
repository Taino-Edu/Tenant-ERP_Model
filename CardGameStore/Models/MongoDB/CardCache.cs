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

    [BsonElement("setSeries")]
    public string? SetSeries { get; set; }

    [BsonElement("setPtcgoCode")]
    public string? SetPtcgoCode { get; set; }

    [BsonElement("setReleaseDate")]
    public string? SetReleaseDate { get; set; }

    [BsonElement("number")]
    public string? Number { get; set; }

    [BsonElement("rarity")]
    public string? Rarity { get; set; }

    /// <summary>Supertype: Pokémon, Trainer, Energy…</summary>
    [BsonElement("type")]
    public string? Type { get; set; }

    /// <summary>Subtypes: Basic, Stage 1, Item…</summary>
    [BsonElement("subtypes")]
    public List<string> Subtypes { get; set; } = new();

    /// <summary>Tipos de energia: Fire, Water…</summary>
    [BsonElement("types")]
    public List<string> Types { get; set; } = new();

    [BsonElement("hp")]
    public string? Hp { get; set; }

    [BsonElement("artist")]
    public string? Artist { get; set; }

    [BsonElement("flavorText")]
    public string? FlavorText { get; set; }

    [BsonElement("regulationMark")]
    public string? RegulationMark { get; set; }

    [BsonElement("evolvesFrom")]
    public string? EvolvesFrom { get; set; }

    [BsonElement("evolvesTo")]
    public List<string> EvolvesTo { get; set; } = new();

    [BsonElement("nationalPokedexNumbers")]
    public List<int> NationalPokedexNumbers { get; set; } = new();

    [BsonElement("legalities")]
    public Dictionary<string, string> Legalities { get; set; } = new();

    [BsonElement("attacks")]
    public List<CardAttackCache> Attacks { get; set; } = new();

    [BsonElement("weaknesses")]
    public List<CardWeaknessCache> Weaknesses { get; set; } = new();

    [BsonElement("resistances")]
    public List<CardWeaknessCache> Resistances { get; set; } = new();

    [BsonElement("retreatCost")]
    public List<string> RetreatCost { get; set; } = new();

    [BsonElement("convertedRetreatCost")]
    public int? ConvertedRetreatCost { get; set; }

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

    /// <summary>Preços TCGPlayer por variação (normal, holofoil, reverseHolofoil…) em USD.</summary>
    [BsonElement("allPrices")]
    public CardAllPricesCache? AllPrices { get; set; }

    /// <summary>Market price da variação principal (mantido para compatibilidade).</summary>
    [BsonElement("marketPrices")]
    public CardPrices? MarketPrices { get; set; }

    /// <summary>Preços CardMarket (mercado europeu, em EUR).</summary>
    [BsonElement("cardMarket")]
    public CardMarketCache? CardMarket { get; set; }

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

public class CardAllPricesCache
{
    [BsonElement("normal")]
    public CardPrices? Normal { get; set; }

    [BsonElement("holofoil")]
    public CardPrices? Holofoil { get; set; }

    [BsonElement("reverseHolofoil")]
    public CardPrices? ReverseHolofoil { get; set; }

    [BsonElement("firstEditionNormal")]
    public CardPrices? FirstEditionNormal { get; set; }

    [BsonElement("firstEditionHolofoil")]
    public CardPrices? FirstEditionHolofoil { get; set; }

    [BsonElement("unlimitedNormal")]
    public CardPrices? UnlimitedNormal { get; set; }

    [BsonElement("unlimitedHolofoil")]
    public CardPrices? UnlimitedHolofoil { get; set; }
}

public class CardMarketCache
{
    [BsonElement("averageSellPrice")]   public decimal? AverageSellPrice  { get; set; }
    [BsonElement("lowPrice")]           public decimal? LowPrice          { get; set; }
    [BsonElement("trendPrice")]         public decimal? TrendPrice        { get; set; }
    [BsonElement("reverseHoloSell")]    public decimal? ReverseHoloSell   { get; set; }
    [BsonElement("reverseHoloLow")]     public decimal? ReverseHoloLow    { get; set; }
    [BsonElement("reverseHoloTrend")]   public decimal? ReverseHoloTrend  { get; set; }
    [BsonElement("lowPriceExPlus")]     public decimal? LowPriceExPlus    { get; set; }
    [BsonElement("avg1")]               public decimal? Avg1              { get; set; }
    [BsonElement("avg7")]               public decimal? Avg7              { get; set; }
    [BsonElement("avg30")]              public decimal? Avg30             { get; set; }
    [BsonElement("reverseHoloAvg1")]    public decimal? ReverseHoloAvg1   { get; set; }
    [BsonElement("reverseHoloAvg7")]    public decimal? ReverseHoloAvg7   { get; set; }
    [BsonElement("reverseHoloAvg30")]   public decimal? ReverseHoloAvg30  { get; set; }
    [BsonElement("url")]                public string?  Url               { get; set; }
    [BsonElement("updatedAt")]          public string?  UpdatedAt         { get; set; }
}

public class CardAttackCache
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("cost")]
    public List<string> Cost { get; set; } = new();

    [BsonElement("convertedEnergyCost")]
    public int ConvertedEnergyCost { get; set; }

    [BsonElement("damage")]
    public string? Damage { get; set; }

    [BsonElement("text")]
    public string? Text { get; set; }
}

public class CardWeaknessCache
{
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Preços de mercado em USD.</summary>
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
