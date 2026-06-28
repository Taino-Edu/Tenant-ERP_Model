// =============================================================================
// TcgApiClient.cs — Cliente HTTP multi-provider para APIs TCG públicas
//
// Roteamento por jogo:
//   Pokemon      → pokemontcg.io (EN, preços) + TCGdex (PT, fan-out paralelo)
//   MTG / Magic  → https://api.scryfall.com/               (gratuita, sem auth)
//   Yu-Gi-Oh!    → https://db.ygoprodeck.com/api/v7/       (gratuita, sem auth)
//   LoL Riftbound→ Riftcodex (gratuita) + Scrydex (opcional, preços)
//   Outros       → retorna vazio (graceful degradation)
//
// Para aumentar rate limits do Pokemon TCG API, defina:
//   appsettings.json: "TcgSettings": { "PokemonApiKey": "sua-chave" }
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CardGameStore.Services.Implementations;

public class TcgApiClient : ITcgApiClient
{
    private readonly IHttpClientFactory          _factory;
    private readonly IConfiguration              _config;
    private readonly ILogger<TcgApiClient>       _logger;
    private readonly IMemoryCache                _cache;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
    };

    public TcgApiClient(IHttpClientFactory factory, IConfiguration config, ILogger<TcgApiClient> logger, IMemoryCache cache)
    {
        _cache = cache;
        _factory = factory;
        _config  = config;
        _logger  = logger;
    }

    // =========================================================================
    // ITcgApiClient
    // =========================================================================

    public async Task<TcgApiCardResponse?> FetchCardByIdAsync(string cardId)
    {
        // cardId format: "pokemon:{id}" or "mtg:{scryfallId}"
        if (cardId.StartsWith("pokemon:", StringComparison.OrdinalIgnoreCase))
            return await FetchPokemonCardByIdAsync(cardId["pokemon:".Length..]);

        if (cardId.StartsWith("mtg:", StringComparison.OrdinalIgnoreCase))
            return await FetchScryfallCardByIdAsync(cardId["mtg:".Length..]);

        return null;
    }

    public async Task<TcgApiSearchResponse> SearchCardsAsync(
        string  name,
        string? game           = null,
        int     page           = 1,
        int     pageSize       = 20,
        string? setCode        = null,
        string? rarity         = null,
        string? cardType       = null,
        string? artist         = null,
        string? supertype      = null,
        string? subtype        = null,
        string? energyType     = null,
        string? regulationMark = null,
        string? legality       = null,
        string? evolvesFrom    = null,
        string? setSeries      = null,
        string? ptcgoCode      = null,
        string? releaseDateFrom = null,
        string? releaseDateTo  = null,
        int?    pokedexNumber  = null,
        int?    hpMin          = null,
        int?    hpMax          = null)
    {
        var normalized = game?.ToLowerInvariant() ?? string.Empty;

        if (normalized.Contains("pokemon") || normalized.Contains("pokémon"))
            return await SearchPokemonCardsAsync(name, page, pageSize,
                setCode, rarity, artist, supertype, subtype, energyType,
                regulationMark, legality, evolvesFrom, setSeries, ptcgoCode,
                releaseDateFrom, releaseDateTo, pokedexNumber, hpMin, hpMax);

        if (normalized.Contains("mtg") || normalized.Contains("magic"))
            return await SearchScryfallCardsAsync(name, page, pageSize, setCode, rarity, cardType);

        if (normalized.Contains("yu-gi-oh") || normalized.Contains("yugioh") || normalized.Contains("yu gi oh"))
            return await SearchYugiohCardsAsync(name, page, pageSize, cardType ?? rarity);

        if (normalized.Contains("riftbound") || normalized.Contains("lol riftbound"))
            return await SearchRiftboundCardsAsync(name, page, pageSize);

        if (normalized.Contains("one piece"))
            return await SearchOnePieceCardsAsync(name, page, pageSize, setCode, rarity, cardType);

        _logger.LogDebug("Jogo '{Game}' sem provider configurado — retornando vazio.", game);
        return new TcgApiSearchResponse { ErrorMessage = "no_api" };
    }

    public async Task<IEnumerable<TcgSetDto>> FetchSetsAsync(string game)
    {
        var normalized = game.ToLowerInvariant();

        if (normalized.Contains("pokemon") || normalized.Contains("pokémon"))
            return await FetchPokemonSetsAsync();

        if (normalized.Contains("mtg") || normalized.Contains("magic"))
            return await FetchScryfallSetsAsync();

        if (normalized.Contains("riftbound") || normalized.Contains("lol"))
            return await FetchRiftboundSetsAsync();

        if (normalized.Contains("one piece"))
            return await FetchOnePieceSetsAsync();

        return Enumerable.Empty<TcgSetDto>();
    }

    // =========================================================================
    // Pokemon TCG API — https://api.pokemontcg.io/v2/
    // =========================================================================

    private HttpClient PokemonClient()
    {
        var client = _factory.CreateClient("PokemonTcgApi");
        var apiKey = _config["TcgSettings:PokemonApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey);
        return client;
    }

    private HttpClient TcgDexClient() => _factory.CreateClient("TcgDexApi");

    private async Task<TcgApiCardResponse?> FetchPokemonCardByIdAsync(string id)
    {
        try
        {
            var response = await PokemonClient().GetAsync($"/v2/cards/{Uri.EscapeDataString(id)}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("data", out var data)
                ? MapPokemonCard(data)
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar carta Pokemon {Id}", id);
            return null;
        }
    }

    private async Task<TcgApiSearchResponse> SearchPokemonCardsAsync(
        string  name,
        int     page,
        int     pageSize,
        string? setCode        = null,
        string? rarity         = null,
        string? artist         = null,
        string? supertype      = null,
        string? subtype        = null,
        string? energyType     = null,
        string? regulationMark = null,
        string? legality       = null,
        string? evolvesFrom    = null,
        string? setSeries      = null,
        string? ptcgoCode      = null,
        string? releaseDateFrom = null,
        string? releaseDateTo  = null,
        int?    pokedexNumber  = null,
        int?    hpMin          = null,
        int?    hpMax          = null)
    {
        try
        {
            // Query estruturada (contém ':') → passa direto para a API
            var isStructured = name.Contains(':');
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(name))
            {
                if (isStructured)
                {
                    parts.Add(name);
                }
                else
                {
                    // Lucene (pokemontcg.io) não suporta wildcard com espaços —
                    // usa só a primeira palavra; TCGdex recebe o nome completo.
                    var pokeNameToken = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                    parts.Add($"name:*{pokeNameToken}*");
                }
            }

            if (!isStructured)
            {
                if (!string.IsNullOrWhiteSpace(setCode))
                    parts.Add(setCode.Length <= 6
                        ? $"set.ptcgoCode:{setCode.ToUpper()}"
                        : $"set.id:{setCode.ToLower()}");
                if (!string.IsNullOrWhiteSpace(rarity))
                    parts.Add($"rarity:\"{rarity}\"");
                if (!string.IsNullOrWhiteSpace(artist))
                    parts.Add($"artist:*{artist}*");
                if (!string.IsNullOrWhiteSpace(supertype))
                    parts.Add($"supertype:{supertype}");
                if (!string.IsNullOrWhiteSpace(subtype))
                    parts.Add($"subtypes:{subtype}");
                if (!string.IsNullOrWhiteSpace(energyType))
                    parts.Add($"types:{energyType}");
                if (!string.IsNullOrWhiteSpace(regulationMark))
                    parts.Add($"regulationMark:{regulationMark.ToUpper()}");
                if (!string.IsNullOrWhiteSpace(legality))
                    parts.Add($"legalities.{legality.ToLower()}:legal");
                if (!string.IsNullOrWhiteSpace(evolvesFrom))
                    parts.Add($"evolvesFrom:*{evolvesFrom}*");
                if (!string.IsNullOrWhiteSpace(setSeries))
                    parts.Add($"set.series:\"{setSeries}\"");
                if (!string.IsNullOrWhiteSpace(ptcgoCode))
                    parts.Add($"set.ptcgoCode:{ptcgoCode.ToUpper()}");
                if (!string.IsNullOrWhiteSpace(releaseDateFrom) || !string.IsNullOrWhiteSpace(releaseDateTo))
                    parts.Add($"set.releaseDate:[{releaseDateFrom ?? "*"} TO {releaseDateTo ?? "*"}]");
                if (pokedexNumber.HasValue)
                    parts.Add($"nationalPokedexNumbers:{pokedexNumber}");
                if (hpMin.HasValue || hpMax.HasValue)
                    parts.Add($"hp:[{hpMin?.ToString() ?? "*"} TO {hpMax?.ToString() ?? "*"}]");
            }

            // Se não houver nenhum filtro, busca a carta com maior nome (fallback)
            var baseQ = parts.Count > 0 ? string.Join(" ", parts) : "*";
            var q   = Uri.EscapeDataString(baseQ);
            var url = $"/v2/cards?q={q}&page={page}&pageSize={pageSize}&orderBy=name";

            // pokemontcg.io é a fonte primária — falha não bloqueia o TCGdex
            var pokeResponse = await PokemonClient().GetAsync(url);

            var seen      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pokeCards = new List<TcgApiCardResponse>();
            var totalCount = 0;

            if (pokeResponse.IsSuccessStatusCode)
            {
                var json = await pokeResponse.Content.ReadAsStringAsync();
                var doc  = JsonDocument.Parse(json);
                var root = doc.RootElement;

                pokeCards = root.TryGetProperty("data", out var dataArr)
                    ? dataArr.EnumerateArray()
                             .Select(MapPokemonCard)
                             .Where(c => !string.IsNullOrEmpty(c.Id) && seen.Add(c.Id))
                             .ToList()
                    : new List<TcgApiCardResponse>();

                totalCount = root.TryGetProperty("totalCount", out var tc) ? tc.GetInt32() : pokeCards.Count;
            }
            else
            {
                _logger.LogWarning("pokemontcg.io retornou {Status} para query '{Q}' — prosseguindo com TCGdex.",
                    (int)pokeResponse.StatusCode, baseQ);
            }

            // TCGdex: complementa com nomes PT — timeout de 3s para não atrasar a busca
            if (page == 1 && !string.IsNullOrWhiteSpace(name) && !isStructured)
            {
                using var cts          = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var tcgdexTask         = SearchTcgDexAsync(name, pageSize, cts.Token);
                IList<TcgApiCardResponse> tcgdexCards;
                try   { tcgdexCards = await tcgdexTask; }
                catch { tcgdexCards = Array.Empty<TcgApiCardResponse>(); }

                var pokeById = pokeCards.ToDictionary(
                    c => c.Id.Replace("pokemon:", "", StringComparison.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var tdCard in tcgdexCards)
                {
                    var baseId = tdCard.Id.Replace("pokemon:", "", StringComparison.OrdinalIgnoreCase);
                    if (pokeById.TryGetValue(baseId, out var existing))
                    {
                        if (existing.Images?.Small == null && tdCard.Images?.Small != null)
                            existing.Images = tdCard.Images;
                    }
                    else if (seen.Add(tdCard.Id))
                    {
                        pokeCards.Add(tdCard);
                    }
                }
            }

            // Se pokemontcg.io não retornou nada, usa a contagem real do TCGdex
            if (totalCount == 0 && pokeCards.Count > 0)
                totalCount = pokeCards.Count;

            return new TcgApiSearchResponse
            {
                Cards      = pokeCards,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pesquisar cartas Pokemon '{Name}'", name);
            return new TcgApiSearchResponse();
        }
    }

    // ── TCGdex — busca multilíngue PT (https://api.tcgdex.net/v2/pt) ──────────
    private async Task<IList<TcgApiCardResponse>> SearchTcgDexAsync(string name, int limit, CancellationToken ct = default)
    {
        try
        {
            var q        = Uri.EscapeDataString(name);
            var response = await TcgDexClient().GetAsync($"/v2/pt/cards?name={q}", ct);
            if (!response.IsSuccessStatusCode) return Array.Empty<TcgApiCardResponse>();

            var json = await response.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<TcgApiCardResponse>();

            return doc.RootElement.EnumerateArray()
                      .Take(limit)
                      .Select(MapTcgDexCard)
                      .Where(c => !string.IsNullOrEmpty(c.Id))
                      .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TCGdex search falhou para '{Name}'", name);
            return Array.Empty<TcgApiCardResponse>();
        }
    }

    private static TcgApiCardResponse MapTcgDexCard(JsonElement c)
    {
        var id      = c.TryGetProperty("id",      out var idEl)  ? idEl.GetString()  ?? "" : "";
        var name    = c.TryGetProperty("name",    out var nmEl)  ? nmEl.GetString()  ?? "" : "";
        var imgBase = c.TryGetProperty("image",   out var imgEl) ? imgEl.GetString()      : null;
        var localId = c.TryGetProperty("localId", out var locEl) ? locEl.GetString()      : null;

        return new TcgApiCardResponse
        {
            Id     = $"pokemon:{id}",
            Name   = name,
            Game   = "Pokemon",
            Number = localId,
            Images = new TcgCardImages
            {
                Small = imgBase != null ? $"{imgBase}/low.webp"  : null,
                Large = imgBase != null ? $"{imgBase}/high.webp" : null,
            },
        };
    }

    private async Task<IEnumerable<TcgSetDto>> FetchPokemonSetsAsync()
    {
        try
        {
            var response = await PokemonClient().GetAsync("/v2/sets?orderBy=releaseDate");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return Enumerable.Empty<TcgSetDto>();

            return data.EnumerateArray().Select(s => new TcgSetDto
            {
                Code        = s.TryGetProperty("id", out var id)          ? id.GetString()!        : string.Empty,
                Name        = s.TryGetProperty("name", out var nm)        ? nm.GetString()!        : string.Empty,
                Game        = "Pokemon",
                Series      = s.TryGetProperty("series", out var sr)      ? sr.GetString()         : null,
                LogoUrl     = s.TryGetProperty("images", out var img) &&
                              img.TryGetProperty("logo", out var logo)    ? logo.GetString()       : null,
                TotalCards  = s.TryGetProperty("total", out var tot)      ? tot.GetInt32()         : 0,
                ReleaseDate = s.TryGetProperty("releaseDate", out var rd) &&
                              DateTime.TryParse(rd.GetString(), out var dt) ? dt                   : null,
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar sets Pokemon");
            return Enumerable.Empty<TcgSetDto>();
        }
    }

    private static TcgApiCardResponse MapPokemonCard(JsonElement c)
    {
        c.TryGetProperty("tcgplayer",  out var tcgp);
        c.TryGetProperty("cardmarket", out var cm);
        c.TryGetProperty("images",     out var imgs);
        c.TryGetProperty("set",        out var set);
        c.TryGetProperty("legalities", out var legEl);

        var allPrices = ExtractAllPokemonPrices(tcgp);
        var mainPrice = allPrices?.Holofoil ?? allPrices?.Normal ?? allPrices?.FirstEditionHolofoil
                     ?? allPrices?.FirstEditionNormal ?? allPrices?.UnlimitedHolofoil
                     ?? allPrices?.ReverseHolofoil;

        // CardMarket prices
        CardMarketPricesApi? cardMarket = null;
        if (cm.ValueKind == JsonValueKind.Object && cm.TryGetProperty("prices", out var cmp))
        {
            cardMarket = new CardMarketPricesApi
            {
                AverageSellPrice = GetDecimal(cmp, "averageSellPrice"),
                LowPrice         = GetDecimal(cmp, "lowPrice"),
                TrendPrice       = GetDecimal(cmp, "trendPrice"),
                ReverseHoloSell  = GetDecimal(cmp, "reverseHoloSell"),
                ReverseHoloLow   = GetDecimal(cmp, "reverseHoloLow"),
                ReverseHoloTrend = GetDecimal(cmp, "reverseHoloTrend"),
                LowPriceExPlus   = GetDecimal(cmp, "lowPriceExPlus"),
                Avg1             = GetDecimal(cmp, "avg1"),
                Avg7             = GetDecimal(cmp, "avg7"),
                Avg30            = GetDecimal(cmp, "avg30"),
                ReverseHoloAvg1  = GetDecimal(cmp, "reverseHoloAvg1"),
                ReverseHoloAvg7  = GetDecimal(cmp, "reverseHoloAvg7"),
                ReverseHoloAvg30 = GetDecimal(cmp, "reverseHoloAvg30"),
                Url       = cm.TryGetProperty("url",       out var cu) ? cu.GetString() : null,
                UpdatedAt = cm.TryGetProperty("updatedAt", out var cua) ? cua.GetString() : null,
            };
        }

        // Legalities dict
        Dictionary<string, string>? legalities = null;
        if (legEl.ValueKind == JsonValueKind.Object)
        {
            legalities = new Dictionary<string, string>();
            foreach (var prop in legEl.EnumerateObject())
                legalities[prop.Name] = prop.Value.GetString() ?? "";
        }

        // Pokédex numbers
        List<int>? pokedex = null;
        if (c.TryGetProperty("nationalPokedexNumbers", out var pdx) && pdx.ValueKind == JsonValueKind.Array)
            pokedex = pdx.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToList();

        return new TcgApiCardResponse
        {
            Id         = c.TryGetProperty("id", out var id)              ? $"pokemon:{id.GetString()}" : string.Empty,
            Name       = c.TryGetProperty("name", out var nm)            ? nm.GetString()!              : string.Empty,
            Game       = "Pokemon",
            SetName    = set.ValueKind != JsonValueKind.Undefined &&
                         set.TryGetProperty("name", out var sn)          ? sn.GetString()               : null,
            SetCode    = set.ValueKind != JsonValueKind.Undefined &&
                         set.TryGetProperty("id", out var sc)            ? sc.GetString()               : null,
            SetSeries  = set.ValueKind != JsonValueKind.Undefined &&
                         set.TryGetProperty("series", out var ss)        ? ss.GetString()               : null,
            SetPtcgoCode = set.ValueKind != JsonValueKind.Undefined &&
                           set.TryGetProperty("ptcgoCode", out var pc)   ? pc.GetString()               : null,
            SetReleaseDate = set.ValueKind != JsonValueKind.Undefined &&
                             set.TryGetProperty("releaseDate", out var rd) ? rd.GetString()             : null,
            Number     = c.TryGetProperty("number", out var num)         ? num.GetString()              : null,
            Rarity     = c.TryGetProperty("rarity", out var rar)         ? rar.GetString()              : null,
            Type       = c.TryGetProperty("supertype", out var st)       ? st.GetString()               : null,
            Subtypes   = JsonArrayToList(c, "subtypes"),
            Types      = JsonArrayToList(c, "types"),
            Hp         = c.TryGetProperty("hp", out var hp)              ? hp.GetString()               : null,
            Artist     = c.TryGetProperty("artist", out var art)         ? art.GetString()              : null,
            FlavorText = c.TryGetProperty("flavorText", out var ft)      ? ft.GetString()               : null,
            RegulationMark = c.TryGetProperty("regulationMark", out var rm) ? rm.GetString()            : null,
            EvolvesFrom = c.TryGetProperty("evolvesFrom", out var ef)    ? ef.GetString()               : null,
            EvolvesTo   = JsonArrayToList(c, "evolvesTo"),
            NationalPokedexNumbers = pokedex,
            Legalities  = legalities,
            Attacks     = ExtractPokemonAttacks(c),
            Weaknesses  = ExtractPokemonWeakResist(c, "weaknesses"),
            Resistances = ExtractPokemonWeakResist(c, "resistances"),
            RetreatCost = JsonArrayToList(c, "retreatCost"),
            ConvertedRetreatCost = c.TryGetProperty("convertedRetreatCost", out var crc) &&
                                   crc.ValueKind == JsonValueKind.Number ? crc.GetInt32() : null,
            Images = new TcgCardImages
            {
                Small = imgs.ValueKind != JsonValueKind.Undefined &&
                        imgs.TryGetProperty("small", out var sm) ? sm.GetString() : null,
                Large = imgs.ValueKind != JsonValueKind.Undefined &&
                        imgs.TryGetProperty("large", out var lg) ? lg.GetString() : null,
            },
            AllPrices  = allPrices,
            CardMarket = cardMarket,
            Prices     = mainPrice,
        };
    }

    private static List<string>? JsonArrayToList(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;
        return arr.EnumerateArray().Select(s => s.GetString() ?? "").Where(s => s != "").ToList();
    }

    private static List<TcgCardAttack>? ExtractPokemonAttacks(JsonElement c)
    {
        if (!c.TryGetProperty("attacks", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        return arr.EnumerateArray().Select(a => new TcgCardAttack
        {
            Name                = a.TryGetProperty("name",  out var n) ? n.GetString()! : string.Empty,
            Cost                = JsonArrayToList(a, "cost") ?? new(),
            ConvertedEnergyCost = a.TryGetProperty("convertedEnergyCost", out var ec) &&
                                  ec.ValueKind == JsonValueKind.Number ? ec.GetInt32() : 0,
            Damage              = a.TryGetProperty("damage", out var d) ? d.GetString() : null,
            Text                = a.TryGetProperty("text",   out var t) ? t.GetString() : null,
        }).ToList();
    }

    private static List<TcgCardWeakness>? ExtractPokemonWeakResist(JsonElement c, string prop)
    {
        if (!c.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        return arr.EnumerateArray().Select(w => new TcgCardWeakness
        {
            Type  = w.TryGetProperty("type",  out var t) ? t.GetString()! : string.Empty,
            Value = w.TryGetProperty("value", out var v) ? v.GetString()! : string.Empty,
        }).ToList();
    }

    private static TcgCardAllPrices? ExtractAllPokemonPrices(JsonElement tcgp)
    {
        if (tcgp.ValueKind == JsonValueKind.Undefined) return null;
        if (!tcgp.TryGetProperty("prices", out var pricesEl)) return null;

        static TcgCardPricesApi? ParseVariant(JsonElement prices, string key)
        {
            if (!prices.TryGetProperty(key, out var v)) return null;
            return new TcgCardPricesApi
            {
                Low       = GetDecimal(v, "low"),
                Mid       = GetDecimal(v, "mid"),
                High      = GetDecimal(v, "high"),
                Market    = GetDecimal(v, "market"),
                DirectLow = GetDecimal(v, "directLow"),
            };
        }

        var result = new TcgCardAllPrices
        {
            Normal               = ParseVariant(pricesEl, "normal"),
            Holofoil             = ParseVariant(pricesEl, "holofoil"),
            ReverseHolofoil      = ParseVariant(pricesEl, "reverseHolofoil"),
            FirstEditionNormal   = ParseVariant(pricesEl, "1stEditionNormal"),
            FirstEditionHolofoil = ParseVariant(pricesEl, "1stEditionHolofoil"),
            UnlimitedNormal      = ParseVariant(pricesEl, "unlimitedNormal"),
            UnlimitedHolofoil    = ParseVariant(pricesEl, "unlimitedHolofoil"),
        };

        return (result.Normal == null && result.Holofoil == null && result.ReverseHolofoil == null
             && result.FirstEditionNormal == null && result.UnlimitedHolofoil == null) ? null : result;
    }

    private static decimal? GetDecimal(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

    // =========================================================================
    // Scryfall API (MTG) — https://api.scryfall.com/
    // =========================================================================

    private HttpClient ScryfallClient() => _factory.CreateClient("ScryfallApi");

    private async Task<TcgApiCardResponse?> FetchScryfallCardByIdAsync(string id)
    {
        try
        {
            await Task.Delay(100); // Scryfall pede ≤10 req/s entre chamadas
            var response = await ScryfallClient().GetAsync($"/cards/{Uri.EscapeDataString(id)}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(json);
            return MapScryfallCard(doc.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar carta MTG {Id}", id);
            return null;
        }
    }

    private async Task<TcgApiSearchResponse> SearchScryfallCardsAsync(
        string name, int page, int pageSize,
        string? setCode = null, string? rarity = null, string? cardType = null)
    {
        try
        {
            await Task.Delay(100);
            // Scryfall usa sintaxe própria: r:rare s:eld t:creature
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(name))
            {
                if (name.Contains(':'))
                    // Já é query estruturada (ex: s:mh3 cn:232 ou name:/dri/)
                    parts.Add(name);
                else if (!name.Contains(' ') && name.Length <= 5)
                    // Busca curta sem espaço → regex substring (name:/dri/ encontra Drizzle, Drannith...)
                    parts.Add($"name:/{Uri.EscapeDataString(name)}/");
                else
                    // Nome completo ou parcial com espaço → fuzzy match do Scryfall (sem aspas)
                    parts.Add(name);
            }
            if (!string.IsNullOrWhiteSpace(setCode))
                parts.Add($"s:{setCode}");
            if (!string.IsNullOrWhiteSpace(rarity))
                parts.Add($"r:{MapScryfallRarity(rarity)}");
            if (!string.IsNullOrWhiteSpace(cardType))
                parts.Add($"t:{Uri.EscapeDataString(cardType)}");
            var q   = Uri.EscapeDataString(string.Join(" ", parts));
            var url = $"/cards/search?q={q}&page={page}&order=name";
            var response = await ScryfallClient().GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new TcgApiSearchResponse(); // sem resultados

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var cards = root.TryGetProperty("data", out var dataArr)
                ? dataArr.EnumerateArray().Take(pageSize).Select(MapScryfallCard).ToList()
                : new List<TcgApiCardResponse>();

            return new TcgApiSearchResponse
            {
                Cards      = cards,
                TotalCount = root.TryGetProperty("total_cards", out var tc) ? tc.GetInt32() : cards.Count,
                Page       = page,
                PageSize   = pageSize,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pesquisar cartas MTG '{Name}'", name);
            return new TcgApiSearchResponse();
        }
    }

    private async Task<IEnumerable<TcgSetDto>> FetchScryfallSetsAsync()
    {
        try
        {
            await Task.Delay(100); // Scryfall pede ≤10 req/s entre chamadas
            var response = await ScryfallClient().GetAsync("/sets");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return Enumerable.Empty<TcgSetDto>();

            return data.EnumerateArray()
                .Where(s => s.TryGetProperty("set_type", out var t) &&
                            t.GetString() is "expansion" or "core" or "masters")
                .Select(s => new TcgSetDto
                {
                    Code       = s.TryGetProperty("code", out var c)           ? c.GetString()!       : string.Empty,
                    Name       = s.TryGetProperty("name", out var n)           ? n.GetString()!       : string.Empty,
                    Game       = "MTG",
                    TotalCards = s.TryGetProperty("card_count", out var cc)    ? cc.GetInt32()        : 0,
                    LogoUrl    = s.TryGetProperty("icon_svg_uri", out var ico) ? ico.GetString()      : null,
                    ReleaseDate = s.TryGetProperty("released_at", out var rd) &&
                                  DateTime.TryParse(rd.GetString(), out var dt) ? dt                  : null,
                }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar sets MTG");
            return Enumerable.Empty<TcgSetDto>();
        }
    }

    // Mapeia label amigável → valor que Scryfall aceita no filtro r:
    private static string MapScryfallRarity(string r) => r.ToLower() switch
    {
        "common"       or "comum"   => "common",
        "uncommon"     or "incomum" => "uncommon",
        "rare"         or "rara"    => "rare",
        "mythic rare"  or "mítica"
            or "mythic"             => "mythic",
        _                           => r.ToLower(),
    };

    private static TcgApiCardResponse MapScryfallCard(JsonElement c)
    {
        c.TryGetProperty("image_uris", out var imgs);
        c.TryGetProperty("prices", out var prices);
        c.TryGetProperty("keywords", out var keywords);

        // Power/Toughness para criaturas
        var power     = c.TryGetProperty("power",     out var pw)  ? pw.GetString()  : null;
        var toughness = c.TryGetProperty("toughness", out var tgh) ? tgh.GetString() : null;
        var manaCost  = c.TryGetProperty("mana_cost", out var mc)  ? mc.GetString()  : null;
        var oracleText = c.TryGetProperty("oracle_text", out var ot) ? ot.GetString() : null;
        // CMC/colors
        var colors    = c.TryGetProperty("colors", out var clrs) && clrs.ValueKind == JsonValueKind.Array
                        ? clrs.EnumerateArray().Select(x => x.GetString()!).ToList()
                        : new List<string>();

        static decimal? ParsePrice(JsonElement prices, string key)
        {
            return prices.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String &&
                   decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Any,
                       System.Globalization.CultureInfo.InvariantCulture, out var val)
                   ? val : null;
        }

        return new TcgApiCardResponse
        {
            Id         = c.TryGetProperty("id", out var id)              ? $"mtg:{id.GetString()}"  : string.Empty,
            Name       = c.TryGetProperty("name", out var nm)            ? nm.GetString()!           : string.Empty,
            Game       = "MTG",
            SetName    = c.TryGetProperty("set_name", out var sn)        ? sn.GetString()            : null,
            SetCode    = c.TryGetProperty("set", out var sc)             ? sc.GetString()            : null,
            Number     = c.TryGetProperty("collector_number", out var cn) ? cn.GetString()           : null,
            Rarity     = c.TryGetProperty("rarity", out var rar)         ? rar.GetString()           : null,
            Type       = c.TryGetProperty("type_line", out var tl)       ? tl.GetString()            : null,
            Hp         = power != null ? $"{power}/{toughness}" : manaCost,
            RegulationMark = manaCost,
            FlavorText = oracleText ?? (c.TryGetProperty("flavor_text", out var ft) ? ft.GetString() : null),
            Subtypes   = keywords.ValueKind == JsonValueKind.Array
                         ? keywords.EnumerateArray().Select(k => k.GetString()!).ToList()
                         : null,
            Types      = colors,
            Images     = new TcgCardImages
            {
                Small = imgs.ValueKind != JsonValueKind.Undefined &&
                        imgs.TryGetProperty("small", out var sm) ? sm.GetString() : null,
                Large = imgs.ValueKind != JsonValueKind.Undefined &&
                        imgs.TryGetProperty("large", out var lg) ? lg.GetString() : null,
            },
            Prices     = prices.ValueKind != JsonValueKind.Undefined ? new TcgCardPricesApi
            {
                Low    = ParsePrice(prices, "usd"),
                Market = ParsePrice(prices, "usd_foil"),
                Mid    = ParsePrice(prices, "eur"),
            } : null,
        };
    }

    // =========================================================================
    // YGOPRODeck API (Yu-Gi-Oh!) — https://db.ygoprodeck.com/api/v7/
    // =========================================================================

    private HttpClient YugiohClient() => _factory.CreateClient("YugiohApi");

    private async Task<TcgApiSearchResponse> SearchYugiohCardsAsync(
        string name, int page, int pageSize, string? cardType = null)
    {
        try
        {
            var offset = (page - 1) * pageSize;
            string url;

            // Passcode YGO: 7-8 dígitos → busca exata por ID
            if (System.Text.RegularExpressions.Regex.IsMatch(name.Trim(), @"^\d{7,8}$"))
            {
                url = $"/api/v7/cardinfo.php?id={name.Trim()}";
            }
            else
            {
                url = $"/api/v7/cardinfo.php?fname={Uri.EscapeDataString(name)}&num={pageSize}&offset={offset}";
                // YGO filtra por tipo (Monster, Spell Card, Trap Card, Effect Monster…)
                if (!string.IsNullOrWhiteSpace(cardType))
                    url += $"&type={Uri.EscapeDataString(cardType)}";
            }
            var response = await YugiohClient().GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                return new TcgApiSearchResponse(); // sem resultados

            response.EnsureSuccessStatusCode();

            var json  = await response.Content.ReadAsStringAsync();
            var doc   = JsonDocument.Parse(json);
            var root  = doc.RootElement;

            var cards = root.TryGetProperty("data", out var dataArr)
                ? dataArr.EnumerateArray().Select(MapYugiohCard).ToList()
                : new List<TcgApiCardResponse>();

            var total = root.TryGetProperty("meta", out var meta) &&
                        meta.TryGetProperty("total_rows", out var tr) ? tr.GetInt32() : cards.Count;

            return new TcgApiSearchResponse
            {
                Cards      = cards,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pesquisar cartas Yu-Gi-Oh '{Name}'", name);
            return new TcgApiSearchResponse();
        }
    }

    private static TcgApiCardResponse MapYugiohCard(JsonElement c)
    {
        // Pega a primeira imagem disponível
        string? smallImg = null, largeImg = null;
        if (c.TryGetProperty("card_images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
        {
            var first = imgs.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Undefined)
            {
                smallImg = first.TryGetProperty("image_url_small", out var s) ? s.GetString() : null;
                largeImg = first.TryGetProperty("image_url",       out var l) ? l.GetString() : null;
            }
        }

        // Preço (card_prices[0].tcgplayer_price)
        decimal? price = null;
        if (c.TryGetProperty("card_prices", out var pricesArr) && pricesArr.ValueKind == JsonValueKind.Array)
        {
            var first = pricesArr.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Undefined &&
                first.TryGetProperty("tcgplayer_price", out var tp) &&
                tp.ValueKind == JsonValueKind.String &&
                decimal.TryParse(tp.GetString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var pVal))
                price = pVal;
        }

        var cardId = c.TryGetProperty("id", out var id) ? id.GetInt64().ToString() : Guid.NewGuid().ToString();

        // ATK/DEF/Level
        c.TryGetProperty("atk",       out var atk);
        c.TryGetProperty("def",       out var def);
        c.TryGetProperty("level",     out var lvl);
        c.TryGetProperty("attribute", out var attr);

        var atkStr = atk.ValueKind == JsonValueKind.Number ? atk.GetInt32().ToString() : null;
        var defStr = def.ValueKind == JsonValueKind.Number ? def.GetInt32().ToString() : null;
        var ptStr  = (atkStr != null || defStr != null) ? $"ATK {atkStr ?? "?"} / DEF {defStr ?? "?"}" : null;
        var lvlStr = lvl.ValueKind == JsonValueKind.Number ? $"Nível {lvl.GetInt32()}" : null;
        var attrStr = attr.ValueKind == JsonValueKind.String ? attr.GetString() : null;

        return new TcgApiCardResponse
        {
            Id         = $"ygo:{cardId}",
            Name       = c.TryGetProperty("name", out var nm)     ? nm.GetString()!  : string.Empty,
            Game       = "Yu-Gi-Oh!",
            Number     = cardId,
            Type       = c.TryGetProperty("type", out var t)      ? t.GetString()    : null,
            Subtypes   = c.TryGetProperty("race", out var race)   ? new List<string> { race.GetString()! } : null,
            Types      = attrStr != null ? new List<string> { attrStr } : new(),
            Hp         = ptStr ?? lvlStr,
            FlavorText = c.TryGetProperty("desc", out var desc)   ? desc.GetString() : null,
            Images     = new TcgCardImages { Small = smallImg, Large = largeImg },
            Prices     = price.HasValue ? new TcgCardPricesApi { Market = price } : null,
        };
    }

    // =========================================================================
    // One Piece TCG — OPTCG API (https://optcgapi.com, gratuita, sem auth)
    //
    //   A API não suporta busca por nome direto — retorna todas as cartas de
    //   um tipo. Estratégia:
    //     1. Código exato (OP01-001): GET /api/sets/card/{code}/
    //     2. Nome livre: fan-out parallel por tipo (Character, Event, Stage,
    //        Leader, DON!!), filtro por nome em memória; cada lista de tipo
    //        é cacheada por 24h no IMemoryCache.
    //     3. Paginação manual sobre o resultado filtrado.
    // =========================================================================

    private static readonly System.Text.RegularExpressions.Regex _opCodeRegex =
        new(@"^[A-Z]{2,6}\d{2}-\d{3}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private HttpClient OptcgClient() => _factory.CreateClient("OptcgApi");

    private async Task<TcgApiSearchResponse> SearchOnePieceCardsAsync(
        string name, int page, int pageSize,
        string? setCode = null, string? rarity = null, string? cardType = null)
    {
        try
        {
            var trimmed = name.Trim();

            // Busca por código exato (ex: OP01-001)
            if (_opCodeRegex.IsMatch(trimmed.ToUpper()))
            {
                var code = trimmed.ToUpper();
                var resp = await OptcgClient().GetAsync($"/api/sets/card/{Uri.EscapeDataString(code)}/");
                if (!resp.IsSuccessStatusCode) return new TcgApiSearchResponse();
                var arr  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
                var card = arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0
                    ? MapOptcgCard(arr[0])
                    : arr.ValueKind == JsonValueKind.Object ? MapOptcgCard(arr) : null;
                if (card == null) return new TcgApiSearchResponse();
                return new TcgApiSearchResponse { Cards = new List<TcgApiCardResponse> { card }, TotalCount = 1, Page = 1, PageSize = pageSize };
            }

            // Busca por nome: carrega catálogo completo por tipo (cache 24h)
            var allCards = await GetOptcgCatalogAsync();

            // Aplica filtros
            IEnumerable<TcgApiCardResponse> filtered = allCards;

            if (!string.IsNullOrWhiteSpace(trimmed))
                filtered = filtered.Where(c => c.Name != null &&
                    c.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(setCode))
                filtered = filtered.Where(c => c.SetCode != null &&
                    c.SetCode.Equals(setCode, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(rarity))
                filtered = filtered.Where(c => c.Rarity != null &&
                    c.Rarity.Equals(rarity, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(cardType))
                filtered = filtered.Where(c => c.Type != null &&
                    c.Type.Equals(cardType, StringComparison.OrdinalIgnoreCase));

            var list  = filtered.ToList();
            var total = list.Count;
            var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new TcgApiSearchResponse { Cards = items, TotalCount = total, Page = page, PageSize = pageSize };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar cartas One Piece '{Name}'", name);
            return new TcgApiSearchResponse();
        }
    }

    // Retorna catálogo completo em cache (24h). Fan-out paralelo por tipo.
    private async Task<IList<TcgApiCardResponse>> GetOptcgCatalogAsync()
    {
        const string key = "optcg:catalog";
        if (_cache.TryGetValue(key, out IList<TcgApiCardResponse>? cached) && cached != null)
            return cached;

        _logger.LogInformation("OPTCG: atualizando catálogo completo…");

        var types = new[] { "Character", "Event", "Stage", "Leader" };
        var tasks = types.Select(t => FetchOptcgByTypeAsync(t)).ToArray();
        var don   = FetchOptcgDonAsync();

        await Task.WhenAll(tasks.Append(don));

        var all = tasks.SelectMany(t => t.Result)
                       .Concat(don.Result)
                       .DistinctBy(c => c.Id)
                       .ToList();

        _cache.Set(key, (IList<TcgApiCardResponse>)all, TimeSpan.FromHours(24));
        _logger.LogInformation("OPTCG: catálogo com {N} cartas cacheado.", all.Count);
        return all;
    }

    private async Task<IList<TcgApiCardResponse>> FetchOptcgByTypeAsync(string cardType)
    {
        const string baseKey = "optcg:type:";
        var cacheKey = baseKey + cardType;
        if (_cache.TryGetValue(cacheKey, out IList<TcgApiCardResponse>? cached) && cached != null)
            return cached;

        try
        {
            var resp = await OptcgClient().GetAsync($"/api/sets/filtered/?card_type={Uri.EscapeDataString(cardType)}");
            if (!resp.IsSuccessStatusCode) return Array.Empty<TcgApiCardResponse>();
            var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
            if (root.ValueKind != JsonValueKind.Array) return Array.Empty<TcgApiCardResponse>();
            var cards = root.EnumerateArray().Select(MapOptcgCard).Where(c => !string.IsNullOrEmpty(c.Id)).ToList();
            _cache.Set(cacheKey, (IList<TcgApiCardResponse>)cards, TimeSpan.FromHours(24));
            return cards;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "OPTCG FetchByType '{T}' falhou", cardType); return Array.Empty<TcgApiCardResponse>(); }
    }

    private async Task<IList<TcgApiCardResponse>> FetchOptcgDonAsync()
    {
        const string cacheKey = "optcg:don";
        if (_cache.TryGetValue(cacheKey, out IList<TcgApiCardResponse>? cached) && cached != null)
            return cached;
        try
        {
            var resp = await OptcgClient().GetAsync("/api/allDonCards/");
            if (!resp.IsSuccessStatusCode) return Array.Empty<TcgApiCardResponse>();
            var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
            if (root.ValueKind != JsonValueKind.Array) return Array.Empty<TcgApiCardResponse>();
            var cards = root.EnumerateArray().Select(MapOptcgCard).Where(c => !string.IsNullOrEmpty(c.Id)).ToList();
            _cache.Set(cacheKey, (IList<TcgApiCardResponse>)cards, TimeSpan.FromHours(24));
            return cards;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "OPTCG FetchDon falhou"); return Array.Empty<TcgApiCardResponse>(); }
    }

    private async Task<IEnumerable<TcgSetDto>> FetchOnePieceSetsAsync()
    {
        try
        {
            var resp = await OptcgClient().GetAsync("/api/allSets/");
            if (!resp.IsSuccessStatusCode) return Enumerable.Empty<TcgSetDto>();
            var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
            if (root.ValueKind != JsonValueKind.Array) return Enumerable.Empty<TcgSetDto>();
            return root.EnumerateArray().Select(s => new TcgSetDto
            {
                Code = s.TryGetProperty("set_id",   out var id)   ? id.GetString()!   : "",
                Name = s.TryGetProperty("set_name", out var nm)   ? nm.GetString()!   : "",
                Game = "One Piece TCG",
            }).Where(s => !string.IsNullOrEmpty(s.Code));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "OPTCG FetchSets falhou"); return Enumerable.Empty<TcgSetDto>(); }
    }

    private static TcgApiCardResponse MapOptcgCard(JsonElement c)
    {
        static string? Str(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var id      = Str(c, "card_set_id") ?? "";
        var imgBase = Str(c, "card_image");

        return new TcgApiCardResponse
        {
            Id       = string.IsNullOrEmpty(id) ? "" : $"op:{id}",
            Name     = Str(c, "card_name") ?? "",
            Game     = "One Piece TCG",
            SetName  = Str(c, "set_name"),
            SetCode  = Str(c, "set_id"),
            Number   = id,
            Rarity   = Str(c, "rarity"),
            Type     = Str(c, "card_type"),
            Hp       = Str(c, "card_power"),
            FlavorText = Str(c, "card_text"),
            Types    = Str(c, "card_color") is string col ? new List<string> { col } : new(),
            Subtypes = Str(c, "sub_types")  is string sub ? new List<string> { sub } : new(),
            Images   = imgBase != null ? new TcgCardImages { Small = imgBase, Large = imgBase } : null,
            Prices   = c.TryGetProperty("market_price", out var mp) && mp.ValueKind == JsonValueKind.Number
                ? new TcgCardPricesApi { Market = mp.GetDecimal() } : null,
        };
    }

    // =========================================================================
    // LoL: Riftbound — fan-out paralelo: Riftcodex + Scrydex
    //
    // Riftcodex:  https://api.riftcodex.com  (gratuita, sem auth)
    //   GET /cards/search?query={q}&page={p}&size={n}
    //   GET /sets
    //
    // Scrydex:    https://api.scrydex.com    (requer X-Api-Key + X-Team-ID)
    //   GET /riftbound/v1/cards?q={q}&page={p}&pageSize={n}
    //
    // Os resultados são fundidos e desduplicados por (nome::setCode::número).
    // =========================================================================

    private HttpClient RiftcodexClient() => _factory.CreateClient("RiftboundApi");
    private HttpClient ScrydexClient()
    {
        var client = _factory.CreateClient("ScrydexApi");
        var apiKey = _config["TcgSettings:ScrydexApiKey"];
        var teamId = _config["TcgSettings:ScrydexTeamId"];
        if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey);
        if (!string.IsNullOrWhiteSpace(teamId)) client.DefaultRequestHeaders.TryAddWithoutValidation("X-Team-ID", teamId);
        return client;
    }

    // Orquestrador: busca em paralelo, funde e deduplica
    private async Task<TcgApiSearchResponse> SearchRiftboundCardsAsync(string name, int page, int pageSize)
    {
        var riftcodexTask = SearchRiftcodexAsync(name, page, pageSize);
        var scrydexTask   = SearchScrydexRiftboundAsync(name, page, pageSize);
        await Task.WhenAll(riftcodexTask, scrydexTask);

        var rc = riftcodexTask.Result;
        var sc = scrydexTask.Result;

        var merged = MergeAndDedupe(rc.Cards, sc.Cards);
        var total  = Math.Max(rc.TotalCount, sc.TotalCount);

        return new TcgApiSearchResponse
        {
            Cards      = merged,
            TotalCount = total > 0 ? total : merged.Count,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    // --- Riftcodex -----------------------------------------------------------

    private async Task<TcgApiSearchResponse> SearchRiftcodexAsync(string name, int page, int pageSize)
    {
        try
        {
            var q    = Uri.EscapeDataString(name);
            var url  = $"/cards/search?query={q}&page={page}&size={pageSize}";
            var resp = await RiftcodexClient().GetAsync(url);
            if (!resp.IsSuccessStatusCode) { _logger.LogWarning("Riftcodex {Status} para '{N}'", resp.StatusCode, name); return new(); }

            var root  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
            var items = ExtractArray(root, "data", "results");
            var cards = items.Select(MapRiftcodexCard).ToList();
            var total = TryGetTotal(root, cards.Count);

            return new TcgApiSearchResponse { Cards = cards, TotalCount = total, Page = page, PageSize = pageSize };
        }
        catch (Exception ex) { _logger.LogError(ex, "Riftcodex erro '{N}'", name); return new(); }
    }

    private async Task<IEnumerable<TcgSetDto>> FetchRiftboundSetsAsync()
    {
        try
        {
            var resp = await RiftcodexClient().GetAsync("/sets");
            if (!resp.IsSuccessStatusCode) return Enumerable.Empty<TcgSetDto>();

            var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
            var arr  = ExtractArray(root, "data");
            if (!arr.Any()) return Enumerable.Empty<TcgSetDto>();

            return arr.Select(s => new TcgSetDto
            {
                // Riftcodex set: { id/set_id, label, code, total_cards, release_date, logo_url }
                Code       = Str(s, "code") ?? Str(s, "set_id") ?? Str(s, "id") ?? string.Empty,
                Name       = Str(s, "label") ?? Str(s, "name") ?? string.Empty,
                Game       = "LoL Riftbound",
                TotalCards = Int(s, "total_cards") ?? Int(s, "card_count") ?? 0,
                LogoUrl    = Str(s, "logo_url") ?? Str(s, "logo"),
            }).ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "Riftcodex sets erro"); return Enumerable.Empty<TcgSetDto>(); }
    }

    // Schema real do Riftcodex (verificado):
    // id, name, collector_number, riftbound_id, tcgplayer_id
    // attributes: { energy, might, power }
    // classification: { type, supertype, rarity, domain }
    // text: { rich, plain, flavour }
    // set: { set_id, label }
    // media: { image_url, artist }
    // tags: string[]
    private static TcgApiCardResponse MapRiftcodexCard(JsonElement c)
    {
        c.TryGetProperty("classification", out var cls);
        c.TryGetProperty("text",           out var txt);
        c.TryGetProperty("set",            out var set);
        c.TryGetProperty("media",          out var media);
        c.TryGetProperty("attributes",     out var attr);

        var tags    = c.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array
                      ? tagsEl.EnumerateArray().Select(t => t.GetString()!).ToList()
                      : new List<string>();
        var domain  = Str(cls, "domain");
        var imgUrl  = Str(media, "image_url");

        string? hpStr = null;
        if (attr.ValueKind == JsonValueKind.Object)
        {
            var parts = new List<string>();
            if (attr.TryGetProperty("power",  out var pw)  && pw.ValueKind  != JsonValueKind.Null) parts.Add($"Power {pw}");
            if (attr.TryGetProperty("might",  out var mg)  && mg.ValueKind  != JsonValueKind.Null) parts.Add($"Might {mg}");
            if (attr.TryGetProperty("energy", out var en)  && en.ValueKind  != JsonValueKind.Null) parts.Add($"Energy {en}");
            if (parts.Count > 0) hpStr = string.Join(" / ", parts);
        }

        // Prefer plain card text; fall back to flavour text
        var cardText = Str(txt, "plain") ?? Str(txt, "flavour") ?? Str(txt, "rich");

        return new TcgApiCardResponse
        {
            Id         = $"riftbound:rc:{Str(c, "id") ?? Guid.NewGuid().ToString()}",
            Name       = Str(c, "name") ?? string.Empty,
            Game       = "LoL Riftbound",
            SetName    = Str(set, "label"),
            SetCode    = Str(set, "set_id"),
            Number     = Str(c, "collector_number"),
            Rarity     = Str(cls, "rarity"),
            Type       = Str(cls, "supertype") ?? Str(cls, "type"),
            Subtypes   = tags.Count > 0 ? tags : null,
            Types      = domain != null ? new List<string> { domain } : new(),
            Hp         = hpStr,
            Artist     = Str(media, "artist"),
            FlavorText = cardText,
            Images     = new TcgCardImages { Small = imgUrl, Large = imgUrl },
        };
    }

    // --- Scrydex -------------------------------------------------------------

    private async Task<TcgApiSearchResponse> SearchScrydexRiftboundAsync(string name, int page, int pageSize)
    {
        var apiKey = _config["TcgSettings:ScrydexApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return new TcgApiSearchResponse(); // Scrydex não configurada — pular silenciosamente

        try
        {
            var q    = Uri.EscapeDataString(name);
            var url  = $"/riftbound/v1/cards?q={q}&page={page}&pageSize={pageSize}&include=prices";
            var resp = await ScrydexClient().GetAsync(url);
            if (!resp.IsSuccessStatusCode) { _logger.LogWarning("Scrydex {Status} para '{N}'", resp.StatusCode, name); return new(); }

            var root  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
            var items = ExtractArray(root, "data", "cards");
            var cards = items.Select(MapScrydexRiftboundCard).ToList();
            var total = TryGetTotal(root, cards.Count);

            return new TcgApiSearchResponse { Cards = cards, TotalCount = total, Page = page, PageSize = pageSize };
        }
        catch (Exception ex) { _logger.LogError(ex, "Scrydex Riftbound erro '{N}'", name); return new(); }
    }

    // Schema Scrydex Riftbound (verificado):
    // id ("OGN-296"), name, number, printed_number, domain, type, artist, rarity
    // rules: [{ text }]
    // images: { front, small, medium, large }
    // expansion: { id, name, code, total, release_date, logo }
    // variants: [{ name, images, prices: { tcgplayer_market, tcgplayer_low, tcgplayer_high, ... } }]
    private static TcgApiCardResponse MapScrydexRiftboundCard(JsonElement c)
    {
        c.TryGetProperty("expansion", out var exp);
        c.TryGetProperty("images",    out var imgs);

        var rulesText = c.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Array
            ? string.Join("\n", rules.EnumerateArray()
                .Select(r => r.TryGetProperty("text", out var t) ? t.GetString() : null)
                .Where(t => t != null))
            : null;

        // Pegar preço do primeiro variant disponível
        TcgCardPricesApi? price = null;
        if (c.TryGetProperty("variants", out var variants) && variants.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in variants.EnumerateArray())
            {
                if (v.TryGetProperty("prices", out var prices))
                {
                    price = new TcgCardPricesApi
                    {
                        Market = DecimalOrNull(prices, "tcgplayer_market") ?? DecimalOrNull(prices, "market"),
                        Low    = DecimalOrNull(prices, "tcgplayer_low")    ?? DecimalOrNull(prices, "low"),
                        High   = DecimalOrNull(prices, "tcgplayer_high")   ?? DecimalOrNull(prices, "high"),
                        Mid    = DecimalOrNull(prices, "tcgplayer_mid")    ?? DecimalOrNull(prices, "mid"),
                    };
                    if (price.Market.HasValue) break;
                }
            }
        }

        var domain = Str(c, "domain");
        var large  = Str(imgs, "large") ?? Str(imgs, "front") ?? Str(imgs, "medium");
        var small  = Str(imgs, "small") ?? large;

        return new TcgApiCardResponse
        {
            Id         = $"riftbound:sc:{Str(c, "id") ?? Guid.NewGuid().ToString()}",
            Name       = Str(c, "name") ?? string.Empty,
            Game       = "LoL Riftbound",
            SetName    = Str(exp, "name"),
            SetCode    = Str(exp, "code"),
            Number     = Str(c, "number") ?? Str(c, "printed_number"),
            Rarity     = Str(c, "rarity"),
            Type       = Str(c, "type"),
            Types      = domain != null ? new List<string> { domain } : new(),
            Artist     = Str(c, "artist"),
            FlavorText = rulesText,
            Images     = new TcgCardImages { Small = small, Large = large },
            Prices     = price,
        };
    }

    // --- Deduplicação e merge ------------------------------------------------

    // Chave de deduplicação: nome normalizado + set + número
    private static string DedupeKey(TcgApiCardResponse c) =>
        $"{c.Name.Trim().ToLowerInvariant()}::{c.SetCode?.ToLowerInvariant() ?? ""}::{c.Number?.ToLowerInvariant() ?? ""}";

    private static List<TcgApiCardResponse> MergeAndDedupe(
        List<TcgApiCardResponse> primary,
        List<TcgApiCardResponse> secondary)
    {
        var seen = new Dictionary<string, TcgApiCardResponse>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in primary)
            seen[DedupeKey(card)] = card;

        foreach (var card in secondary)
        {
            var key = DedupeKey(card);
            if (!seen.TryGetValue(key, out var existing))
                seen[key] = card;
            else
                MergeInto(existing, card);
        }

        return seen.Values.ToList();
    }

    // Preenche campos faltantes no primary com dados do secondary
    private static void MergeInto(TcgApiCardResponse primary, TcgApiCardResponse secondary)
    {
        primary.SetName    ??= secondary.SetName;
        primary.SetCode    ??= secondary.SetCode;
        primary.Number     ??= secondary.Number;
        primary.Rarity     ??= secondary.Rarity;
        primary.Type       ??= secondary.Type;
        primary.Artist     ??= secondary.Artist;
        primary.FlavorText ??= secondary.FlavorText;
        primary.Hp         ??= secondary.Hp;
        if (primary.Subtypes == null || primary.Subtypes.Count == 0) primary.Subtypes = secondary.Subtypes;
        if (primary.Types    == null || primary.Types.Count    == 0) primary.Types    = secondary.Types;
        if (primary.Images?.Large == null && secondary.Images?.Large != null)
            primary.Images = secondary.Images;
        // Preços: prefer Scrydex (tem dados de mercado mais completos)
        if (primary.Prices     == null && secondary.Prices     != null) primary.Prices     = secondary.Prices;
        if (primary.AllPrices  == null && secondary.AllPrices  != null) primary.AllPrices  = secondary.AllPrices;
    }

    // --- Helpers estáticos ---------------------------------------------------

    private static IEnumerable<JsonElement> ExtractArray(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
            if (root.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.Array)
                return val.EnumerateArray();
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray();
        return Enumerable.Empty<JsonElement>();
    }

    private static int TryGetTotal(JsonElement root, int fallback)
    {
        if (root.TryGetProperty("total",      out var t)) return t.GetInt32();
        if (root.TryGetProperty("total_count",out var c)) return c.GetInt32();
        if (root.TryGetProperty("meta", out var meta))
        {
            if (meta.TryGetProperty("total", out var mt)) return mt.GetInt32();
            if (meta.TryGetProperty("count", out var mc)) return mc.GetInt32();
        }
        if (root.TryGetProperty("pagination", out var pg) && pg.TryGetProperty("total", out var pt)) return pt.GetInt32();
        return fallback;
    }

    private static string? Str(JsonElement el, string key) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement el, string key) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static decimal? DecimalOrNull(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
        ? v.GetDecimal() : null;
}
