using System.Net.Http.Json;
using System.Text.Json;
using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;

namespace CardGameStore.Services.Implementations;

/// <summary>
/// Assistente comercial público e deliberadamente sem AppDbContext/ITenantContext.
/// Ele conhece somente o catálogo fixo abaixo e nunca consegue consultar dados de lojas.
/// </summary>
public sealed class PublicSalesAssistantService : IPublicSalesAssistantService
{
    private const string GeminiUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent";

    private const string MarketingUrl = "https://wa.me/5517997455482";

    private const string CommercialContext = """
        Você é o Assistente Octus da 3E Systen. Responda em português do Brasil, de forma curta,
        simples e cordial. Fale somente sobre o produto Octus e as informações comerciais abaixo.
        Nunca peça CPF, CNPJ, senha, cartão, dados fiscais ou qualquer dado pessoal. Você não tem
        acesso a lojas, clientes, banco de dados, documentos, cobranças ou painéis. Se a pergunta
        sair desse escopo, diga que o Marketing pode ajudar e indique o WhatsApp.

        Octus é um ERP personalizável para varejo e restaurantes, com PDV, estoque, financeiro,
        crediário, fiscal/NFC-e, relatórios, portal do contador, PWA e módulos opcionais. A marca,
        cores, logo e domínio do lojista podem substituir a identidade padrão do Octus.

        Planos mensais: Lagoa R$129, Rio R$269 e Mar R$487. Todos os planos têm taxa de
        implantação, cobrada uma única vez; o valor é definido na contratação conforme o porte da
        operação, então NÃO informe valor de implantação — diga que o Marketing fecha esse valor.
        Todos têm 15 dias grátis; a primeira mensalidade é cobrada no 16º dia. O módulo
        restaurante é opcional e habilitado apenas para quem contratar/usar.

        Clientes Fundadores: disponível para clientes do estado de São Paulo, sem limite de vagas.
        Também recebem 15 dias grátis e depois 30% de desconto nas quatro primeiras mensalidades.
        Cada indicação que fechar soma 10 pontos percentuais de desconto nesses mesmos quatro meses,
        até 100%; com 7 indicações, as quatro mensalidades ficam grátis. Visitas técnicas somente na
        região metropolitana de São José do Rio Preto. Condições são confirmadas pelo Marketing.

        Marketing: +55 17 99745-5482. Não invente funcionalidades, preços, prazos ou garantias.
        Termine respostas sobre contratação convidando a falar com o Marketing.
        """;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PublicSalesAssistantService> _logger;

    public PublicSalesAssistantService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PublicSalesAssistantService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PublicAssistantResponse> AskAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        var normalizedMessage = message.Trim();
        var apiKey = _configuration["GeminiSettings:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            return BuildFallback(normalizedMessage);

        try
        {
            var payload = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = CommercialContext } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = normalizedMessage } }
                    }
                },
                // 320 cortava a resposta no meio da palavra na página de vendas —
                // ver GeminiLimits, que é onde este número mora agora.
                generationConfig = new { maxOutputTokens = GeminiLimits.MaxOutputTokens }
            };

            var client = _httpClientFactory.CreateClient("gemini");
            using var response = await client.PostAsJsonAsync(
                $"{GeminiUrl}?key={apiKey}", payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Assistente Octus recebeu status {StatusCode} do provedor.", response.StatusCode);
                return BuildFallback(normalizedMessage);
            }

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            var reply = document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return string.IsNullOrWhiteSpace(reply)
                ? BuildFallback(normalizedMessage)
                : new PublicAssistantResponse { Reply = reply.Trim(), MarketingWhatsappUrl = MarketingUrl };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Assistente Octus indisponível; usando resposta segura local.");
            return BuildFallback(normalizedMessage);
        }
    }

    internal static PublicAssistantResponse BuildFallback(string message)
    {
        var text = message.ToLowerInvariant();
        var reply = text.Contains("plano") || text.Contains("preço") || text.Contains("valor")
            ? "Os planos são Lagoa por R$ 129/mês, Rio por R$ 269/mês e Mar por R$ 487/mês. Todos incluem 15 dias grátis e têm taxa de implantação cobrada uma única vez, com valor definido na contratação."
            : text.Contains("fundador") || text.Contains("indica") || text.Contains("desconto")
                ? "Clientes Fundadores de São Paulo têm 15 dias grátis e 30% de desconto nas quatro primeiras mensalidades. Cada indicação fechada soma 10% de desconto, até quatro meses grátis com 7 indicações."
                : text.Contains("restaurante") || text.Contains("comanda") || text.Contains("cozinha")
                    ? "O Octus atende restaurantes por um módulo opcional, sem alterar o fluxo de quem usa o sistema apenas no varejo."
                    : "O Octus reúne PDV, estoque, financeiro, crediário, fiscal, relatórios e recursos opcionais em um ERP que pode receber a identidade da sua empresa.";

        return new PublicAssistantResponse
        {
            Reply = $"{reply} Se quiser confirmar algum detalhe, fale com nosso Marketing pelo WhatsApp.",
            MarketingWhatsappUrl = MarketingUrl,
        };
    }
}
