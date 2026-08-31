// =============================================================================
// AsaasPlatformGateway.cs — Implementação Asaas do gateway de mensalidade da
// plataforma (RB-01).
//
// Só a nossa conta Asaas é usada aqui: uma conta, a do Octus, cobrando as lojas.
// Nada de subconta ou split — isso foi avaliado e descartado, com motivo, em
// docs/planejamento/REBUILD-ESCOPO-2026-08.md (RB-02).
// =============================================================================

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;

namespace CardGameStore.Services.Implementations;

public class AsaasPlatformGateway : IPlatformPaymentGateway
{
    public const string GatewayName = "asaas";

    /// <summary>User-Agent enviado ao Asaas. Não é enfeite: a API RECUSA com 400
    /// e código `user_agent_not_informed` quando o header não vem, e o
    /// HttpClient do .NET não manda nenhum por padrão. Descoberto no primeiro
    /// teste em sandbox — o cadastro do cliente falhava antes mesmo de validar
    /// CNPJ, então nenhuma cobrança chegava a ser criada.</summary>
    public const string UserAgent = "Tenant-ERP/1.0 (+https://3esysten.com.br)";

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration     _config;
    private readonly ILogger<AsaasPlatformGateway> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public AsaasPlatformGateway(
        IHttpClientFactory http,
        IConfiguration config,
        ILogger<AsaasPlatformGateway> logger)
    {
        _http   = http;
        _config = config;
        _logger = logger;
    }

    public string Name => GatewayName;

    private string? ApiKey       => _config["Billing:Asaas:ApiKey"];
    private string? WebhookToken => _config["Billing:Asaas:WebhookToken"];

    /// <summary>Forma de pagamento. "UNDEFINED" deixa o lojista escolher entre
    /// Pix, boleto e cartão na própria fatura — que é o que queremos enquanto a
    /// taxa de assinatura em Pix não está confirmada: fixar "PIX" aqui poderia
    /// nos amarrar justamente na tarifa que ainda está em aberto.</summary>
    private string BillingType => _config["Billing:Asaas:BillingType"] ?? "UNDEFINED";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    private HttpClient CreateClient()
    {
        var client = _http.CreateClient(GatewayName);
        client.DefaultRequestHeaders.Remove("access_token");
        client.DefaultRequestHeaders.Add("access_token", ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Obrigatório — ver o comentário em UserAgent. Sem isso o Asaas devolve
        // 400 antes de olhar o corpo da requisição.
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        return client;
    }

    // ── Emissão ──────────────────────────────────────────────────────────────

    /// <summary>Valor mínimo aceito pelo Asaas por cobrança. Checado aqui pra
    /// virar uma pendência legível em vez de um 400 do gateway — e pra não
    /// gastar chamada numa cobrança que já se sabe recusada. Plano de tabela
    /// nenhum chega perto disso; quem esbarra é cortesia ou ajuste manual.</summary>
    public const decimal ValorMinimo = 5.00m;

    public async Task<string> GarantirClienteAsync(Tenant tenant, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(tenant.BillingCustomerId))
            return tenant.BillingCustomerId;

        if (!IsConfigured)
            throw new InvalidOperationException("Asaas não configurado (Billing:Asaas:ApiKey ausente).");

        using var client = CreateClient();
        return await CriarClienteAsync(client, tenant, ct);
    }

    public async Task<CobrancaGatewayResult> EmitirCobrancaAsync(
        TenantCharge charge, Tenant tenant, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Asaas não configurado (Billing:Asaas:ApiKey ausente).");

        if (string.IsNullOrWhiteSpace(tenant.BillingCustomerId))
            throw new InvalidOperationException(
                $"Tenant {tenant.Slug} sem cliente no gateway — chame GarantirClienteAsync antes.");

        if (charge.Amount < ValorMinimo)
            throw new InvalidOperationException(
                $"Asaas não aceita cobrança abaixo de {ValorMinimo:C} (esta é de {charge.Amount:C}).");

        using var client = CreateClient();

        var body = new
        {
            customer          = tenant.BillingCustomerId,
            billingType       = BillingType,
            value             = charge.Amount,
            dueDate           = charge.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            description       = DescricaoDaCobranca(charge, tenant),
            // Amarra a cobrança do Asaas à nossa linha. O webhook não usa isso
            // (busca por ExternalChargeId), mas é o que salva a conciliação
            // manual quando alguém precisa achar a origem de um pagamento.
            externalReference = charge.Id.ToString(),
        };

        using var response = await client.PostAsync(
            "payments",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            ct);

        var conteudo = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Asaas recusou a cobrança do tenant {Slug}: {Status} {Corpo}",
                tenant.Slug, (int)response.StatusCode, conteudo);
            throw new InvalidOperationException($"Asaas recusou a cobrança ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(conteudo);
        var raiz = doc.RootElement;

        var externalId = LerTexto(raiz, "id")
            ?? throw new InvalidOperationException("Asaas devolveu cobrança sem id.");

        return new CobrancaGatewayResult(
            ExternalId: externalId,
            PaymentUrl: LerTexto(raiz, "invoiceUrl") ?? LerTexto(raiz, "bankSlipUrl"));
    }

    private async Task<string> CriarClienteAsync(HttpClient client, Tenant tenant, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenant.BillingCnpj))
            throw new InvalidOperationException(
                $"Tenant {tenant.Slug} sem BillingCnpj — o gateway não cria cliente sem CPF/CNPJ.");

        var body = new
        {
            name     = tenant.DisplayName ?? tenant.Slug,
            cpfCnpj  = SomenteDigitos(tenant.BillingCnpj),
            email    = tenant.BillingEmail,
            // Reaproveitável na conciliação e evita cliente duplicado se alguém
            // recriar o tenant com o mesmo CNPJ.
            externalReference = tenant.Id.ToString(),
        };

        using var response = await client.PostAsync(
            "customers",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            ct);

        var conteudo = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Asaas recusou o cliente do tenant {Slug}: {Status} {Corpo}",
                tenant.Slug, (int)response.StatusCode, conteudo);
            throw new InvalidOperationException($"Asaas recusou o cadastro do cliente ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(conteudo);
        return LerTexto(doc.RootElement, "id")
            ?? throw new InvalidOperationException("Asaas devolveu cliente sem id.");
    }

    private static string DescricaoDaCobranca(TenantCharge charge, Tenant tenant)
    {
        var loja = tenant.DisplayName ?? tenant.Slug;
        return charge.Kind == TenantChargeKind.Implantacao
            ? $"Implantação — {loja}"
            : $"Mensalidade {charge.ReferenceMonth:MM/yyyy} — {loja}";
    }

    // ── Webhook ──────────────────────────────────────────────────────────────

    /// <summary>Comparação de tempo constante: comparar segredo com == vaza o
    /// tamanho do prefixo correto pelo tempo de resposta. É barato blindar.</summary>
    public bool ValidarAutenticacao(string? tokenRecebido)
    {
        var esperado = WebhookToken;

        // Sem token configurado o endpoint fica fechado, não aberto. Um webhook
        // público sem segredo é um botão de "quitar mensalidade" para qualquer
        // um na internet.
        if (string.IsNullOrWhiteSpace(esperado)) return false;
        if (string.IsNullOrWhiteSpace(tokenRecebido)) return false;

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(tokenRecebido),
            Encoding.UTF8.GetBytes(esperado));
    }

    public GatewayWebhookNotification? InterpretarWebhook(JsonElement payload)
    {
        var evento = LerTexto(payload, "event");
        if (string.IsNullOrWhiteSpace(evento)) return null;

        if (!payload.TryGetProperty("payment", out var pagamento) ||
            pagamento.ValueKind != JsonValueKind.Object)
            return null;

        var externalId = LerTexto(pagamento, "id");
        if (string.IsNullOrWhiteSpace(externalId)) return null;

        var outcome = evento switch
        {
            // RECEIVED = dinheiro disponível na conta; CONFIRMED = processado,
            // ainda não liquidado (cartão espera a janela). Os dois dão baixa: o
            // que interessa pro contrato com a loja é que ela pagou, e segurar a
            // reativação até a liquidação do cartão deixaria cliente adimplente
            // com a loja suspensa por semanas.
            "PAYMENT_RECEIVED" or "PAYMENT_CONFIRMED" => GatewayPaymentOutcome.Paga,

            "PAYMENT_REFUNDED"
                or "PAYMENT_PARTIALLY_REFUNDED"
                or "PAYMENT_DELETED"
                or "PAYMENT_CHARGEBACK_REQUESTED"
                or "PAYMENT_REVERSED" => GatewayPaymentOutcome.Revertida,

            // PAYMENT_OVERDUE inclusive: vencimento a gente calcula por DueDate,
            // que é a nossa fonte de verdade e não depende do gateway estar vivo.
            _ => GatewayPaymentOutcome.Ignorada,
        };

        return new GatewayWebhookNotification(externalId, outcome, LerData(pagamento));
    }

    /// <summary>paymentDate é o dia da liquidação; confirmedDate é o do aceite.
    /// Preferir paymentDate mantém o relatório de recebidos alinhado com o
    /// extrato. Null cai no "agora" de quem chamou.</summary>
    private static DateTime? LerData(JsonElement pagamento)
    {
        foreach (var campo in new[] { "paymentDate", "confirmedDate", "clientPaymentDate" })
        {
            var texto = LerTexto(pagamento, campo);
            if (string.IsNullOrWhiteSpace(texto)) continue;

            if (DateTime.TryParse(texto, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var data))
                return DateTime.SpecifyKind(data.Date, DateTimeKind.Utc);
        }

        return null;
    }

    private static string? LerTexto(JsonElement elemento, string propriedade) =>
        elemento.TryGetProperty(propriedade, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private static string SomenteDigitos(string valor) =>
        new(valor.Where(char.IsDigit).ToArray());
}
