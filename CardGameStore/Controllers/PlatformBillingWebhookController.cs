// =============================================================================
// PlatformBillingWebhookController.cs — Recebe a confirmação de pagamento da
// mensalidade da plataforma (RB-01) e dá baixa sozinho.
//
// É o que substitui a baixa manual: antes o dono da plataforma abria o painel,
// conferia o extrato e clicava em "dar baixa" loja por loja.
//
// Endpoint anônimo POR NECESSIDADE — quem chama é o gateway, que não tem como
// carregar um JWT nosso. A autenticação é o segredo compartilhado no header,
// validado pelo próprio gateway (ValidarAutenticacao). Sem isso, este endpoint
// seria um botão público de "quitar mensalidade".
// =============================================================================

using System.Text.Json;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/webhooks/billing")]
[AllowAnonymous]
[Produces("application/json")]
public class PlatformBillingWebhookController : ControllerBase
{
    private readonly IPlatformBillingService _billing;
    private readonly IPlatformPaymentGateway? _gateway;
    private readonly ILogger<PlatformBillingWebhookController> _logger;

    public PlatformBillingWebhookController(
        IPlatformBillingService billing,
        ILogger<PlatformBillingWebhookController> logger,
        IPlatformPaymentGateway? gateway = null)
    {
        _billing = billing;
        _gateway = gateway;
        _logger  = logger;
    }

    /// <summary>Header em que o Asaas manda o segredo configurado no painel dele.</summary>
    private const string TokenHeader = "asaas-access-token";

    [HttpPost]
    public async Task<IActionResult> Receber([FromBody] JsonElement payload)
    {
        if (_gateway is null || !_gateway.IsConfigured)
        {
            _logger.LogWarning("Webhook de billing recebido sem gateway configurado.");
            return NotFound();
        }

        var token = Request.Headers[TokenHeader].FirstOrDefault();

        if (!_gateway.ValidarAutenticacao(token))
        {
            // Sem detalhe no corpo: dizer "token errado" a quem está sondando o
            // endpoint só confirma que ele existe e que o header é o certo.
            _logger.LogWarning("Webhook de billing rejeitado: autenticação inválida.");
            return Unauthorized();
        }

        var notificacao = _gateway.InterpretarWebhook(payload);

        if (notificacao is null)
        {
            // 200 de propósito: corpo que não entendemos não melhora com
            // retentativa, e devolver erro faria o gateway reenfileirar pra
            // sempre. O log é o que registra o caso pra investigação.
            _logger.LogWarning("Webhook de billing com payload não interpretável.");
            return Ok(new { Ignorado = true });
        }

        if (notificacao.Outcome == GatewayPaymentOutcome.Ignorada)
            return Ok(new { Ignorado = true });

        var encontrada = await _billing.RegistrarPagamentoExternoAsync(
            _gateway.Name,
            notificacao.ExternalChargeId,
            paga: notificacao.Outcome == GatewayPaymentOutcome.Paga,
            pagoEm: notificacao.PagoEm);

        // Também 200 quando não achamos a cobrança: o gateway notifica tudo que
        // acontece na conta, e nem toda cobrança lá é mensalidade nossa. Devolver
        // 404 faria o gateway retentar indefinidamente um evento legítimo que
        // simplesmente não é da nossa conta a receber.
        return Ok(new { Processado = encontrada });
    }
}
