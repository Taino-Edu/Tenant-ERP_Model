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
// Chave de API distinta só pra IA (nunca a mesma variável do resto do sistema):
//   - ProspectingSettings:GeminiApiKey → enriquecimento por IA, separada da
//     GeminiSettings:ApiKey usada pelo Assistente de IA de cada loja, pra não
//     misturar custo/cota de uma feature com a outra.
// =============================================================================

using System.Text;
using System.Text.Json;
using CardGameStore.Common;
using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;

namespace CardGameStore.Services.Implementations;

public class ProspectingService : IProspectingService
{
    private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";
    private const string OverpassUrl  = "https://overpass-api.de/api/interpreter";
    private const string GeminiUrl    = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    // Nominatim/Overpass pedem um User-Agent identificando a aplicação (uso
    // deles é público e gratuito, mas com política de uso justo) — nunca usar
    // o default do HttpClient.
    private const string UserAgent = "TenantERP-Prospecting/1.0";

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

    public ProspectingService(IHttpClientFactory factory, IConfiguration config, ILogger<ProspectingService> logger)
    {
        _factory = factory;
        _config  = config;
        _logger  = logger;
    }

    public async Task<List<ProspectCandidateDto>> SearchAsync(string categoria, string cidade)
    {
        var bbox = await GeocodeCityAsync(cidade);
        var query = BuildOverpassQuery(categoria, bbox);

        var client = _factory.CreateClient("osm");
        var response = await client.PostAsync(OverpassUrl,
            new StringContent($"data={Uri.EscapeDataString(query)}", Encoding.UTF8, "application/x-www-form-urlencoded"));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Overpass API retornou {Status}: {Error}", response.StatusCode, error);
            throw new InvalidOperationException("Falha ao buscar no OpenStreetMap — tenta de novo em instantes.");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
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

        return candidates.ToList();
    }

    /// <summary>Resolve "cidade, UF" pra um bounding box [sul, oeste, norte,
    /// leste] via Nominatim (geocodificador do OSM) — necessário porque o
    /// Overpass busca por coordenadas, não por nome de cidade.</summary>
    private async Task<(double Sul, double Oeste, double Norte, double Leste)> GeocodeCityAsync(string cidade)
    {
        var client = _factory.CreateClient("osm");
        var url = $"{NominatimUrl}?q={Uri.EscapeDataString(cidade)}&format=json&limit=1";
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
        return (sul, oeste, norte, leste);
    }

    private static string BuildOverpassQuery(string categoria, (double Sul, double Oeste, double Norte, double Leste) bbox)
    {
        var bboxStr = $"{bbox.Sul.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                      $"{bbox.Norte.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                      $"{bbox.Oeste.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                      $"{bbox.Leste.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        var termoNormalizado = categoria.Trim().ToLowerInvariant();
        var filtro = CategoriaParaTagOsm.TryGetValue(termoNormalizado, out var tag)
            ? $"[\"{tag.Tag}\"=\"{tag.Value}\"]"
            // Fallback: sem mapeamento exato, busca qualquer comércio/serviço cujo
            // nome contenha o termo buscado.
            : $"[~\"^(shop|amenity|office)$\"~\".\"][\"name\"~\"{categoria.Trim()}\",i]";

        return $"""
            [out:json][timeout:25];
            (
              node{filtro}({bboxStr});
              way{filtro}({bboxStr});
            );
            out center 60;
            """;
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
        var apiKey = _config["ProspectingSettings:GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ProspectingSettings:GeminiApiKey não configurada.");

        var prompt = $$"""
            Você está analisando um possível cliente para uma plataforma de ERP/PDV pra lojas e varejo.
            Dados públicos do negócio (do OpenStreetMap, não invente nada além disso):
            - Nome: {{request.Nome}}
            - Categoria: {{request.Categoria ?? "não informada"}}
            - Endereço: {{request.Endereco ?? "não informado"}}
            - Presença digital: {{request.DigitalPresence}}

            Responda em JSON estrito, sem markdown, só o objeto:
            {"estimatedRevenueRange": "faixa curta tipo 'R$20-50k/mês' (máximo 50 caracteres, SEM explicação junto)", "abordagemSugerida": "2-3 frases de como abordar esse lead especificamente, mencionando o que a plataforma resolveria pra esse tipo de negócio"}
            """;

        var client = _factory.CreateClient("gemini");
        var body = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
        });

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, GeminiUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        requestMessage.Headers.Add("x-goog-api-key", apiKey);
        var response = await client.SendAsync(requestMessage);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini (prospecção) retornou {Status}", response.StatusCode);
            throw new InvalidOperationException("Falha ao gerar enriquecimento por IA.");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "{}";

        // Gemini às vezes envolve o JSON em ```json ... ``` mesmo pedindo pra não fazer isso.
        text = text.Trim().Trim('`').Replace("json\n", "").Trim();

        try
        {
            using var parsed = JsonDocument.Parse(text);
            var revenueRange = parsed.RootElement.GetProperty("estimatedRevenueRange").GetString() ?? "";

            // Lead.EstimatedRevenueRange tem limite de 60 caracteres — a IA às
            // vezes ignora o pedido de resposta curta e manda a explicação
            // junto. Em vez de deixar a confirmação do lead falhar depois por
            // causa disso, cai pra heurística sem IA (que já respeita o limite).
            // Sem os sinais de completude aqui (não persistidos na request),
            // usa o pior caso como fallback conservador.
            if (revenueRange.Length > 60)
                revenueRange = EstimateRevenueRangeHeuristic(false, false, false);

            return new ProspectingEnrichResponse
            {
                EstimatedRevenueRange = revenueRange,
                AbordagemSugerida     = parsed.RootElement.GetProperty("abordagemSugerida").GetString() ?? "",
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Resposta do Gemini fora do formato esperado: {Text}", text);
            throw new InvalidOperationException("A IA retornou uma resposta em formato inesperado — tenta de novo.");
        }
    }
}
