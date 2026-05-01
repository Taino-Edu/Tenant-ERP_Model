// =============================================================================
// TcgApiClient.cs — Implementação do cliente HTTP para a API TCG externa (apitcg.com)
// Adapte os endpoints conforme a documentação real em https://docs.apitcg.com/
// =============================================================================
using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using System.Text.Json;

namespace CardGameStore.Services.Implementations;

/// <summary>
/// Cliente HTTP que faz as chamadas reais à API TCG externa.
/// Separado do TcgService (que gerencia o cache) seguindo o princípio SRP.
/// </summary>
public class TcgApiClient : ITcgApiClient
{
    private readonly HttpClient              _httpClient;
    private readonly ILogger<TcgApiClient>   _logger;

    // Opções de desserialização case-insensitive (a maioria das APIs usa camelCase)
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TcgApiClient(IHttpClientFactory httpClientFactory, ILogger<TcgApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TcgApi");
        _logger     = logger;
    }

    /// <inheritdoc/>
    public async Task<TcgApiCardResponse?> FetchCardByIdAsync(string cardId)
    {
        try
        {
            // TODO: Ajuste o endpoint conforme docs.apitcg.com
            var response = await _httpClient.GetAsync($"/v1/cards/{cardId}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("API TCG retornou {Status} para carta {Id}", response.StatusCode, cardId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TcgApiCardResponse>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar carta {Id} na API TCG", cardId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<TcgApiSearchResponse> SearchCardsAsync(string name, string? game, int page, int pageSize)
    {
        try
        {
            // Monta a query string — ajuste conforme a API real
            var query  = Uri.EscapeDataString(name);
            var gameFilter = game != null ? $"&game={Uri.EscapeDataString(game)}" : string.Empty;
            var url    = $"/v1/cards/search?q={query}{gameFilter}&page={page}&pageSize={pageSize}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TcgApiSearchResponse>(json, _jsonOptions)
                   ?? new TcgApiSearchResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pesquisar '{Name}' na API TCG", name);
            return new TcgApiSearchResponse();
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TcgSetDto>> FetchSetsAsync(string game)
    {
        try
        {
            var url      = $"/v1/sets?game={Uri.EscapeDataString(game)}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<TcgSetDto>>(json, _jsonOptions)
                   ?? new List<TcgSetDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar sets do jogo '{Game}'", game);
            return Enumerable.Empty<TcgSetDto>();
        }
    }
}
