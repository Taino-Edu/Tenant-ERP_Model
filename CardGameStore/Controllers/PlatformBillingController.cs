// =============================================================================
// PlatformBillingController.cs — Financeiro da plataforma: o que cobramos de
// cada loja e o que entrou. Ver PlatformBillingService pro racional das regras.
//
// PlatformOwnerOnly, como todo /api/platform/*: são os números do negócio da
// plataforma, não da loja. Nenhum lojista pode chegar aqui.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using CardGameStore.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/platform/billing")]
[Authorize(Policy = "PlatformOwnerOnly")]
[RequirePlatformPermission(PlatformPermission.FinanceRead)]
public class PlatformBillingController : ControllerBase
{
    private readonly IPlatformBillingService _billing;
    private readonly ILogger<PlatformBillingController> _logger;

    public PlatformBillingController(IPlatformBillingService billing, ILogger<PlatformBillingController> logger)
    {
        _billing = billing;
        _logger  = logger;
    }

    /// <summary>Painel do mês: MRR contratado, faturado, recebido, em aberto e
    /// inadimplência acumulada. Sem competência na query, usa o mês atual.</summary>
    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo([FromQuery] DateTime? competencia)
        => Ok(await _billing.ObterResumoAsync(competencia ?? DateTime.UtcNow));

    /// <summary>Cobranças de um mês de competência.</summary>
    [HttpGet("cobrancas")]
    public async Task<IActionResult> Cobrancas([FromQuery] DateTime? competencia)
        => Ok(await _billing.ListarPorCompetenciaAsync(competencia ?? DateTime.UtcNow));

    /// <summary>Histórico de cobranças de uma loja específica.</summary>
    [HttpGet("cobrancas/tenant/{tenantId:guid}")]
    public async Task<IActionResult> PorTenant(Guid tenantId)
        => Ok(await _billing.ListarPorTenantAsync(tenantId));

    /// <summary>Gera as mensalidades do mês. Idempotente — a unique index
    /// (tenant, tipo, competência) impede duplicar, então clicar duas vezes é
    /// inofensivo e o resultado diz quantas já existiam.</summary>
    [HttpPost("gerar-mensalidades")]
    [RequirePlatformPermission(PlatformPermission.FinanceManage)]
    public async Task<IActionResult> GerarMensalidades([FromBody] GerarMensalidadesRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var resultado = await _billing.GerarMensalidadesAsync(request.Competencia);
        return Ok(resultado);
    }

    /// <summary>Dá baixa numa cobrança (ou reabre, mandando pagoEm null).</summary>
    [HttpPut("cobrancas/{id:guid}/pagamento")]
    [RequirePlatformPermission(PlatformPermission.FinanceManage)]
    public async Task<IActionResult> DefinirPagamento(Guid id, [FromBody] DefinirPagamentoRequest request)
    {
        try
        {
            return Ok(await _billing.DefinirPagamentoAsync(id, request.PagoEm));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Falha ao dar baixa na cobrança {Id}: {Msg}", id, ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
    }

    // ── Lançamentos manuais ─────────────────────────────────────────────────
    // Os três exigem FinanceManage, como gerar mensalidades e dar baixa. Quem
    // tem só FinanceRead (Auditoria) continua enxergando o financeiro inteiro
    // sem poder emitir nem apagar cobrança.

    /// <summary>Cria uma cobrança avulsa para uma loja.</summary>
    [HttpPost("cobrancas")]
    [RequirePlatformPermission(PlatformPermission.FinanceManage)]
    public async Task<IActionResult> CriarCobranca([FromBody] CriarCobrancaRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            return Ok(await _billing.CriarCobrancaAsync(request));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Falha ao criar cobrança para {TenantId}: {Msg}", request.TenantId, ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Altera valor, vencimento e observação de uma cobrança em aberto.</summary>
    [HttpPut("cobrancas/{id:guid}")]
    [RequirePlatformPermission(PlatformPermission.FinanceManage)]
    public async Task<IActionResult> AtualizarCobranca(Guid id, [FromBody] AtualizarCobrancaRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            return Ok(await _billing.AtualizarCobrancaAsync(id, request));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Falha ao alterar a cobrança {Id}: {Msg}", id, ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Exclui uma cobrança em aberto.</summary>
    [HttpDelete("cobrancas/{id:guid}")]
    [RequirePlatformPermission(PlatformPermission.FinanceManage)]
    public async Task<IActionResult> ExcluirCobranca(Guid id)
    {
        try
        {
            await _billing.ExcluirCobrancaAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Falha ao excluir a cobrança {Id}: {Msg}", id, ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
    }
}
