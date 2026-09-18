// =============================================================================
// AssinaturaController.cs — A loja olhando a PRÓPRIA assinatura.
//
// Até aqui todo o billing era PlatformOwnerOnly: o lojista não via plano, não
// via fatura e não tinha onde informar os dados de cobrança — quem preenchia
// era o dono da plataforma, por SQL. Isto existe pra fechar esse buraco.
//
// Três regras que sustentam a segurança deste controller:
//
// 1. O tenant vem SEMPRE de ITenantContext, nunca do corpo ou da rota. Não há
//    parâmetro de tenant em endpoint nenhum aqui — se houvesse, uma loja
//    poderia ler a fatura de outra trocando um id.
// 2. Role "Admin" no seco, não a policy "AdminOnly": aquela inclui Operator e
//    Integration, e dado de faturamento é do dono da loja, não de quem opera o
//    caixa nem de um token de API.
// 3. Só campos de faturamento são graváveis. Plano, mensalidade, status e
//    vencimento são decisão comercial da plataforma — aparecem para leitura e
//    não têm endpoint de escrita aqui.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using CardGameStore.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/assinatura")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AssinaturaController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    private readonly ITenantContext _tenant;
    private readonly ILogger<AssinaturaController> _logger;
    private readonly IPlatformBillingService? _billing;

    public AssinaturaController(
        CatalogDbContext catalog,
        ITenantContext tenant,
        ILogger<AssinaturaController> logger,
        IPlatformBillingService? billing = null)
    {
        _catalog = catalog;
        _tenant  = tenant;
        _logger  = logger;
        _billing = billing;
    }

    [HttpGet]
    public async Task<ActionResult<AssinaturaDto>> Obter(CancellationToken ct)
    {
        var tenant = await _catalog.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _tenant.TenantId, ct);

        if (tenant is null) return NotFound();

        var hoje = DateTime.UtcNow.Date;

        var faturas = await _catalog.TenantCharges.AsNoTracking()
            .Where(c => c.TenantId == tenant.Id)
            .OrderByDescending(c => c.ReferenceMonth)
            .ThenByDescending(c => c.DueDate)
            .Select(c => new FaturaDto
            {
                Id              = c.Id,
                Tipo            = c.Kind.ToString(),
                Valor           = c.Amount,
                Competencia     = c.ReferenceMonth,
                Vencimento      = c.DueDate,
                PagoEm          = c.PaidAt,
                Vencida         = c.PaidAt == null && c.DueDate < hoje,
                LinkDePagamento = c.PaymentUrl,
                Desconto          = c.DiscountAmount,
                DescricaoDesconto = c.DiscountSummary,
                Parcela           = c.InstallmentNumber,
                TotalParcelas     = c.InstallmentCount,
            })
            .ToListAsync(ct);

        return Ok(new AssinaturaDto
        {
            Plano              = tenant.PlanName,
            Mensalidade        = tenant.MonthlyPrice,
            Situacao           = tenant.Status == TenantStatus.Active ? "Ativa" : "Suspensa",
            StatusPagamento    = tenant.PaymentStatus.ToString(),
            Cnpj               = tenant.BillingCnpj,
            EmailDeFaturamento = tenant.BillingEmail,
            DadosCompletos     = !string.IsNullOrWhiteSpace(tenant.BillingCnpj)
                                 && !string.IsNullOrWhiteSpace(tenant.BillingEmail),
            Faturas            = faturas,
        });
    }

    [HttpPut("faturamento")]
    public async Task<ActionResult<AssinaturaDto>> AtualizarFaturamento(
        [FromBody] AtualizarFaturamentoRequest request, CancellationToken ct)
    {
        var tenant = await _catalog.Tenants
            .FirstOrDefaultAsync(t => t.Id == _tenant.TenantId, ct);

        if (tenant is null) return NotFound();

        var documento = CnpjValidAttribute.SomenteDigitos(request.Documento);

        // Documento novo = pessoa jurídica diferente no gateway. Manter o
        // BillingCustomerId antigo faria a próxima cobrança sair no CNPJ errado,
        // e o lojista receberia uma fatura em nome de outra empresa. Zerar aqui
        // faz o job criar o cliente certo na próxima rodada.
        if (!string.Equals(tenant.BillingCnpj, documento, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(tenant.BillingCustomerId))
        {
            _logger.LogInformation(
                "Documento de faturamento do tenant {Slug} mudou — descartando o cliente {Cliente} no gateway",
                tenant.Slug, tenant.BillingCustomerId);

            tenant.BillingCustomerId = null;
        }

        tenant.BillingCnpj  = documento;
        tenant.BillingEmail = request.Email.Trim();

        await _catalog.SaveChangesAsync(ct);

        // A fatura sai agora, e não na próxima rodada do job. Quem chega aqui
        // quase sempre está com a loja suspensa ou perto disso, esperando o link
        // pra pagar — 12 horas de espera seriam 12 horas a mais fora do ar.
        if (_billing is not null)
        {
            try
            {
                var emissao = await _billing.EmitirCobrancasPendentesDaLojaAsync(tenant.Id, CancellationToken.None);
                foreach (var pendencia in emissao.Pendencias)
                    _logger.LogWarning("Fatura do tenant {Slug} não emitida após atualizar faturamento: {Pendencia}",
                        tenant.Slug, pendencia);
            }
            catch (Exception ex)
            {
                // Os dados já estão salvos; a rodada automática tenta de novo.
                _logger.LogError(ex, "Falha ao emitir as faturas do tenant {Slug} após atualizar faturamento", tenant.Slug);
            }
        }

        return await Obter(ct);
    }
}
