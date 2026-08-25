using System.Text.RegularExpressions;
using CardGameStore.DTOs;
using CardGameStore.Middleware;
using CardGameStore.Multitenancy;
using CardGameStore.Security;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/integrations/services")]
[Authorize(Policy = "AdminOnly")]
[OperatorForbidden]
[Produces("application/json")]
public sealed partial class IntegrationServicesController(CatalogDbContext catalog) : ControllerBase
{
    private static readonly HashSet<string> Ufs = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS",
        "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC",
        "SP", "SE", "TO",
    };

    [HttpPost("financeiro/analisar")]
    [RequireIntegrationScope(IntegrationScope.FinanceRead)]
    public ActionResult<IntegrationFinancialAnalysisResponse> AnalyzeFinanceiro(
        [FromBody] IntegrationFinancialAnalysisRequest request)
    {
        if (request.Produtos.Count > 100)
            return BadRequest(new { Message = "Envie no máximo 100 produtos por análise." });
        if (request.RecebiveisVencidos > request.RecebiveisEmAberto)
            return BadRequest(new { Message = "Recebíveis vencidos não podem superar o total em aberto." });
        if (request.Inicio.HasValue && request.Fim.HasValue && request.Inicio > request.Fim)
            return BadRequest(new { Message = "A data inicial não pode ser posterior à data final." });

        return Ok(IntegrationFinancialAnalyzer.Analyze(request));
    }

    [HttpGet("fiscal/ibpt/{ncm}")]
    [RequireIntegrationScope(IntegrationScope.FiscalRead)]
    public async Task<ActionResult<IntegrationIbptResponse>> GetIbpt(
        string ncm, [FromQuery] string uf, [FromQuery] bool importado = false,
        CancellationToken ct = default)
    {
        var normalizedNcm = DigitsOnly().Replace(ncm ?? string.Empty, string.Empty);
        var normalizedUf = (uf ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedNcm.Length != 8)
            return BadRequest(new { Message = "O NCM deve conter exatamente 8 dígitos." });
        if (!Ufs.Contains(normalizedUf))
            return BadRequest(new { Message = "Informe uma UF brasileira válida." });

        var item = await catalog.IbptTabela.AsNoTracking()
            .Where(entry => entry.Ncm == normalizedNcm && entry.Uf == normalizedUf && entry.Importado == importado)
            .Select(entry => new IntegrationIbptResponse(
                entry.Ncm, entry.Uf, entry.Importado,
                entry.PercentualFederal, entry.PercentualEstadual, entry.PercentualMunicipal,
                entry.PercentualFederal + entry.PercentualEstadual + entry.PercentualMunicipal,
                entry.Fonte, entry.Versao, entry.VigenciaInicio, entry.VigenciaFim,
                entry.VigenciaFim.HasValue && entry.VigenciaFim.Value.Date < DateTime.UtcNow.Date,
                entry.AtualizadoEm))
            .SingleOrDefaultAsync(ct);

        return item is null
            ? NotFound(new { Message = $"NCM {normalizedNcm} não encontrado na tabela IBPT de {normalizedUf}." })
            : Ok(item);
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex DigitsOnly();
}
