// =============================================================================
// TcgApiClient.cs — Cliente HTTP multi-provider para APIs TCG públicas
//
// Roteamento por jogo:
//   Pokemon      → https://api.pokemontcg.io/v2/   (gratuita, sem auth obrigatória)
//   MTG / Magic  → https://api.scryfall.com/        (gratuita, sem auth)
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

        // Para outros jogos (Yu-Gi-Oh, One Piece, etc.) retorna vazio sem erro
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
            var q       = Uri.EscapeDataString($"name:*{name}*");
            var url     = $"/v2/cards?q={q}&page={page}&pageSize={pageSize}";
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
        var prices = ExtractPokemonPrices(tcgp);

        c.TryGetProperty("images", out var imgs);
        c.TryGetProperty("set", out var set);
        c.TryGetProperty("subtypes", out var subtypesEl);

        return new TcgApiCardResponse
        {
            Id       = c.TryGetProperty("id", out var id)         ? $"pokemon:{id.GetString()}" : string.Empty,
            Name     = c.TryGetProperty("name", out var nm)       ? nm.GetString()!              : string.Empty,
            Game     = "Pokemon",
            SetName  = set.ValueKind != JsonValueKind.Undefined &&
                       set.TryGetProperty("name", out var sn)     ? sn.GetString()               : null,
            SetCode  = set.ValueKind != JsonValueKind.Undefined &&
                       set.TryGetProperty("id", out var sc)       ? sc.GetString()               : null,
            Number   = c.TryGetProperty("number", out var num)    ? num.GetString()              : null,
            Rarity   = c.TryGetProperty("rarity", out var rar)    ? rar.GetString()              : null,
            Type     = c.TryGetProperty("supertype", out var st)  ? st.GetString()               : null,
            Subtypes = subtypesEl.ValueKind == JsonValueKind.Array
                       ? subtypesEl.EnumerateArray().Select(s => s.GetString()!).ToList()
                       : null,
            Images   = new TcgCardImages
            {
                Small = imgs.ValueKind != JsonValueKind.Undefined &&
                        imgs.TryGetProperty("small", out var sm) ? sm.GetString() : null,
                Large = imgs.ValueKind != JsonValueKind.Undefined &&
                        imgs.TryGetProperty("large", out var lg) ? lg.GetString() : null,
            },
            Prices   = prices,
        };
    }

    private static TcgCardPricesApi? ExtractPokemonPrices(JsonElement tcgp)
    {
        if (tcgp.ValueKind == JsonValueKind.Undefined) return null;
        if (!tcgp.TryGetProperty("prices", out var pricesEl)) return null;

        // Tenta holofoil → normal → 1stEditionNormal, nessa ordem
        foreach (var variant in new[] { "holofoil", "normal", "1stEditionNormal", "reverseHolofoil" })
        {
            if (!pricesEl.TryGetProperty(variant, out var v)) continue;
            return new TcgCardPricesApi
            {
                Low    = v.TryGetProperty("low",    out var lo) && lo.ValueKind == JsonValueKind.Number ? lo.GetDecimal() : null,
                Mid    = v.TryGetProperty("mid",    out var mi) && mi.ValueKind == JsonValueKind.Number ? mi.GetDecimal() : null,
                High   = v.TryGetProperty("high",   out var hi) && hi.ValueKind == JsonValueKind.Number ? hi.GetDecimal() : null,
                Market = v.TryGetProperty("market", out var mk) && mk.ValueKind == JsonValueKind.Number ? mk.GetDecimal() : null,
            };
        }
        return null;
    }

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
}
