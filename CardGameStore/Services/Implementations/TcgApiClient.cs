// =============================================================================
// TcgApiClient.cs — Cliente HTTP multi-provider para APIs TCG públicas
//
// Roteamento por jogo:
//   Pokemon      → https://api.pokemontcg.io/v2/          (gratuita, sem auth obrigatória)
//   MTG / Magic  → https://api.scryfall.com/               (gratuita, sem auth)
//   Yu-Gi-Oh!    → https://db.ygoprodeck.com/api/v7/       (gratuita, sem auth)
//   Outros       → retorna vazio (graceful degradation)
//
// Para aumentar rate limits do Pokemon TCG API, defina:
//   appsettings.json: "TcgSettings": { "PokemonApiKey": "sua-chave" }
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CardGameStore.Services.Implementations;

public class TcgApiClient : ITcgApiClient
{
    private readonly IHttpClientFactory          _factory;
    private readonly IConfiguration              _config;
    private readonly ILogger<TcgApiClient>       _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
    };

    public TcgApiClient(IHttpClientFactory factory, IConfiguration config, ILogger<TcgApiClient> logger)
    {
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

    public async Task<TcgApiSearchResponse> SearchCardsAsync(string name, string? game, int page, int pageSize)
    {
        var normalized = game?.ToLowerInvariant() ?? string.Empty;

        if (normalized.Contains("pokemon") || normalized.Contains("pokémon"))
            return await SearchPokemonCardsAsync(name, page, pageSize);

        if (normalized.Contains("mtg") || normalized.Contains("magic"))
            return await SearchScryfallCardsAsync(name, page, pageSize);

        if (normalized.Contains("yu-gi-oh") || normalized.Contains("yugioh") || normalized.Contains("yu gi oh"))
            return await SearchYugiohCardsAsync(name, page, pageSize);

        if (normalized.Contains("riftbound") || normalized.Contains("lol riftbound"))
            return await SearchRiftboundCardsAsync(name, page, pageSize);

        // Para outros jogos (One Piece, Dragon Ball, etc.) retorna vazio sem erro
        _logger.LogDebug("Jogo '{Game}' sem provider configurado — retornando vazio.", game);
        return new TcgApiSearchResponse();
    }

    public async Task<IEnumerable<TcgSetDto>> FetchSetsAsync(string game)
    {
        var normalized = game.ToLowerInvariant();

        if (normalized.Contains("pokemon") || normalized.Contains("pokémon"))
            return await FetchPokemonSetsAsync();

        if (normalized.Contains("mtg") || normalized.Contains("magic"))
            return await FetchScryfallSetsAsync();

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

    private async Task<TcgApiSearchResponse> SearchPokemonCardsAsync(string name, int page, int pageSize)
    {
        try
        {
            // Busca por código: "set:SV8PT5 number:1" → passa diretamente como query
            var q = name.StartsWith("set:", StringComparison.OrdinalIgnoreCase)
                ? Uri.EscapeDataString(name)
                : Uri.EscapeDataString($"name:*{name}*");
            var url = $"/v2/cards?q={q}&page={page}&pageSize={pageSize}";
            var response = await PokemonClient().GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json    = await response.Content.ReadAsStringAsync();
            var doc     = JsonDocument.Parse(json);
            var root    = doc.RootElement;

            var cards = root.TryGetProperty("data", out var dataArr)
                ? dataArr.EnumerateArray().Select(MapPokemonCard).ToList()
                : new List<TcgApiCardResponse>();

            return new TcgApiSearchResponse
            {
                Cards      = cards,
                TotalCount = root.TryGetProperty("totalCount", out var tc) ? tc.GetInt32() : cards.Count,
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
        c.TryGetProperty("tcgplayer", out var tcgp);
        c.TryGetProperty("images",   out var imgs);
        c.TryGetProperty("set",      out var set);

        var allPrices = ExtractAllPokemonPrices(tcgp);
        // market price da variação principal para compatibilidade
        var mainPrice = allPrices?.Holofoil ?? allPrices?.Normal ?? allPrices?.FirstEditionNormal ?? allPrices?.ReverseHolofoil;

        return new TcgApiCardResponse
        {
            Id       = c.TryGetProperty("id", out var id)              ? $"pokemon:{id.GetString()}" : string.Empty,
            Name     = c.TryGetProperty("name", out var nm)            ? nm.GetString()!              : string.Empty,
            Game     = "Pokemon",
            SetName  = set.ValueKind != JsonValueKind.Undefined &&
                       set.TryGetProperty("name", out var sn)          ? sn.GetString()               : null,
            SetCode  = set.ValueKind != JsonValueKind.Undefined &&
                       set.TryGetProperty("id", out var sc)            ? sc.GetString()               : null,
            Number   = c.TryGetProperty("number", out var num)         ? num.GetString()              : null,
            Rarity   = c.TryGetProperty("rarity", out var rar)         ? rar.GetString()              : null,
            Type     = c.TryGetProperty("supertype", out var st)       ? st.GetString()               : null,
            Subtypes = JsonArrayToList(c, "subtypes"),
            Types    = JsonArrayToList(c, "types"),
            Hp       = c.TryGetProperty("hp", out var hp)              ? hp.GetString()               : null,
            Artist   = c.TryGetProperty("artist", out var art)         ? art.GetString()              : null,
            FlavorText = c.TryGetProperty("flavorText", out var ft)    ? ft.GetString()               : null,
            RegulationMark = c.TryGetProperty("regulationMark", out var rm) ? rm.GetString()          : null,
            Attacks    = ExtractPokemonAttacks(c),
            Weaknesses = ExtractPokemonWeakResist(c, "weaknesses"),
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
            AllPrices = allPrices,
            Prices    = mainPrice,
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
        };

        return (result.Normal == null && result.Holofoil == null &&
                result.ReverseHolofoil == null && result.FirstEditionNormal == null) ? null : result;
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

    private async Task<TcgApiSearchResponse> SearchScryfallCardsAsync(string name, int page, int pageSize)
    {
        try
        {
            await Task.Delay(100); // Scryfall pede ≤10 req/s entre chamadas
            // Scryfall usa paginação por "page" (não offset) com 175 cards/page max
            var q       = Uri.EscapeDataString(name);
            var url     = $"/cards/search?q={q}&page={page}&order=name";
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

    private static TcgApiCardResponse MapScryfallCard(JsonElement c)
    {
        c.TryGetProperty("image_uris", out var imgs);
        c.TryGetProperty("prices", out var prices);
        c.TryGetProperty("keywords", out var keywords);

        return new TcgApiCardResponse
        {
            Id      = c.TryGetProperty("id", out var id)            ? $"mtg:{id.GetString()}"  : string.Empty,
            Name    = c.TryGetProperty("name", out var nm)          ? nm.GetString()!           : string.Empty,
            Game    = "MTG",
            SetName = c.TryGetProperty("set_name", out var sn)      ? sn.GetString()            : null,
            SetCode = c.TryGetProperty("set", out var sc)           ? sc.GetString()            : null,
            Number  = c.TryGetProperty("collector_number", out var cn) ? cn.GetString()         : null,
            Rarity  = c.TryGetProperty("rarity", out var rar)       ? rar.GetString()           : null,
            Type    = c.TryGetProperty("type_line", out var tl)     ? tl.GetString()            : null,
            Subtypes = keywords.ValueKind == JsonValueKind.Array
                       ? keywords.EnumerateArray().Select(k => k.GetString()!).ToList()
                       : null,
            Images  = new TcgCardImages
            {
                Small = imgs.ValueKind != JsonValueKind.Undefined &&
                        imgs.TryGetProperty("small", out var sm) ? sm.GetString() : null,
                Large = imgs.ValueKind != JsonValueKind.Undefined &&
                        imgs.TryGetProperty("large", out var lg) ? lg.GetString() : null,
            },
            Prices  = prices.ValueKind != JsonValueKind.Undefined ? new TcgCardPricesApi
            {
                Low    = prices.TryGetProperty("usd",      out var usd)  && usd.ValueKind  == JsonValueKind.String &&
                         decimal.TryParse(usd.GetString(), System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var usdVal) ? usdVal : null,
                Market = prices.TryGetProperty("usd_foil", out var foil) && foil.ValueKind == JsonValueKind.String &&
                         decimal.TryParse(foil.GetString(), System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var foilVal) ? foilVal : null,
            } : null,
        };
    }

    // =========================================================================
    // YGOPRODeck API (Yu-Gi-Oh!) — https://db.ygoprodeck.com/api/v7/
    // =========================================================================

    private HttpClient YugiohClient() => _factory.CreateClient("YugiohApi");

    private async Task<TcgApiSearchResponse> SearchYugiohCardsAsync(string name, int page, int pageSize)
    {
        try
        {
            var q        = Uri.EscapeDataString(name);
            var offset   = (page - 1) * pageSize;
            var url      = $"/api/v7/cardinfo.php?fname={q}&num={pageSize}&offset={offset}";
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

        return new TcgApiCardResponse
        {
            Id      = $"ygo:{cardId}",
            Name    = c.TryGetProperty("name", out var nm)     ? nm.GetString()!  : string.Empty,
            Game    = "Yu-Gi-Oh!",
            SetName = null,
            SetCode = null,
            Number  = cardId,
            Rarity  = null,
            Type    = c.TryGetProperty("type", out var t)      ? t.GetString()    : null,
            Subtypes = c.TryGetProperty("race", out var race)  ? new List<string> { race.GetString()! } : null,
            Images  = new TcgCardImages { Small = smallImg, Large = largeImg },
            Prices  = price.HasValue ? new TcgCardPricesApi { Market = price } : null,
        };
    }

    // =========================================================================
    // LoL: Riftbound API — TCG físico da Riot Games
    // URL base configurável via appsettings: TcgSettings:RiftboundApiUrl
    // Endpoint esperado (community API): GET /cards?name={name}&page={page}&pageSize={pageSize}
    // =========================================================================

    private HttpClient RiftboundClient() => _factory.CreateClient("RiftboundApi");

    private async Task<TcgApiSearchResponse> SearchRiftboundCardsAsync(string name, int page, int pageSize)
    {
        try
        {
            var q        = Uri.EscapeDataString(name);
            var url      = $"/cards?name={q}&page={page}&pageSize={pageSize}";
            var response = await RiftboundClient().GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Riftbound API retornou {Status} para '{Name}'", response.StatusCode, name);
                return new TcgApiSearchResponse();
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var cards = root.TryGetProperty("data", out var dataArr)
                ? dataArr.EnumerateArray().Select(MapRiftboundCard).ToList()
                : root.ValueKind == JsonValueKind.Array
                    ? root.EnumerateArray().Select(MapRiftboundCard).Take(pageSize).ToList()
                    : new List<TcgApiCardResponse>();

            return new TcgApiSearchResponse
            {
                Cards      = cards,
                TotalCount = root.TryGetProperty("total", out var total) ? total.GetInt32() : cards.Count,
                Page       = page,
                PageSize   = pageSize,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de conexão com Riftbound API para '{Name}'", name);
            return new TcgApiSearchResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pesquisar cartas Riftbound '{Name}'", name);
            return new TcgApiSearchResponse();
        }
    }

    private static TcgApiCardResponse MapRiftboundCard(JsonElement c)
    {
        return new TcgApiCardResponse
        {
            Id       = c.TryGetProperty("id", out var id)       ? $"riftbound:{id.GetString()}" : $"riftbound:{Guid.NewGuid()}",
            Name     = c.TryGetProperty("name", out var nm)     ? nm.GetString()!                : string.Empty,
            Game     = "LoL Riftbound",
            SetName  = c.TryGetProperty("set", out var set)     ? set.GetString()               : null,
            SetCode  = c.TryGetProperty("setCode", out var sc)  ? sc.GetString()               : null,
            Number   = c.TryGetProperty("number", out var num)  ? num.GetString()              : null,
            Rarity   = c.TryGetProperty("rarity", out var rar)  ? rar.GetString()              : null,
            Type     = c.TryGetProperty("type", out var t)      ? t.GetString()                 : null,
            Subtypes = c.TryGetProperty("subtypes", out var sub) && sub.ValueKind == JsonValueKind.Array
                       ? sub.EnumerateArray().Select(s => s.GetString()!).ToList()
                       : c.TryGetProperty("region", out var reg) && reg.ValueKind == JsonValueKind.String
                           ? new List<string> { reg.GetString()! }
                           : null,
            Images   = new TcgCardImages
            {
                Small = c.TryGetProperty("imageSmall", out var sm) ? sm.GetString()
                      : c.TryGetProperty("image", out var img)     ? img.GetString() : null,
                Large = c.TryGetProperty("imageLarge", out var lg) ? lg.GetString()
                      : c.TryGetProperty("image", out var img2)    ? img2.GetString() : null,
            },
        };
    }
}
