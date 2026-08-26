// =============================================================================
// IPlatformPaymentGateway.cs — Emissão e baixa automática da mensalidade DA
// PLATAFORMA (RB-01). Quem recebe aqui somos nós, não o lojista.
//
// Não confundir com as integrações de recebimento do tenant (Inter, Mercado
// Pago) que vivem em IntegrationConfig, no schema da loja: aquelas são o
// dinheiro DA VENDA e caem na conta do lojista. Esta é a nossa cobrança contra
// ele, e cai na nossa.
//
// Existe como interface por uma razão concreta e datada: em 26/08/2026 ainda
// não estava confirmado se o Asaas aplica 1,99% sobre assinatura em Pix ou só
// em cartão/parcelado. No plano Mar isso é R$ 9,69 contra R$ 1,99 por cobrança,
// e a resposta pode empurrar a plataforma pra outro PSP (Woovi, Efí). Todo o
// resto do RB-01 — webhook, baixa idempotente, suspensão automática — é
// indiferente a essa escolha, então nada disso pode nascer amarrado ao Asaas.
// =============================================================================

using System.Text.Json;
using CardGameStore.Multitenancy;

namespace CardGameStore.Services.Interfaces;

/// <summary>O que o gateway devolve ao registrar uma cobrança. CustomerId vem
/// preenchido quando o gateway precisou criar o cliente agora — cabe a quem
/// chamou persistir em Tenant.BillingCustomerId, senão a próxima mensalidade
/// cria um cliente duplicado pro mesmo CNPJ.</summary>
public record CobrancaGatewayResult(string ExternalId, string? PaymentUrl, string? CustomerId = null);

/// <summary>O que um evento de webhook significa para o nosso financeiro. O
/// gateway manda dezenas de eventos (visualizou boleto, análise de risco,
/// antecipou); só três desfechos mudam o estado de uma cobrança aqui.</summary>
public enum GatewayPaymentOutcome
{
    /// <summary>Dinheiro entrou. Dá baixa.</summary>
    Paga,

    /// <summary>Estorno, chargeback ou cobrança removida. Reabre.</summary>
    Revertida,

    /// <summary>Evento que não muda nada (criada, visualizada, vencida — o
    /// vencimento a gente calcula por DueDate, não por aviso do gateway).</summary>
    Ignorada,
}

/// <summary>Evento de webhook já traduzido pro nosso domínio.</summary>
public record GatewayWebhookNotification(
    string ExternalChargeId,
    GatewayPaymentOutcome Outcome,
    DateTime? PagoEm);

public interface IPlatformPaymentGateway
{
    /// <summary>Identificador gravado em TenantCharge.Gateway.</summary>
    string Name { get; }

    /// <summary>False quando faltam credenciais. O sistema tem que subir e
    /// operar sem gateway nenhum: a plataforma rodou meses com baixa manual e
    /// não pode parar de rodar porque uma chave não foi preenchida.</summary>
    bool IsConfigured { get; }

    /// <summary>Registra a cobrança no gateway e devolve o id externo e o link
    /// de pagamento.</summary>
    Task<CobrancaGatewayResult> EmitirCobrancaAsync(TenantCharge charge, Tenant tenant, CancellationToken ct = default);

    /// <summary>Valida o segredo que o gateway manda no header. Endpoint de
    /// webhook é público por natureza — sem isso qualquer um na internet dá
    /// baixa nas nossas mensalidades mandando um POST.</summary>
    bool ValidarAutenticacao(string? tokenRecebido);

    /// <summary>Traduz o payload do gateway. Devolve null quando o corpo não é
    /// interpretável — que é diferente de Ignorada: uma é "não entendi", a
    /// outra é "entendi e não muda nada".</summary>
    GatewayWebhookNotification? InterpretarWebhook(JsonElement payload);
}
