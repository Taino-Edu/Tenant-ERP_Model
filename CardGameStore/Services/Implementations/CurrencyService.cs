// =============================================================================
// CurrencyService.cs — Cotação USD/BRL em tempo real (AwesomeAPI)
// Cache em memória com TTL de 1 hora para evitar calls excessivos.
// API: https://economia.awesomeapi.com.br/json/last/USD-BRL (gratuita, sem key)
// =============================================================================

using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace CardGameStore.Services.Implementations;

public class CurrencyService
{
    private readonly IHttpClientFactory    _factory;
    private readonly IMemoryCache          _cache;
    private readonly ILogger<CurrencyService> _logger;

    private const string CacheKey = "usd_brl_rate";

    public CurrencyService(IHttpClientFactory factory, IMemoryCache cache, ILogger<CurrencyService> logger)
    {
        _factory = factory;
        _cache   = cache;
        _logger  = logger;
    }

    /// <summary>Retorna a cotação USD → BRL atual. Usa cache de 1 hora.</summary>
    public async Task<decimal> GetUsdToBrlAsync()
    {
        if (_cache.TryGetValue(CacheKey, out decimal cached))
            return cached;

        try
        {
            var client   = _factory.CreateClient("AwesomeApi");
            var response = await client.GetAsync("/json/last/USD-BRL");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("USDBRL", out var usdBrl) &&
                usdBrl.TryGetProperty("bid", out var bid) &&
                decimal.TryParse(bid.GetString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var rate))
            {
                _cache.Set(CacheKey, rate, TimeSpan.FromHours(1));
                _logger.LogInformation("Cotação USD/BRL atualizada: {Rate}", rate);
                return rate;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao buscar cotação USD/BRL — usando fallback");
        }

        // Fallback: retorna valor do cache anterior ou estimativa conservadora
        return _cache.TryGetValue(CacheKey, out decimal fallback) ? fallback : 5.80m;
    }
}
