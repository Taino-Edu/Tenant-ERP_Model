using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CardGameStore.Services.Implementations;

public class InterSyncService
{
    private readonly IServiceScopeFactory     _scopeFactory;
    private readonly EncryptionService        _enc;
    private readonly IConfiguration           _config;
    private readonly ILogger<InterSyncService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public InterSyncService(
        IServiceScopeFactory scopeFactory,
        EncryptionService enc,
        IConfiguration config,
        ILogger<InterSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _enc          = enc;
        _config       = config;
        _logger       = logger;
    }

    public bool IsConfigured(IntegrationConfig cfg) =>
        !string.IsNullOrWhiteSpace(cfg.ClientId) &&
        !string.IsNullOrWhiteSpace(cfg.ClientSecret) &&
        CertificateExists();

    public bool CertificateExists()
    {
        var crt = _config["Inter:CertificatePath"];
        var key = _config["Inter:KeyPath"];
        return File.Exists(crt) && File.Exists(key);
    }

    // ── Sincroniza extrato dos últimos N dias ─────────────────────────────────
    public async Task<InterSyncResult> SyncAsync(int days = 7)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cfg = await db.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.Source == "inter");

        if (cfg is null || !IsConfigured(cfg))
            return new InterSyncResult { Skipped = true, Reason = "Inter não configurado (Client ID, Client Secret ou certificado ausente)." };

        try
        {
            var clientSecret = _enc.Decrypt(cfg.ClientSecret!);
            var token        = await GetTokenAsync(cfg.ClientId!, clientSecret);

            var fim    = DateOnly.FromDateTime(DateTime.Now);
            var inicio = fim.AddDays(-days);

            var transactions = await FetchExtratoAsync(token, inicio, fim);

            int imported = 0, skipped = 0;
            foreach (var t in transactions)
            {
                var exists = await db.ExternalTransactions
                    .AnyAsync(x => x.Source == "inter" && x.ExternalId == t.CodigoTransacao);

                if (exists) { skipped++; continue; }

                var isIncome = t.Tipo == "C"; // C = Crédito, D = Débito
                db.ExternalTransactions.Add(new ExternalTransaction
                {
                    Source      = "inter",
                    ExternalId  = t.CodigoTransacao,
                    Type        = isIncome ? "income" : "expense",
                    Amount      = Math.Abs(t.Valor),
                    Description = t.Descricao ?? t.TipoTransacao ?? "Transação Inter",
                    DueDate     = t.DataTransacao.ToDateTime(TimeOnly.MinValue),
                    PaidAt      = t.DataTransacao.ToDateTime(TimeOnly.MinValue),
                    Status      = "paid",
                    Category    = t.TipoTransacao,
                });
                imported++;
            }

            await db.SaveChangesAsync();

            cfg.LastSyncAt = DateTime.UtcNow;
            cfg.UpdatedAt  = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return new InterSyncResult { Imported = imported, Duplicates = skipped };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao sincronizar extrato Inter");
            return new InterSyncResult { Error = ex.Message };
        }
    }

    // ── OAuth2 Client Credentials com mTLS ───────────────────────────────────
    private async Task<string> GetTokenAsync(string clientId, string clientSecret)
    {
        using var http = BuildMtlsClient();

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"]    = "client_credentials",
            ["scope"]         = "extrato.read",
        });

        var resp = await http.PostAsync("https://cdpj.partners.bancointer.com.br/oauth/v2/token", body);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<InterTokenResponse>(_json)
            ?? throw new InvalidOperationException("Resposta de token inválida.");

        return json.AccessToken;
    }

    // ── Extrato ───────────────────────────────────────────────────────────────
    private async Task<List<InterLancamento>> FetchExtratoAsync(string token, DateOnly inicio, DateOnly fim)
    {
        using var http = BuildMtlsClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"https://cdpj.partners.bancointer.com.br/banking/v3/extrato?dataInicio={inicio:yyyy-MM-dd}&dataFim={fim:yyyy-MM-dd}";
        var resp = await http.GetAsync(url);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<InterExtratoResponse>(_json);
        return body?.Transacoes ?? [];
    }

    // ── HttpClient com certificado mTLS do Inter ──────────────────────────────
    private HttpClient BuildMtlsClient()
    {
        var certPath = _config["Inter:CertificatePath"]!;
        var keyPath  = _config["Inter:KeyPath"]!;

        var cert    = X509Certificate2.CreateFromPemFile(certPath, keyPath);
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(cert);

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }
}

// ── Background service — sincroniza a cada hora ───────────────────────────────
public class InterSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InterSyncBackgroundService> _logger;

    public InterSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<InterSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // aguarda 2 min após startup para não competir com EnsureCreated
        await Task.Delay(TimeSpan.FromMinutes(2), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc         = scope.ServiceProvider.GetRequiredService<InterSyncService>();
                var result      = await svc.SyncAsync(days: 7);

                if (!result.Skipped)
                    _logger.LogInformation(
                        "Inter sync: {imported} importadas, {dup} duplicatas, erro={error}",
                        result.Imported, result.Duplicates, result.Error ?? "-");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no background sync do Inter");
            }

            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}

// ── DTOs internos da API Inter ────────────────────────────────────────────────
internal record InterTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")]   int    ExpiresIn);

internal record InterExtratoResponse(
    [property: JsonPropertyName("transacoes")] List<InterLancamento>? Transacoes);

internal record InterLancamento(
    [property: JsonPropertyName("codigoTransacao")] string?  CodigoTransacao,
    [property: JsonPropertyName("dataTransacao")]   DateOnly DataTransacao,
    [property: JsonPropertyName("tipo")]            string?  Tipo,
    [property: JsonPropertyName("tipoTransacao")]   string?  TipoTransacao,
    [property: JsonPropertyName("descricao")]       string?  Descricao,
    [property: JsonPropertyName("valor")]           decimal  Valor);

public record InterSyncResult
{
    public bool    Skipped    { get; init; }
    public string? Reason     { get; init; }
    public int     Imported   { get; init; }
    public int     Duplicates { get; init; }
    public string? Error      { get; init; }
}
