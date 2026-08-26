using System.Text.RegularExpressions;
using CardGameStore.DTOs;
using CardGameStore.Middleware;
using CardGameStore.Multitenancy;
using CardGameStore.Security;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/integrations/services")]
[Authorize(Policy = "AdminOnly")]
[OperatorForbidden]
[Produces("application/json")]
public sealed partial class IntegrationServicesController(
    CatalogDbContext catalog,
    AppDbContext db,
    INfceEmissionService emissao,
    FiscalConfigService fiscalConfig) : ControllerBase
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

    [HttpGet("fiscal/config")]
    [RequireIntegrationScope(IntegrationScope.FiscalRead)]
    public async Task<IActionResult> GetFiscalConfig()
    {
        var cfg = await db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
        return Ok(FiscalConfigService.ToDto(cfg));
    }

    [HttpGet("fiscal/health")]
    [RequireIntegrationScope(IntegrationScope.FiscalRead)]
    public async Task<IActionResult> GetFiscalHealth()
    {
        var cfg = await db.FiscalConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == FiscalConfig.SingletonId);
        var pending = await db.NotasFiscaisEmitidas.CountAsync(n =>
            n.Origem == NotaFiscalOrigem.IntegracaoExterna &&
            (n.Status == NotaFiscalStatus.PendenteEmissao ||
             n.Status == NotaFiscalStatus.AutorizadaContingencia ||
             n.Status == NotaFiscalStatus.ResultadoIncerto));
        var rejected24h = await db.NotasFiscaisEmitidas.CountAsync(n =>
            n.Origem == NotaFiscalOrigem.IntegracaoExterna &&
            n.Status == NotaFiscalStatus.Rejeitada && n.CreatedAt >= DateTime.UtcNow.AddHours(-24));
        var companyReady = cfg is not null &&
            !string.IsNullOrWhiteSpace(cfg.Cnpj) &&
            !string.IsNullOrWhiteSpace(cfg.RazaoSocial) &&
            !string.IsNullOrWhiteSpace(cfg.Uf) &&
            !string.IsNullOrWhiteSpace(cfg.CodigoMunicipioIbge);

        return Ok(new
        {
            Status = !companyReady || cfg?.CertificadoConfigurado != true
                ? "Bloqueado"
                : pending > 0 || rejected24h > 0 ? "RequerAtencao" : "Pronto",
            Ambiente = (cfg?.Ambiente ?? AmbienteFiscal.Homologacao).ToString(),
            EmpresaConfigurada = companyReady,
            CertificadoConfigurado = cfg?.CertificadoConfigurado == true,
            cfg?.CertificadoValidade,
            Pendentes = pending,
            Rejeitadas24h = rejected24h,
        });
    }

    [HttpPut("fiscal/config")]
    [RequireIntegrationScope(IntegrationScope.FiscalWrite)]
    public async Task<IActionResult> UpdateFiscalConfig([FromBody] SaveFiscalConfigRequest request)
    {
        var result = await fiscalConfig.SalvarAsync(request);
        return result.Ok
            ? Ok(FiscalConfigService.ToDto(result.Config))
            : BadRequest(new { Message = result.Erro });
    }

    [HttpPost("fiscal/certificate")]
    [RequireIntegrationScope(IntegrationScope.FiscalWrite)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UploadFiscalCertificate(
        [FromForm] IFormFile certificate, [FromForm] string password)
    {
        if (certificate is null || certificate.Length == 0)
            return BadRequest(new { Message = "Envie um certificado A1 no formato PFX/P12." });
        if (string.IsNullOrWhiteSpace(password))
            return BadRequest(new { Message = "Informe a senha do certificado." });

        await using var stream = certificate.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        var (erro, info) = await fiscalConfig.SalvarCertificadoAsync(buffer.ToArray(), password);
        return erro is null
            ? Ok(new { Message = "Certificado validado e armazenado.", info?.NotAfter })
            : BadRequest(new { Message = erro });
    }

    [HttpPost("fiscal/nfce")]
    [RequireIntegrationScope(IntegrationScope.FiscalWrite)]
    public async Task<ActionResult<IntegrationFiscalNoteResponse>> EmitFiscalNote(
        [FromBody] IntegrationFiscalEmissionRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var note = await emissao.EmitirIntegracaoAsync(request);
            return Ok(ToIntegrationResponse(note));
        }
        catch (FiscalNaoConfiguradoException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (OverflowException)
        {
            return BadRequest(new { Message = "Os valores enviados excedem o limite aceito." });
        }
    }

    [HttpGet("fiscal/nfce/{id:guid}")]
    [RequireIntegrationScope(IntegrationScope.FiscalRead)]
    public async Task<ActionResult<IntegrationFiscalNoteResponse>> GetFiscalNote(Guid id)
    {
        var note = await db.NotasFiscaisEmitidas.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.Origem == NotaFiscalOrigem.IntegracaoExterna);
        return note is null ? NotFound() : Ok(ToIntegrationResponse(note));
    }

    [HttpGet("fiscal/nfce")]
    [RequireIntegrationScope(IntegrationScope.FiscalRead)]
    public async Task<ActionResult<IntegrationFiscalNoteResponse>> FindFiscalNote(
        [FromQuery] string source, [FromQuery] string externalDocumentId)
    {
        var normalizedSource = (source ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedId = (externalDocumentId ?? string.Empty).Trim();
        var note = await db.NotasFiscaisEmitidas.AsNoTracking().FirstOrDefaultAsync(n =>
            n.Origem == NotaFiscalOrigem.IntegracaoExterna &&
            n.ExternalSource == normalizedSource && n.ExternalDocumentId == normalizedId);
        return note is null ? NotFound() : Ok(ToIntegrationResponse(note));
    }

    [HttpPost("fiscal/nfce/{id:guid}/retry")]
    [RequireIntegrationScope(IntegrationScope.FiscalWrite)]
    public async Task<ActionResult<IntegrationFiscalNoteResponse>> RetryFiscalNote(Guid id)
    {
        var external = await db.NotasFiscaisEmitidas.AsNoTracking()
            .AnyAsync(n => n.Id == id && n.Origem == NotaFiscalOrigem.IntegracaoExterna);
        if (!external) return NotFound();
        return Ok(ToIntegrationResponse(await emissao.ReprocessarAsync(id)));
    }

    [HttpPost("fiscal/nfce/{id:guid}/cancel")]
    [RequireIntegrationScope(IntegrationScope.FiscalWrite)]
    public async Task<ActionResult<IntegrationFiscalNoteResponse>> CancelFiscalNote(
        Guid id, [FromBody] IntegrationFiscalCancelRequest request)
    {
        var external = await db.NotasFiscaisEmitidas.AsNoTracking()
            .AnyAsync(n => n.Id == id && n.Origem == NotaFiscalOrigem.IntegracaoExterna);
        if (!external) return NotFound();
        try
        {
            return Ok(ToIntegrationResponse(await emissao.CancelarAsync(id, request.Justification)));
        }
        catch (Exception ex) when (ex is InvalidOperationException or FiscalNaoConfiguradoException)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("fiscal/nfce/{id:guid}/receipt")]
    [RequireIntegrationScope(IntegrationScope.FiscalRead)]
    public async Task<IActionResult> GetFiscalReceipt(Guid id)
    {
        var external = await db.NotasFiscaisEmitidas.AsNoTracking()
            .AnyAsync(n => n.Id == id && n.Origem == NotaFiscalOrigem.IntegracaoExterna);
        if (!external) return NotFound();
        var receipt = await emissao.ObterCupomAsync(id);
        return receipt is null ? NotFound() : Ok(receipt);
    }

    private static IntegrationFiscalNoteResponse ToIntegrationResponse(NotaFiscalEmitida note) => new(
        note.Id,
        note.ExternalSource ?? string.Empty,
        note.ExternalDocumentId ?? string.Empty,
        note.Status.ToString(),
        note.ValorTotalEmCentavos,
        note.Serie,
        note.Numero,
        note.ChaveAcesso,
        note.Protocolo,
        note.MotivoRejeicao,
        note.EmitidoEm,
        note.AutorizadoEm,
        note.CanceladoEm,
        note.CreatedAt);

    [GeneratedRegex("[^0-9]")]
    private static partial Regex DigitsOnly();
}
