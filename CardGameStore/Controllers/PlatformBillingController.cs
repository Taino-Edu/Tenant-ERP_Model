// =============================================================================
// PlatformBillingController.cs — Financeiro da plataforma: o que cobramos de
// cada loja e o que entrou. Ver PlatformBillingService pro racional das regras.
//
// PlatformOwnerOnly, como todo /api/platform/*: são os números do negócio da
// plataforma, não da loja. Nenhum lojista pode chegar aqui.
// =============================================================================

using System.IdentityModel.Tokens.Jwt;
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
    private readonly ICondicoesComerciaisService _condicoes;
    private readonly ILogger<PlatformBillingController> _logger;

    public PlatformBillingController(
        IPlatformBillingService billing,
        ICondicoesComerciaisService condicoes,
        ILogger<PlatformBillingController> logger)
    {
        _billing   = billing;
        _condicoes = condicoes;
        _logger    = logger;
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

    /// <summary>Manda agora para o gateway as cobranças em aberto que ainda não
    /// foram emitidas, sem esperar a rodada automática de 12 em 12 horas. Seguro
    /// clicar mais de uma vez: a emissão é serializada e só pega cobrança sem id
    /// externo.</summary>
    [HttpPost("emitir-pendentes")]
    [RequirePlatformPermission(PlatformPermission.FinanceManage)]
    public async Task<IActionResult> EmitirPendentes()
        // Sem o CancellationToken da requisição, de propósito: fechar a aba no
        // meio da rodada cancelaria o SaveChanges DEPOIS de o gateway já ter
        // registrado a cobrança, o id externo não seria gravado e a próxima
        // rodada emitiria a mesma cobrança de novo.
        => Ok(await _billing.EmitirCobrancasPendentesAsync(CancellationToken.None));

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

    // ── Condições comerciais ────────────────────────────────────────────────
    // O combinado com a loja. Toda escrita já aplica o resultado às cobranças em
    // aberto e devolve o que mudou nelas, pra quem negociou ver na hora se alguma
    // fatura emitida não pôde ser trocada.

    /// <summary>Condições da loja, descontos e a prévia dos próximos 12 meses.</summary>
    [HttpGet("tenants/{tenantId:guid}/condicoes")]
    public async Task<IActionResult> Condicoes(Guid tenantId)
        => await Executar(() => _condicoes.ObterAsync(tenantId), tenantId);

    /// <summary>Mensalidade, início, dia de vencimento e implantação parcelada.</summary>
    [HttpPut("tenants/{tenantId:guid}/condicoes")]
    [RequirePlatformPermission(PlatformPermission.FinanceManage)]
    public async Task<IActionResult> AtualizarCondicoes(Guid tenantId, [FromBody] AtualizarCondicoesRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return await Executar(() => _condicoes.AtualizarAsync(tenantId, request), tenantId);
    }

    [HttpPost("tenants/{tenantId:guid}/descontos")]
    [RequirePlatformPermission(PlatformPermission.FinanceManage)]
    public async Task<IActionResult> CriarDesconto(Guid tenantId, [FromBody] SalvarDescontoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var autor = User.FindFirst(JwtRegisteredClaimNames.Name)?.Value
                 ?? User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                 ?? User.Identity?.Name;

        return await Executar(() => _condicoes.CriarDescontoAsync(tenantId, request, autor), tenantId);
    }

    [HttpPut("tenants/{tenantId:guid}/descontos/{descontoId:guid}")]
    [RequirePlatformPermission(PlatformPermission.FinanceManage)]
    public async Task<IActionResult> AtualizarDesconto(Guid tenantId, Guid descontoId, [FromBody] SalvarDescontoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return await Executar(() => _condicoes.AtualizarDescontoAsync(tenantId, descontoId, request), tenantId);
    }

    [HttpDelete("tenants/{tenantId:guid}/descontos/{descontoId:guid}")]
    [RequirePlatformPermission(PlatformPermission.FinanceManage)]
    public async Task<IActionResult> ExcluirDesconto(Guid tenantId, Guid descontoId)
        => await Executar(() => _condicoes.ExcluirDescontoAsync(tenantId, descontoId), tenantId);

    private async Task<IActionResult> Executar<T>(Func<Task<T>> acao, Guid tenantId)
    {
        try
        {
            return Ok(await acao());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Condições comerciais do tenant {TenantId}: {Msg}", tenantId, ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
    }
}
