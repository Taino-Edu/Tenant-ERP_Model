// =============================================================================
// GeminiChatService.cs — Assistente IA usando Gemini 2.0 Flash (Google)
//
// Configuração:
//   GEMINI_API_KEY → chave do Google AI Studio (aistudio.google.com/apikey)
//
// Funciona sem a chave configurada — retorna mensagem amigável de erro.
// O sistema não quebra se o Gemini estiver indisponível.
// =============================================================================

using System.Net.Http.Json;
using System.Text.Json;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class GeminiChatService : IAiChatService
{
    private const string GEMINI_URL =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

    private readonly AppDbContext              _db;
    private readonly IVendaAvulsaService       _vendas;
    private readonly IHttpClientFactory        _http;
    private readonly IConfiguration            _config;
    private readonly ILogger<GeminiChatService> _logger;

    public GeminiChatService(
        AppDbContext db,
        IVendaAvulsaService vendas,
        IHttpClientFactory http,
        IConfiguration config,
        ILogger<GeminiChatService> logger)
    {
        _db     = db;
        _vendas = vendas;
        _http   = http;
        _config = config;
        _logger = logger;
    }

    public async Task<string> ChatAsync(string userMessage)
    {
        var apiKey = _config["GeminiSettings:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("GeminiChatService: GEMINI_API_KEY não configurado.");
            return "O assistente IA não está configurado. Peça ao administrador do sistema para adicionar a chave GEMINI_API_KEY.";
        }

        try
        {
            var contexto = await BuildContextAsync();
            var prompt   = BuildPrompt(userMessage, contexto);

            var client   = _http.CreateClient("gemini");
            var url      = $"{GEMINI_URL}?key={apiKey}";

            var payload = new
            {
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    temperature     = 0.3,
                    maxOutputTokens = 512,
                }
            };

            var response = await client.PostAsJsonAsync(url, payload);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini API erro {Status}: {Body}", response.StatusCode, err);
                return "Não consegui obter resposta do assistente agora. Tente novamente em instantes.";
            }

            using var doc    = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "Sem resposta.";

            return text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeminiChatService: erro inesperado ao chamar API.");
            return "Ocorreu um erro ao processar sua pergunta. Tente novamente.";
        }
    }

    // ── Contexto ──────────────────────────────────────────────────────────────

    // LGPD: Os dados de clientes enviados ao Gemini são anonimizados.
    // Nomes reais são substituídos por "Cliente #N" antes de serem transmitidos
    // à API externa do Google, preservando apenas dados financeiros necessários.
    private async Task<string> BuildContextAsync()
    {
        var agora      = DateTime.UtcNow;
        var hoje       = agora.Date;
        var ha30Dias   = hoje.AddDays(-30);

        // ── Vendas hoje ───────────────────────────────────────────────────────
        var vendasHojeComanda = await _db.Comandas
            .Where(c => c.ClosedAt >= hoje && c.Status == ComandaStatus.Fechada)
            .SumAsync(c => (decimal)c.TotalInCents);

        var todasVendas   = (await _vendas.GetRecentAsync(200)).ToList();
        var vendasHojeAvulsa = todasVendas
            .Where(v => v.SoldAt >= hoje)
            .Sum(v => (decimal)v.TotalInCents);

        var totalHoje = (vendasHojeComanda + vendasHojeAvulsa) / 100m;

        // ── Comandas ──────────────────────────────────────────────────────────
        var comandasAbertas = await _db.Comandas
            .CountAsync(c => c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento);

        // ── Ticket médio ──────────────────────────────────────────────────────
        var tickets = await _db.Comandas
            .Where(c => c.ClosedAt >= ha30Dias && c.TotalInCents > 0)
            .Select(c => (decimal)c.TotalInCents)
            .ToListAsync();
        var ticketMedio = tickets.Count > 0 ? tickets.Average() / 100m : 0;

        // ── Top produtos ──────────────────────────────────────────────────────
        var topProdutos = await _db.ComandaItems
            .Where(i => i.AddedAt >= ha30Dias)
            .GroupBy(i => i.ItemNameSnapshot)
            .Select(g => new { Nome = g.Key, Qtd = g.Sum(i => i.Quantity) })
            .OrderByDescending(t => t.Qtd)
            .Take(5)
            .ToListAsync();

        // ── Clientes ──────────────────────────────────────────────────────────
        var totalClientes   = await _db.Users.CountAsync(u => u.IsActive && u.Role == UserRole.Customer);
        var clientesAtivos  = await _db.Comandas
            .Where(c => c.ClosedAt >= ha30Dias)
            .Select(c => c.UserId)
            .Distinct()
            .CountAsync();

        // ── Crediários ────────────────────────────────────────────────────────
        var crediarios = await _db.Crediarios
            .Include(c => c.User)
            .Where(c => c.Status == CrediariosStatus.Aberto)
            .Select(c => new { c.User.Name, Valor = c.ValorEmCentavos / 100m, c.DataVencimento })
            .ToListAsync();

        // ── Estoque baixo ─────────────────────────────────────────────────────
        var estoqueBaixo = await _db.Products
            .Where(p => p.IsActive && p.StockQuantity <= p.MinimumStock)
            .Select(p => new { p.Name, p.StockQuantity, p.MinimumStock })
            .ToListAsync();

        // ── Monta JSON de contexto (com anonimização LGPD) ────────────────────
        var ctx = new
        {
            dataHora          = agora.ToString("dd/MM/yyyy HH:mm") + " (UTC)",
            vendasHoje        = $"R$ {totalHoje:N2}",
            comandasAbertas,
            ticketMedio30dias = $"R$ {ticketMedio:N2}",
            totalClientes,
            clientesAtivos30dias = clientesAtivos,
            clientesInativos30dias = Math.Max(0, totalClientes - clientesAtivos),
            topProdutos30dias = topProdutos.Select(p => $"{p.Nome} ({p.Qtd} un)"),
            // LGPD: nomes reais substituídos por "Cliente #N" — não enviamos
            // dados pessoais identificáveis à API do Google Gemini.
            crediarios = crediarios.Select((c, index) => new
            {
                cliente    = $"Cliente #{index + 1}",
                valor      = $"R$ {c.Valor:N2}",
                vencimento = c.DataVencimento.ToString("dd/MM/yyyy"),
                vencido    = c.DataVencimento < agora,
            }),
            estoqueBaixo = estoqueBaixo.Select(p => new
            {
                produto  = p.Name,
                estoque  = p.StockQuantity,
                minimo   = p.MinimumStock,
            }),
        };

        return JsonSerializer.Serialize(ctx, new JsonSerializerOptions { WriteIndented = false });
    }

    private static string BuildPrompt(string userMessage, string contextJson) => $"""
        Você é um assistente de gestão da Santuário Nerd, loja especializada em card games (TCG).
        Responda em português brasileiro, de forma direta e objetiva — máximo 3 parágrafos curtos.
        Use linguagem simples, sem jargões técnicos.
        Não invente dados: use APENAS os dados fornecidos abaixo.
        Se os dados não forem suficientes para responder, diga isso claramente.
        Valores monetários sempre no formato R$ X,XX.

        DADOS ATUAIS DA LOJA:
        {contextJson}

        PERGUNTA DO ADMINISTRADOR:
        {userMessage}
        """;
}
