// =============================================================================
// ProspectingService.cs — Busca de possíveis clientes via Google Places API
// (Text Search), com classificação heurística sem IA (presença digital, score
// de oportunidade, faixa de faturamento) e enriquecimento opcional via Gemini.
//
// Duas chaves de API distintas por design (nunca a mesma variável):
//   - Places:ApiKey            → busca de negócios (Google Places API)
//   - ProspectingSettings:GeminiApiKey → enriquecimento por IA, separada da
//     GeminiSettings:ApiKey usada pelo Assistente de IA de cada loja, pra não
//     misturar custo/cota de uma feature com a outra.
// =============================================================================

using System.Text;
using System.Text.Json;
using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;

namespace CardGameStore.Services.Implementations;

public class ProspectingService : IProspectingService
{
    private const string PlacesUrl = "https://places.googleapis.com/v1/places:searchText";
    private const string GeminiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

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
        var apiKey = _config["Places:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Places:ApiKey não configurada — prospecção precisa de uma chave do Google Places API.");

        var client = _factory.CreateClient("places");
        client.DefaultRequestHeaders.Remove("X-Goog-Api-Key");
        client.DefaultRequestHeaders.Add("X-Goog-Api-Key", apiKey);
        client.DefaultRequestHeaders.Remove("X-Goog-FieldMask");
        client.DefaultRequestHeaders.Add("X-Goog-FieldMask",
            "places.id,places.displayName,places.formattedAddress,places.nationalPhoneNumber,places.websiteUri,places.rating,places.userRatingCount");

        var body = JsonSerializer.Serialize(new { textQuery = $"{categoria} em {cidade}" });
        var response = await client.PostAsync(PlacesUrl, new StringContent(body, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Places API retornou {Status}: {Error}", response.StatusCode, error);
            throw new InvalidOperationException("Falha ao buscar no Google Places — confira a chave configurada.");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var candidates = new List<ProspectCandidateDto>();

        if (!doc.RootElement.TryGetProperty("places", out var places))
            return candidates;

        foreach (var place in places.EnumerateArray())
        {
            var nome     = place.TryGetProperty("displayName", out var dn) && dn.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
            var website  = place.TryGetProperty("websiteUri", out var w) ? w.GetString() : null;
            var rating   = place.TryGetProperty("rating", out var r) ? r.GetDouble() : (double?)null;
            var reviews  = place.TryGetProperty("userRatingCount", out var rc) ? rc.GetInt32() : (int?)null;

            var digitalPresence = await ClassifyDigitalPresenceAsync(website);

            candidates.Add(new ProspectCandidateDto
            {
                PlaceId               = place.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                Nome                  = nome,
                Endereco              = place.TryGetProperty("formattedAddress", out var addr) ? addr.GetString() : null,
                Telefone              = place.TryGetProperty("nationalPhoneNumber", out var ph) ? ph.GetString() : null,
                Website               = website,
                Rating                = rating,
                ReviewCount           = reviews,
                DigitalPresence       = digitalPresence,
                OpportunityScore      = CalculateOpportunityScore(rating, reviews, digitalPresence),
                EstimatedRevenueRange = EstimateRevenueRangeHeuristic(reviews),
            });
        }

        return candidates;
    }

    /// <summary>Sem site = "SemSite" (maior oportunidade). Com site, faz um GET
    /// simples e procura assinatura de plataforma de e-commerce conhecida no
    /// HTML — se achar, "ECommerce"; senão, "SiteLegado". Nunca lança: falha
    /// de rede vira "SiteLegado" (mais conservador que assumir e-commerce).</summary>
    private async Task<string> ClassifyDigitalPresenceAsync(string? website)
    {
        if (string.IsNullOrWhiteSpace(website))
            return "SemSite";

        try
        {
            var client = _factory.CreateClient("prospecting-site-check");
            var response = await client.GetAsync(website);
            var html = await response.Content.ReadAsStringAsync();
            return EcommerceSignatures.Any(sig => html.Contains(sig, StringComparison.OrdinalIgnoreCase))
                ? "ECommerce"
                : "SiteLegado";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao checar site {Website} — classificando como SiteLegado", website);
            return "SiteLegado";
        }
    }

    /// <summary>Score 0-100: até 40 pts por presença digital fraca (quem não
    /// tem site precisa mais da gente), até 20 pts pela nota (legitimidade do
    /// negócio), até 40 pts pelo volume de avaliações (proxy de porte/atividade).</summary>
    internal static int CalculateOpportunityScore(double? rating, int? reviewCount, string digitalPresence)
    {
        var digitalScore = digitalPresence switch
        {
            "SemSite"    => 40,
            "SiteLegado" => 20,
            _            => 0,
        };

        var ratingScore = rating.HasValue ? (int)Math.Round(Math.Clamp(rating.Value, 0, 5) / 5.0 * 20) : 0;

        var reviews = reviewCount ?? 0;
        var reviewScore = reviews switch
        {
            < 10  => 5,
            < 50  => 15,
            < 200 => 25,
            _     => 40,
        };

        return Math.Clamp(digitalScore + ratingScore + reviewScore, 0, 100);
    }

    /// <summary>Faixa grosseira usando nº de avaliações como proxy de porte —
    /// não é dado financeiro real, só heurística pra priorizar quem abordar
    /// primeiro. Nomeada "heuristic" de propósito pra nunca ser confundida com
    /// a estimativa mais fina que o enriquecimento por IA pode gerar.</summary>
    internal static string EstimateRevenueRangeHeuristic(int? reviewCount)
    {
        var reviews = reviewCount ?? 0;
        return reviews switch
        {
            < 20  => "R$5-15k/mês (estimativa)",
            < 100 => "R$15-40k/mês (estimativa)",
            < 300 => "R$40-100k/mês (estimativa)",
            _     => "R$100k+/mês (estimativa)",
        };
    }

    public async Task<ProspectingEnrichResponse> EnrichWithAiAsync(ProspectingEnrichRequest request)
    {
        var apiKey = _config["ProspectingSettings:GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ProspectingSettings:GeminiApiKey não configurada.");

        var prompt = $$"""
            Você está analisando um possível cliente para uma plataforma de ERP/PDV pra lojas e varejo.
            Dados públicos do negócio (do Google Maps, não invente nada além disso):
            - Nome: {{request.Nome}}
            - Categoria: {{request.Categoria ?? "não informada"}}
            - Endereço: {{request.Endereco ?? "não informado"}}
            - Nota: {{(request.Rating?.ToString("0.0") ?? "sem nota")}}
            - Nº de avaliações: {{(request.ReviewCount?.ToString() ?? "0")}}
            - Presença digital: {{request.DigitalPresence}}

            Responda em JSON estrito, sem markdown, só o objeto:
            {"estimatedRevenueRange": "faixa estimada tipo 'R$20-50k/mês' com uma frase curta do porquê", "abordagemSugerida": "2-3 frases de como abordar esse lead especificamente, mencionando o que a plataforma resolveria pra esse tipo de negócio"}
            """;

        var client = _factory.CreateClient("gemini");
        var body = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
        });

        var response = await client.PostAsync($"{GeminiUrl}?key={apiKey}", new StringContent(body, Encoding.UTF8, "application/json"));
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
            return new ProspectingEnrichResponse
            {
                EstimatedRevenueRange = parsed.RootElement.GetProperty("estimatedRevenueRange").GetString() ?? "",
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
