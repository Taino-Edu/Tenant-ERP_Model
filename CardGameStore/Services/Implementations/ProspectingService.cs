// =============================================================================
// ProspectingService.cs — Busca de possíveis clientes via OpenStreetMap
// (Nominatim pra geocodificar a cidade + Overpass API pra achar os negócios),
// com classificação heurística sem IA (presença digital, score de
// oportunidade, faixa de faturamento) e enriquecimento opcional via Gemini.
//
// Por que OSM em vez de Google Places: os dados do OSM são licenciados sob
// ODbL (Open Database License) — permitem guardar/reusar os dados, diferente
// das políticas do Google Places (que proíbem cachear nome/telefone vindos do
// Text Search, só o Place ID é livre pra guardar pra sempre). Bônus: OSM é de
// graça, sem chave de API nenhuma.
//
// ProspectingSettings:GeminiApiKey continua sendo a chave preferida. Quando ela
// não existe, a instalação usa GeminiSettings:ApiKey, que já abastece os outros
// recursos de IA. Assim uma variável opcional ausente não derruba a prospecção.
// =============================================================================

using System.Text;
using System.Text.Json;
using CardGameStore.Common;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class ProspectingService : IProspectingService
{
    private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromDays(7);

    // Modelos tentados em ordem, com fallback: o Google já aposentou modelo duas
    // vezes na vida deste repo (gemini-2.5-flash virou 404 e derrubou o
    // assistente inteiro, ver commit 2c25f09), e um alias morto aqui quebrava o
    // enriquecimento por completo — era exatamente o "Falha ao enriquecer com
    // IA" que o usuário reportava. Com a cadeia, aposentar um modelo degrada a
    // qualidade da análise em vez de derrubar a feature.
    //
    // O primeiro favorece qualidade; os seguintes são modelos GA mais leves.
    // Nomes fixos evitam que uma troca silenciosa de alias altere o contrato da
    // API sem que o aplicativo tenha sido atualizado.
    private static readonly string[] GeminiModels =
    [
        "gemini-3.7-flash",
        "gemini-3.6-flash",
        "gemini-3.5-flash-lite",
    ];

    private static string GeminiUrlFor(string model) =>
        $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

    // A instância pública principal (overpass-api.de) é gratuita mas sofre
    // rate-limiting e quedas com frequência — sem SLA nenhuma. Mirrors
    // públicos conhecidos como fallback pra não depender de uma única
    // instância no ar pra prospecção funcionar.
    private static readonly string[] OverpassUrls =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.openstreetmap.ru/api/interpreter",
    ];

    // Mapeia termos comuns em português pro par tag=valor do OpenStreetMap.
    // Sem entrada exata, cai no fallback (nome contendo o termo buscado, ver
    // BuildOverpassQuery). Lista pequena de propósito — cresce sob demanda.
    private static readonly Dictionary<string, (string Tag, string Value)> CategoriaParaTagOsm = new(StringComparer.OrdinalIgnoreCase)
    {
        ["roupa"]        = ("shop", "clothes"),
        ["roupas"]       = ("shop", "clothes"),
        ["vestuario"]    = ("shop", "clothes"),
        ["restaurante"]  = ("amenity", "restaurant"),
        ["farmacia"]     = ("amenity", "pharmacy"),
        ["mercado"]      = ("shop", "supermarket"),
        ["supermercado"] = ("shop", "supermarket"),
        ["padaria"]      = ("shop", "bakery"),
        ["eletronicos"]  = ("shop", "electronics"),
        ["salao"]        = ("shop", "hairdresser"),
        ["cabeleireiro"] = ("shop", "hairdresser"),
        ["pet"]          = ("shop", "pet"),
        ["petshop"]      = ("shop", "pet"),
        ["livraria"]     = ("shop", "books"),
        ["papelaria"]    = ("shop", "stationery"),
        ["academia"]     = ("leisure", "fitness_centre"),
        ["moveis"]       = ("shop", "furniture"),
        ["joalheria"]    = ("shop", "jewelry"),
        ["joias"]        = ("shop", "jewelry"),
        ["otica"]        = ("shop", "optician"),
        ["sapataria"]    = ("shop", "shoes"),
        ["calcados"]     = ("shop", "shoes"),
        ["bar"]          = ("amenity", "bar"),
        ["lanchonete"]   = ("amenity", "fast_food"),
        ["cafe"]         = ("amenity", "cafe"),
        ["pizzaria"]     = ("amenity", "restaurant"),
        ["hotel"]        = ("tourism", "hotel"),
        ["pousada"]      = ("tourism", "guest_house"),
        ["clinica"]      = ("amenity", "clinic"),
        ["dentista"]     = ("amenity", "dentist"),
        ["veterinario"]  = ("amenity", "veterinary"),
        ["oficina"]      = ("shop", "car_repair"),
        ["autopecas"]    = ("shop", "car_parts"),
        ["conveniencia"] = ("shop", "convenience"),
        ["cosmeticos"]   = ("shop", "beauty"),
        ["beleza"]       = ("shop", "beauty"),
        ["tatuagem"]     = ("shop", "tattoo"),
        ["lavanderia"]   = ("shop", "laundry"),
        ["floricultura"] = ("shop", "florist"),
        ["brinquedos"]   = ("shop", "toys"),
        ["informatica"]  = ("shop", "computer"),
        ["celular"]      = ("shop", "mobile_phone"),
        ["material de construcao"] = ("shop", "doityourself"),
        ["construcao"]   = ("shop", "doityourself"),
        ["contabilidade"] = ("office", "accountant"),
        ["imobiliaria"]   = ("office", "estate_agent"),
        ["advocacia"]     = ("office", "lawyer"),
    };

    // Assinaturas conhecidas no HTML de plataformas de e-commerce — presença
    // de qualquer uma classifica o site como "ECommerce" em vez de "SiteLegado".
    private static readonly string[] EcommerceSignatures =
    [
        "cdn.shopify.com", "myshopify.com",
        "nuvemshop.com.br", "tiendanube.com",
        "vtexassets.com", "vtex.com.br",
        "woocommerce", "wp-content/plugins/woocommerce",
        "mercadoshops", "lojaintegrada.com.br",
    ];

    private readonly IHttpClientFactory       _factory;
    private readonly IConfiguration           _config;
    private readonly ILogger<ProspectingService> _logger;
    private readonly CatalogDbContext          _catalog;

    public ProspectingService(IHttpClientFactory factory, IConfiguration config,
        ILogger<ProspectingService> logger, CatalogDbContext catalog)
    {
        _factory = factory;
        _config  = config;
        _logger  = logger;
        _catalog = catalog;
    }

    public IReadOnlyList<string> ListSupportedCategories() => CategoriaParaTagOsm.Keys
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x)
        .ToList();

    public async Task<List<ProspectingSearchSummaryDto>> ListSearchesAsync(int limit = 20) =>
        await _catalog.ProspectingSearches.AsNoTracking()
            .OrderByDescending(s => s.RefreshedAt)
            .Take(limit)
            .Select(s => new ProspectingSearchSummaryDto
            {
                Id = s.Id, Categoria = s.Category, Cidade = s.City, Source = s.Source,
                Status = s.Status.ToString(), ResultCount = s.ResultCount,
                RefreshedAt = s.RefreshedAt, ExpiresAt = s.ExpiresAt, Warning = s.Warning,
            }).ToListAsync();

    public async Task<ProspectingSearchResultDto?> GetSearchAsync(Guid id)
    {
        var search = await _catalog.ProspectingSearches.AsNoTracking()
            .Include(s => s.Candidates)
                .ThenInclude(c => c.Observations.OrderByDescending(o => o.ObservedAt).Take(5))
            .FirstOrDefaultAsync(s => s.Id == id);
        return search is null ? null : ToResult(search, true);
    }

    public async Task<ProspectingSearchResultDto> SearchAsync(string categoria, string cidade, bool forceRefresh = false)
    {
        categoria = categoria.Trim();
        cidade = cidade.Trim();
        var now = DateTime.UtcNow;
        var cacheKey = NormalizeCacheKey(categoria, cidade);

        var search = await _catalog.ProspectingSearches
            .Include(s => s.Candidates)
                .ThenInclude(c => c.Observations.OrderByDescending(o => o.ObservedAt).Take(5))
            .OrderByDescending(s => s.RefreshedAt)
            .FirstOrDefaultAsync(s => s.CacheKey == cacheKey);

        if (!forceRefresh && search is not null && search.ExpiresAt > now && search.Status != ProspectingSearchStatus.Failed)
            return ToResult(search, true);

        var location = await ResolveLocationAsync(cidade);
        var query = BuildOverpassQuery(categoria, location.Bbox, location.AreaId);
        var body = await QueryOverpassWithFallbackAsync(query);

        using var doc = JsonDocument.Parse(body);
        var rawPlaces = new List<(string PlaceId, string Nome, string? Endereco, string? Telefone, string? Website, bool TemHorario)>();

        if (doc.RootElement.TryGetProperty("elements", out var elements))
        {
            foreach (var el in elements.EnumerateArray())
            {
                if (!el.TryGetProperty("tags", out var tags) || !tags.TryGetProperty("name", out var nameEl))
                    continue; // sem nome não dá pra virar lead de verdade

                var tipo = el.TryGetProperty("type", out var tp) ? tp.GetString() : "node";
                var id   = el.TryGetProperty("id", out var idEl) ? idEl.GetInt64() : 0;

                rawPlaces.Add((
                    PlaceId:    $"{tipo}/{id}", // convenção do próprio OSM pra referenciar um elemento
                    Nome:       nameEl.GetString() ?? "",
                    Endereco:   BuildEndereco(tags),
                    Telefone:   GetTag(tags, "phone") ?? GetTag(tags, "contact:phone"),
                    Website:    GetTag(tags, "website") ?? GetTag(tags, "contact:website"),
                    TemHorario: GetTag(tags, "opening_hours") is not null
                ));
            }
        }

        // Checagem de site roda em paralelo (grau limitado) — em série, um
        // resultado com varios sites lentos/fora do ar seguraria a resposta
        // inteira por dezenas de segundos (timeout de 8s por site).
        using var throttle = new SemaphoreSlim(5);
        var candidates = await Task.WhenAll(rawPlaces.Select(async p =>
        {
            await throttle.WaitAsync();
            try
            {
                var digitalPresence = await ClassifyDigitalPresenceAsync(p.Website);
                var temTelefone     = !string.IsNullOrWhiteSpace(p.Telefone);
                var temEnderecoCompleto = !string.IsNullOrWhiteSpace(p.Endereco);

                return new ProspectCandidateDto
                {
                    PlaceId               = p.PlaceId,
                    Nome                  = p.Nome,
                    Endereco              = p.Endereco,
                    Telefone              = p.Telefone,
                    Website               = p.Website,
                    DigitalPresence       = digitalPresence,
                    OpportunityScore      = CalculateOpportunityScore(temTelefone, p.TemHorario, temEnderecoCompleto, digitalPresence),
                    EstimatedRevenueRange = EstimateRevenueRangeHeuristic(temTelefone, p.TemHorario, temEnderecoCompleto),
                };
            }
            finally
            {
                throttle.Release();
            }
        }));

        var isNewSearch = search is null;
        search ??= new ProspectingSearch
        {
            Category = categoria,
            City = cidade,
            CacheKey = cacheKey,
            CreatedAt = now,
        };
        if (_catalog.Entry(search).State == EntityState.Detached)
            _catalog.ProspectingSearches.Add(search);

        search.Category = categoria;
        search.City = cidade;
        search.Status = ProspectingSearchStatus.Completed;
        search.Warning = null;
        search.South = location.Bbox.Sul;
        search.West = location.Bbox.Oeste;
        search.North = location.Bbox.Norte;
        search.East = location.Bbox.Leste;
        search.OsmAreaId = location.AreaId;
        search.RefreshedAt = now;
        search.ExpiresAt = now.Add(SearchCacheTtl);

        var sourceIds = candidates.Select(c => c.PlaceId).Distinct().ToList();
        var existingLeads = await _catalog.Leads.AsNoTracking()
            .Where(l => l.PlaceId != null && sourceIds.Contains(l.PlaceId))
            .Select(l => new { l.Id, l.PlaceId, l.Status, l.ConvertedTenantId })
            .ToListAsync();
        var leadBySource = existingLeads
            .Where(l => l.PlaceId is not null)
            .ToDictionary(l => l.PlaceId!, StringComparer.OrdinalIgnoreCase);
        var suppressions = await _catalog.ProspectSuppressions.AsNoTracking().ToListAsync();

        var existingBySource = search.Candidates.ToDictionary(c => c.SourceId, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dto in candidates)
        {
            seen.Add(dto.PlaceId);
            if (!existingBySource.TryGetValue(dto.PlaceId, out var entity))
            {
                entity = new ProspectCandidate { Search = search, SearchId = search.Id, SourceId = dto.PlaceId, FirstSeenAt = now };
                search.Candidates.Add(entity);
            }

            var confidence = CalculateEnrichmentConfidence(dto);
            ObserveChange(entity, "Name", entity.Name, dto.Nome, "OpenStreetMap", confidence, now);
            ObserveChange(entity, "Address", entity.Address, dto.Endereco, "OpenStreetMap", confidence, now);
            ObserveChange(entity, "Phone", entity.Phone, dto.Telefone, "OpenStreetMap", confidence, now);
            ObserveChange(entity, "Website", entity.Website, dto.Website, "OpenStreetMap", confidence, now);
            ObserveChange(entity, "DigitalPresence", entity.DigitalPresence, dto.DigitalPresence,
                string.IsNullOrWhiteSpace(dto.Website) ? "OpenStreetMap" : "WebsiteCheck", confidence, now);
            ObserveChange(entity, "OpportunityScore", entity.OpportunityScore.ToString(),
                dto.OpportunityScore.ToString(), "DeterministicScore", 100, now);
            ObserveChange(entity, "EstimatedRevenueRange", entity.EstimatedRevenueRange,
                dto.EstimatedRevenueRange, "Heuristic", 35, now);

            entity.Name = dto.Nome;
            entity.Address = dto.Endereco;
            entity.Phone = dto.Telefone;
            entity.Website = dto.Website;
            entity.DigitalPresence = dto.DigitalPresence;
            entity.OpportunityScore = dto.OpportunityScore;
            entity.EstimatedRevenueRange = dto.EstimatedRevenueRange;
            entity.LastSeenAt = now;
            entity.EnrichmentStatus = ProspectEnrichmentStatus.Updated;
            entity.LastEnrichedAt = now;
            entity.EnrichmentSource = string.IsNullOrWhiteSpace(dto.Website)
                ? "OpenStreetMap"
                : "OpenStreetMap;WebsiteCheck";
            entity.EnrichmentConfidence = confidence;
            if (leadBySource.TryGetValue(dto.PlaceId, out var lead))
            {
                entity.LeadId = lead.Id;
                entity.Status = lead.ConvertedTenantId.HasValue || lead.Status == LeadStatus.Convertido
                    ? ProspectCandidateStatus.Customer
                    : ProspectCandidateStatus.Lead;
            }
            else if (entity.Status == ProspectCandidateStatus.Stale)
                entity.Status = ProspectCandidateStatus.New;
            if (IsSuppressed(dto, suppressions))
                entity.Status = ProspectCandidateStatus.Suppressed;
        }

        foreach (var missing in search.Candidates.Where(c => !seen.Contains(c.SourceId) &&
                     c.Status is ProspectCandidateStatus.New or ProspectCandidateStatus.Selected))
            missing.Status = ProspectCandidateStatus.Stale;

        search.ResultCount = search.Candidates.Count(c =>
            c.Status is not ProspectCandidateStatus.Stale and not ProspectCandidateStatus.Suppressed);
        try
        {
            await _catalog.SaveChangesAsync();
        }
        catch (DbUpdateException) when (isNewSearch)
        {
            // Duas requisições iguais podem terminar a consulta externa ao mesmo
            // tempo. O índice único decide a vencedora; a outra devolve o mesmo
            // snapshot persistido em vez de criar uma pesquisa duplicada.
            _catalog.ChangeTracker.Clear();
            var concurrentSearch = await _catalog.ProspectingSearches.AsNoTracking()
                .Include(s => s.Candidates)
                    .ThenInclude(c => c.Observations.OrderByDescending(o => o.ObservedAt).Take(5))
                .FirstOrDefaultAsync(s => s.CacheKey == cacheKey);
            if (concurrentSearch is not null)
                return ToResult(concurrentSearch, true);
            throw;
        }
        return ToResult(search, false);
    }

    private static string NormalizeCacheKey(string category, string city)
    {
        static string Normalize(string value)
        {
            var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            return string.Concat(decomposed.Where(c =>
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark))
                .Normalize(NormalizationForm.FormC);
        }
        return $"{Normalize(city)}|{Normalize(category)}";
    }

    private static ProspectingSearchResultDto ToResult(ProspectingSearch search, bool fromCache) => new()
    {
        Id = search.Id, Categoria = search.Category, Cidade = search.City,
        Source = search.Source, Status = search.Status.ToString(), ResultCount = search.ResultCount,
        RefreshedAt = search.RefreshedAt, ExpiresAt = search.ExpiresAt,
        Warning = search.Warning, FromCache = fromCache,
        Candidates = search.Candidates.OrderByDescending(c => c.OpportunityScore).ThenBy(c => c.Name)
            .Select(ToDto).ToList(),
    };

    private static ProspectCandidateDto ToDto(ProspectCandidate c) => new()
    {
        Id = c.Id, PlaceId = c.SourceId, Nome = c.Name, Endereco = c.Address,
        Telefone = c.Phone, Website = c.Website, DigitalPresence = c.DigitalPresence,
        OpportunityScore = c.OpportunityScore, EstimatedRevenueRange = c.EstimatedRevenueRange,
        Status = c.Status.ToString(), LeadId = c.LeadId, FirstSeenAt = c.FirstSeenAt,
        LastSeenAt = c.LastSeenAt, EnrichmentStatus = c.EnrichmentStatus.ToString(),
        LastEnrichedAt = c.LastEnrichedAt, EnrichmentSource = c.EnrichmentSource,
        EnrichmentConfidence = c.EnrichmentConfidence, SuggestedApproach = c.SuggestedApproach,
        RecentObservations = c.Observations.OrderByDescending(o => o.ObservedAt).Take(5)
            .Select(o => new ProspectObservationDto
            {
                FieldName = o.FieldName, PreviousValue = o.PreviousValue,
                ObservedValue = o.ObservedValue, Source = o.Source,
                Confidence = o.Confidence, ObservedAt = o.ObservedAt,
            }).ToList(),
    };

    internal static void ObserveChange(ProspectCandidate candidate, string fieldName,
        string? previousValue, string? observedValue, string source, int confidence, DateTime observedAt)
    {
        if (string.Equals(previousValue, observedValue, StringComparison.Ordinal)) return;
        candidate.Observations.Add(new ProspectObservation
        {
            Candidate = candidate,
            CandidateId = candidate.Id,
            FieldName = fieldName,
            PreviousValue = previousValue,
            ObservedValue = observedValue,
            Source = source,
            Confidence = confidence,
            ObservedAt = observedAt,
        });
    }

    internal static bool IsSuppressed(ProspectCandidateDto candidate,
        IReadOnlyCollection<ProspectSuppression> suppressions)
    {
        var source = $"OpenStreetMap:{candidate.PlaceId}".ToLowerInvariant();
        var phone = ProspectingCampaignService.NormalizePhone(candidate.Telefone);
        var domain = ProspectingCampaignService.NormalizeDomain(candidate.Website);
        return suppressions.Any(s =>
            (s.KeyType == ProspectSuppressionKeyType.SourceId && s.NormalizedValue == source) ||
            (phone is not null && s.KeyType == ProspectSuppressionKeyType.Phone && s.NormalizedValue == phone) ||
            (domain is not null && s.KeyType == ProspectSuppressionKeyType.Domain && s.NormalizedValue == domain));
    }

    private static int CalculateEnrichmentConfidence(ProspectCandidateDto candidate)
    {
        var confidence = 45;
        if (!string.IsNullOrWhiteSpace(candidate.Endereco)) confidence += 15;
        if (!string.IsNullOrWhiteSpace(candidate.Telefone)) confidence += 20;
        if (!string.IsNullOrWhiteSpace(candidate.Website)) confidence += 20;
        return Math.Min(confidence, 100);
    }

    /// <summary>Tenta cada instância pública do Overpass em ordem até uma
    /// responder com sucesso — a instância principal (overpass-api.de) é de
    /// graça mas sem SLA, então cai com frequência sob rate-limit. Só lança
    /// erro (o "não está respondendo" que o admin vê) se todas falharem.</summary>
    private async Task<string> QueryOverpassWithFallbackAsync(string query)
    {
        var client = _factory.CreateClient("osm");
        var content = new StringContent($"data={Uri.EscapeDataString(query)}", Encoding.UTF8, "application/x-www-form-urlencoded");

        Exception? lastError = null;
        foreach (var url in OverpassUrls)
        {
            try
            {
                var response = await client.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync();

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Overpass ({Url}) retornou {Status}: {Error}", url, response.StatusCode, error);
                lastError = new InvalidOperationException($"{url} → {response.StatusCode}");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Overpass ({Url}) falhou", url);
                lastError = ex;
            }
        }

        _logger.LogError(lastError, "Todas as instâncias Overpass falharam");
        throw new InvalidOperationException("Falha ao buscar no OpenStreetMap — tenta de novo em instantes.");
    }

    private sealed record ResolvedLocation(
        (double Sul, double Oeste, double Norte, double Leste) Bbox,
        long? AreaId);

    /// <summary>Reaproveita a geocodificação de qualquer pesquisa anterior na
    /// mesma cidade. Isso evita chamar o Nominatim uma vez por categoria e
    /// cumpre a exigência de cache da instância pública.</summary>
    private async Task<ResolvedLocation> ResolveLocationAsync(string cidade)
    {
        var normalizedCity = NormalizeCacheKey("", cidade).Split('|')[0];
        var previous = (await _catalog.ProspectingSearches.AsNoTracking()
                .Where(s => s.South != 0 && s.North != 0)
                .OrderByDescending(s => s.RefreshedAt)
                .ToListAsync())
            .FirstOrDefault(s => NormalizeCacheKey("", s.City).StartsWith(normalizedCity + "|", StringComparison.Ordinal));

        return previous is not null
            ? new ResolvedLocation((previous.South, previous.West, previous.North, previous.East), previous.OsmAreaId)
            : await GeocodeCityAsync(cidade);
    }

    /// <summary>Resolve "cidade, UF" para a área administrativa do OSM e um
    /// bounding box de fallback.</summary>
    private async Task<ResolvedLocation> GeocodeCityAsync(string cidade)
    {
        var client = _factory.CreateClient("osm");
        var url = $"{NominatimUrl}?q={Uri.EscapeDataString(cidade)}&format=json&addressdetails=1&limit=1";
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Falha ao localizar a cidade informada.");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (doc.RootElement.GetArrayLength() == 0)
            throw new ArgumentException($"Cidade '{cidade}' não encontrada — confira a grafia.");

        // Nominatim retorna boundingbox como [sul, norte, oeste, leste] (nessa ordem exata).
        var bbox = doc.RootElement[0].GetProperty("boundingbox");
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var sul   = double.Parse(bbox[0].GetString()!, culture);
        var norte = double.Parse(bbox[1].GetString()!, culture);
        var oeste = double.Parse(bbox[2].GetString()!, culture);
        var leste = double.Parse(bbox[3].GetString()!, culture);
        long? areaId = null;
        var result = doc.RootElement[0];
        if (result.TryGetProperty("osm_type", out var type) && type.GetString() == "relation" &&
            result.TryGetProperty("osm_id", out var id))
            areaId = 3_600_000_000L + id.GetInt64();

        return new ResolvedLocation((sul, oeste, norte, leste), areaId);
    }

    internal static string BuildOverpassQuery(string categoria,
        (double Sul, double Oeste, double Norte, double Leste) bbox, long? areaId = null)
    {
        // Overpass QL espera bbox na ordem (sul,oeste,norte,leste) — inverter
        // norte/oeste aqui faz o Overpass ler uma longitude no lugar da
        // latitude norte e rejeitar toda query com "n must be >= s" (longitude
        // é quase sempre bem menor que a latitude sul), derrubando 100% das
        // buscas independente de cidade/categoria.
        var bboxStr = $"{bbox.Sul.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                      $"{bbox.Oeste.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                      $"{bbox.Norte.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                      $"{bbox.Leste.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        var tag = ResolveTagOsm(categoria);
        var normalized = categoria.Trim().ToLowerInvariant();
        var filtro = normalized is "todos" or "todos os negocios" or "todos os negócios"
            ? "[~\"^(shop|amenity|office|craft|tourism|leisure)$\"~\".\"][\"name\"]"
            : tag is { } t
            ? $"[\"{t.Tag}\"=\"{t.Value}\"]"
            // Fallback: nenhuma palavra da frase bateu com o dicionário, busca
            // qualquer comércio/serviço cujo nome contenha o termo buscado.
            : $"[~\"^(shop|amenity|office|craft|tourism|leisure)$\"~\".\"][\"name\"~\"{EscapeOverpassRegex(categoria.Trim())}\",i]";

        var scope = areaId.HasValue ? "(area.searchArea)" : $"({bboxStr})";
        var areaDeclaration = areaId.HasValue ? $"area({areaId.Value})->.searchArea;" : string.Empty;

        return $"""
            [out:json][timeout:25];
            {areaDeclaration}
            nwr{filtro}{scope};
            out tags center;
            """;
    }

    private static string EscapeOverpassRegex(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("[", "\\[", StringComparison.Ordinal)
        .Replace("]", "\\]", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal)
        .Replace("{", "\\{", StringComparison.Ordinal)
        .Replace("}", "\\}", StringComparison.Ordinal);

    /// <summary>Acha o tag OSM pra categoria digitada: primeiro tenta a frase
    /// inteira (ex: "petshop"), depois cada palavra isolada (ex: "loja de
    /// roupas" → "loja", "de", "roupas" → bate em "roupas"). Sem usar essa
    /// segunda tentativa, qualquer frase natural que não fosse exatamente uma
    /// chave do dicionário caía no fallback por nome — que quase nunca acha
    /// nada, já que estabelecimentos raramente têm o tipo de negócio como
    /// nome literal no OSM.</summary>
    private static (string Tag, string Value)? ResolveTagOsm(string categoria)
    {
        var termoNormalizado = categoria.Trim().ToLowerInvariant();
        if (CategoriaParaTagOsm.TryGetValue(termoNormalizado, out var tagFraseInteira))
            return tagFraseInteira;

        foreach (var palavra in termoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (CategoriaParaTagOsm.TryGetValue(palavra, out var tagPalavra))
                return tagPalavra;
        }

        return null;
    }

    private static string? GetTag(JsonElement tags, string key) =>
        tags.TryGetProperty(key, out var v) ? v.GetString() : null;

    private static string? BuildEndereco(JsonElement tags)
    {
        var rua     = GetTag(tags, "addr:street");
        var numero  = GetTag(tags, "addr:housenumber");
        var cidade  = GetTag(tags, "addr:city");
        if (rua is null && cidade is null) return null;

        var partes = new[] { rua is not null ? $"{rua}{(numero is not null ? $", {numero}" : "")}" : null, cidade }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" — ", partes);
    }

    /// <summary>Sem site = "SemSite" (maior oportunidade). Com site, faz um GET
    /// simples e procura assinatura de plataforma de e-commerce conhecida no
    /// HTML — se achar, "ECommerce"; senão, "SiteLegado". Nunca lança: falha
    /// de rede (ou destino não permitido) vira "SiteLegado" (mais conservador
    /// que assumir e-commerce).
    ///
    /// A URL vem do OpenStreetMap — dado editável por qualquer pessoa, então
    /// não confiável (alguém podia cadastrar um site apontando pra rede
    /// interna), por isso só aceita http/https e segue redirect manualmente
    /// revalidando cada hop; a proteção contra IP privado/interno acontece no
    /// ConnectCallback do HttpClient "prospecting-site-check" (ver SafeOutboundHttp).</summary>
    private async Task<string> ClassifyDigitalPresenceAsync(string? website)
    {
        if (!SafeOutboundHttp.IsPublicHttpUrl(website, out var uri))
            return "SemSite";

        try
        {
            var client = _factory.CreateClient("prospecting-site-check");
            var current = uri!;

            for (var hop = 0; hop < 3; hop++)
            {
                var response = await client.GetAsync(current);

                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is not null)
                {
                    var next = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(current, response.Headers.Location);

                    if (next.Scheme != Uri.UriSchemeHttp && next.Scheme != Uri.UriSchemeHttps)
                        return "SiteLegado";

                    current = next;
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync();
                return EcommerceSignatures.Any(sig => html.Contains(sig, StringComparison.OrdinalIgnoreCase))
                    ? "ECommerce"
                    : "SiteLegado";
            }

            return "SiteLegado"; // redirect demais — desiste e classifica conservador
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao checar site {Website} — classificando como SiteLegado", website);
            return "SiteLegado";
        }
    }

    /// <summary>Score 0-100: até 40 pts por presença digital fraca (quem não
    /// tem site precisa mais da gente) + até 60 pts pela "completude" do
    /// cadastro no OSM (telefone, horário de funcionamento, endereço completo)
    /// como proxy de quão estabelecido/rastreável é o negócio — o OSM não tem
    /// nota/avaliações como o Google Maps, então esse é o substituto.</summary>
    internal static int CalculateOpportunityScore(bool temTelefone, bool temHorario, bool temEndereco, string digitalPresence)
    {
        var digitalScore = digitalPresence switch
        {
            "SemSite"    => 40,
            "SiteLegado" => 20,
            _            => 0,
        };

        var completeness = (temTelefone ? 20 : 0) + (temHorario ? 20 : 0) + (temEndereco ? 20 : 0);

        return Math.Clamp(digitalScore + completeness, 0, 100);
    }

    /// <summary>Faixa grosseira usando a mesma "completude" do cadastro no OSM
    /// como proxy de porte (sem nota/avaliações disponíveis) — não é dado
    /// financeiro real, só heurística pra priorizar quem abordar primeiro.</summary>
    internal static string EstimateRevenueRangeHeuristic(bool temTelefone, bool temHorario, bool temEndereco)
    {
        var completeness = (temTelefone ? 20 : 0) + (temHorario ? 20 : 0) + (temEndereco ? 20 : 0);
        return completeness switch
        {
            < 20 => "R$5-15k/mês (estimativa)",
            < 40 => "R$15-40k/mês (estimativa)",
            < 60 => "R$40-100k/mês (estimativa)",
            _    => "R$100k+/mês (estimativa)",
        };
    }

    public async Task<ProspectingEnrichResponse> EnrichWithAiAsync(ProspectingEnrichRequest request)
    {
        var apiKey = ResolveGeminiApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Chave do Gemini não configurada na plataforma.");

        var persistedCandidate = request.CandidateId is Guid candidateId
            ? await _catalog.ProspectCandidates.FirstOrDefaultAsync(c => c.Id == candidateId)
            : null;

        var prompt = $$"""
            Você está analisando um possível cliente para uma plataforma de ERP/PDV pra lojas e varejo.
            Dados públicos do negócio (do OpenStreetMap, não invente nada além disso):
            - Nome: {{request.Nome}}
            - Categoria: {{request.Categoria ?? "não informada"}}
            - Endereço: {{request.Endereco ?? "não informado"}}
            - Presença digital: {{request.DigitalPresence}}

            Não estime faturamento, porte, CNPJ ou qualquer dado ausente.
            Responda em JSON estrito, sem markdown, só o objeto:
            {"abordagemSugerida": "2-3 frases de como abordar esse lead especificamente, mencionando apenas o que os dados informados sustentam e o que a plataforma resolveria pra esse tipo de negócio"}
            """;

        var result = new ProspectingEnrichResponse
        {
            // A IA não cria dado financeiro. Mantemos a heurística
            // determinística e explicitamente rotulada que já estava salva.
            EstimatedRevenueRange = persistedCandidate?.EstimatedRevenueRange
                ?? EstimateRevenueRangeHeuristic(false, false, false),
            AbordagemSugerida = await GenerateSuggestedApproachAsync(prompt, apiKey),
        };

        if (persistedCandidate is not null)
        {
            UpdateCandidateEnrichment(persistedCandidate, result.AbordagemSugerida, DateTime.UtcNow);
            await _catalog.SaveChangesAsync();
        }

        return result;
    }

    public async Task<ProspectingEnrichResponse?> EnrichLeadWithAiAsync(Guid leadId)
    {
        var apiKey = ResolveGeminiApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Chave do Gemini não configurada na plataforma.");

        var lead = await _catalog.Leads.FirstOrDefaultAsync(l => l.Id == leadId);
        if (lead is null) return null;

        var candidate = await _catalog.ProspectCandidates
            .Include(c => c.Search)
            .FirstOrDefaultAsync(c => c.LeadId == leadId);

        var prompt = $$"""
            Você está analisando um lead já captado por uma plataforma de ERP/PDV.
            Use somente os dados abaixo; não invente CNPJ, faturamento, porte,
            endereço, segmento ou qualquer informação ausente.
            - Nome: {{lead.Nome}}
            - Origem: {{lead.Origem}}
            - Contexto informado pelo lead: {{lead.Mensagem ?? "não informado"}}
            - Categoria observada: {{candidate?.Search.Category ?? "não informada"}}
            - Endereço público observado: {{candidate?.Address ?? "não informado"}}
            - Presença digital: {{lead.DigitalPresence ?? candidate?.DigitalPresence ?? "não informada"}}
            - Pontuação interna de oportunidade: {{lead.OpportunityScore?.ToString() ?? "não informada"}}

            Responda em JSON estrito, sem markdown, só o objeto:
            {"abordagemSugerida": "2-3 frases de abordagem personalizada, mencionando apenas o que os dados sustentam e como a plataforma pode ajudar"}
            """;

        var abordagem = await GenerateSuggestedApproachAsync(prompt, apiKey);
        var now = DateTime.UtcNow;
        lead.AbordagemSugerida = abordagem;
        lead.UpdatedAt = now;

        if (candidate is not null)
            UpdateCandidateEnrichment(candidate, abordagem, now);

        await _catalog.SaveChangesAsync();
        return new ProspectingEnrichResponse
        {
            EstimatedRevenueRange = lead.EstimatedRevenueRange ?? string.Empty,
            AbordagemSugerida = abordagem,
        };
    }

    private static void UpdateCandidateEnrichment(
        ProspectCandidate candidate, string abordagem, DateTime observedAt)
    {
        ObserveChange(candidate, "SuggestedApproach", candidate.SuggestedApproach,
            abordagem, "Gemini", 50, observedAt);
        candidate.SuggestedApproach = abordagem;
        candidate.EnrichmentStatus = ProspectEnrichmentStatus.Updated;
        candidate.LastEnrichedAt = observedAt;
        candidate.EnrichmentSource = string.IsNullOrWhiteSpace(candidate.EnrichmentSource)
            ? "Gemini"
            : candidate.EnrichmentSource.Contains("Gemini", StringComparison.Ordinal)
                ? candidate.EnrichmentSource
                : $"{candidate.EnrichmentSource};Gemini";
    }

    private async Task<string> GenerateSuggestedApproachAsync(string prompt, string apiKey)
    {
        try
        {
            var rawJson = await CallGeminiWithFallbackAsync(prompt, apiKey);
            using var doc = JsonDocument.Parse(rawJson);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0)
                throw new JsonException("Resposta sem candidatos.");

            var text = candidates[0].GetProperty("content").GetProperty("parts")[0]
                .GetProperty("text").GetString() ?? "{}";

            // Gemini ainda pode envolver JSON em markdown apesar do MIME solicitado.
            text = text.Trim().Trim('`').Replace("json\n", "").Trim();
            using var parsed = JsonDocument.Parse(text);
            var abordagem = parsed.RootElement.GetProperty("abordagemSugerida").GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(abordagem))
                throw new JsonException("Resposta sem abordagem sugerida.");
            return abordagem;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IndexOutOfRangeException)
        {
            _logger.LogWarning(ex, "Resposta do Gemini fora do formato esperado na prospecção.");
            throw new InvalidOperationException(
                "A IA respondeu fora do formato esperado. Tente novamente em alguns instantes.", ex);
        }
    }

    private string? ResolveGeminiApiKey() =>
        _config["ProspectingSettings:GeminiApiKey"]?.Trim() is { Length: > 0 } dedicated
            ? dedicated
            : _config["GeminiSettings:ApiKey"]?.Trim();

    /// <summary>
    /// Chama o Gemini percorrendo <see cref="GeminiModels"/> até um responder, e
    /// devolve o JSON cru da resposta.
    ///
    /// Loga o CORPO do erro, não só o status: a versão anterior logava apenas
    /// "Gemini (prospecção) retornou {Status}" e descartava a resposta, que é
    /// justamente onde o Google explica o motivo ("model not found",
    /// "quota exceeded", "API key not valid"). Sem isso a feature falhava com
    /// um toast genérico e zero pista no log — foi o que travou o diagnóstico.
    ///
    /// A chave nunca entra em log: vai no header (x-goog-api-key), não na URL,
    /// e o corpo de erro do Google não a devolve.
    /// </summary>
    private async Task<string> CallGeminiWithFallbackAsync(string prompt, string apiKey)
    {
        var client = _factory.CreateClient("gemini");
        var body = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json" },
        });

        var falhas = new List<string>();

        foreach (var model in GeminiModels)
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, GeminiUrlFor(model))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            requestMessage.Headers.Add("x-goog-api-key", apiKey);

            HttpResponseMessage response;
            string conteudo;
            try
            {
                response = await client.SendAsync(requestMessage);
                conteudo = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Falha de rede ao chamar Gemini com o modelo {Model}.", model);
                falhas.Add($"{model}: rede");
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    if (model != GeminiModels[0])
                        _logger.LogWarning(
                            "Enriquecimento de lead caiu no modelo de fallback {Model} — o preferido falhou.", model);

                    return conteudo;
                }

                _logger.LogError(
                    "Gemini (prospecção) recusou o modelo {Model}: {Status} — {Corpo}",
                    model, (int)response.StatusCode, Truncar(conteudo, 500));

                falhas.Add($"{model}: {(int)response.StatusCode}");
            }
        }

        // Mensagem com os status por modelo: o dono da plataforma é quem usa esta
        // tela, e "todos os modelos recusaram, 404 nos dois" é acionável de
        // imediato — bem diferente do "Falha ao enriquecer com IA" anterior.
        throw new InvalidOperationException(
            $"A IA de enriquecimento não respondeu ({string.Join("; ", falhas)}). Confira a chave e o log da API.");
    }

    private static string Truncar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max] + "…";
}
