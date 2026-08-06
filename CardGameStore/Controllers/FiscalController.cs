// =============================================================================
// FiscalController.cs — Configuração fiscal, certificado A1, naturezas de
// operação e exportação de XMLs de NFC-e pro contador.
// =============================================================================

using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Middleware;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/fiscal")]
[Authorize(Policy = "AdminOnly")]
[RequireModule("fiscal")]
[RequireOperatorPermission(Permissao.Fiscal)]
[Produces("application/json")]
public class FiscalController : ControllerBase
{
    private readonly AppDbContext              _db;
    private readonly FiscalXmlExportService    _export;
    private readonly INfceEmissionService      _emissao;
    private readonly CatalogDbContext          _catalog;
    private readonly ITenantContext            _tenant;
    private readonly IbptTaxService            _ibpt;
    private readonly IAuditService             _audit;
    // Criptografia e validação de certificado saíram daqui junto com a escrita
    // da config: vivem em FiscalConfigService, compartilhado com o portal do
    // contador (ver o cabeçalho daquele arquivo).
    private readonly FiscalConfigService       _configService;
    private readonly IConciliacaoFiscalService _conciliacao;
    private readonly IAlertaFiscalService      _alertas;
    private readonly INfceSchemaValidator      _schemaValidator;

    public FiscalController(
        AppDbContext db, FiscalXmlExportService export, INfceEmissionService emissao,
        CatalogDbContext catalog, ITenantContext tenant, IbptTaxService ibpt, IAuditService audit,
        FiscalConfigService configService, IConciliacaoFiscalService conciliacao,
        IAlertaFiscalService alertas, INfceSchemaValidator schemaValidator)
    {
        _db            = db;
        _export        = export;
        _emissao       = emissao;
        _catalog       = catalog;
        _tenant        = tenant;
        _ibpt          = ibpt;
        _audit         = audit;
        _configService = configService;
        _conciliacao   = conciliacao;
        _alertas       = alertas;
        _schemaValidator = schemaValidator;
    }

    /// <summary>Agrega configuração, certificado, regra fiscal padrão, produtos e notas das
    /// últimas 24h num único payload — usado pela tela "Visão geral" pra responder em poucos
    /// segundos se o fiscal está pronto pra emitir e qual é a próxima ação.</summary>
    // ── GET /api/fiscal/saude ──────────────────────────────────────────────────
    [HttpGet("saude")]
    public async Task<IActionResult> GetSaude(CancellationToken ct)
    {
        var cfg = await _db.FiscalConfigs.FindAsync(new object?[] { FiscalConfig.SingletonId }, ct);
        var temNaturezaPadrao = await _db.NaturezasOperacao.AnyAsync(n => n.IsPadrao, ct);
        var ibpt = await _ibpt.ObterStatusAsync(ct);

        var empresaCompleta = cfg is not null &&
            !string.IsNullOrWhiteSpace(cfg.Cnpj) &&
            !string.IsNullOrWhiteSpace(cfg.RazaoSocial) &&
            !string.IsNullOrWhiteSpace(cfg.Logradouro) &&
            !string.IsNullOrWhiteSpace(cfg.CodigoMunicipioIbge) &&
            !string.IsNullOrWhiteSpace(cfg.Uf);

        var certificadoConfigurado = cfg?.CertificadoConfigurado == true;
        int? diasParaVencerCertificado = cfg?.CertificadoValidade.HasValue == true
            ? (int)(cfg.CertificadoValidade!.Value.Date - DateTime.UtcNow.Date).TotalDays
            : null;
        var certificadoVencido = diasParaVencerCertificado is < 0;
        var certificadoPertoDeVencer = diasParaVencerCertificado is >= 0 and <= 30;

        var produtosSemNcm = await _db.Products.CountAsync(p => p.IsActive && string.IsNullOrEmpty(p.Ncm), ct);

        var desde24h = DateTime.UtcNow.AddHours(-24);
        var notas24h = await _db.NotasFiscaisEmitidas
            .Where(n => n.CreatedAt >= desde24h)
            .GroupBy(n => n.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int Contagem24h(NotaFiscalStatus s) => notas24h.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

        var pendentesQuery = _db.NotasFiscaisEmitidas.Where(n =>
            n.Status == NotaFiscalStatus.PendenteEmissao ||
            n.Status == NotaFiscalStatus.AutorizadaContingencia ||
            n.Status == NotaFiscalStatus.ResultadoIncerto);
        var pendentesCount = await pendentesQuery.CountAsync(ct);
        var pendenteMaisAntiga = pendentesCount > 0
            ? await pendentesQuery.OrderBy(n => n.CreatedAt).Select(n => n.CreatedAt).FirstAsync(ct)
            : (DateTime?)null;
        var rejeitadas24h = Contagem24h(NotaFiscalStatus.Rejeitada);

        // Checklist de ativação, na ordem em que o lojista deve resolver (seção 4.2 do
        // wizard: Empresa → Certificado → Regras → Produtos → Homologação/Produção).
        var checklist = new[]
        {
            new { Etapa = "Empresa",       Concluido = empresaCompleta },
            new { Etapa = "Certificado",   Concluido = certificadoConfigurado && !certificadoVencido },
            new { Etapa = "RegrasFiscais", Concluido = temNaturezaPadrao },
            new { Etapa = "Produtos",      Concluido = produtosSemNcm == 0 },
            new { Etapa = "Homologacao",   Concluido = cfg?.Ambiente == AmbienteFiscal.Producao },
        };

        // Pendências ordenadas por impacto: primeiro o que bloqueia emissão, depois o
        // que só requer atenção.
        var pendencias = new List<object>();
        if (!empresaCompleta)
            pendencias.Add(new { Categoria = "ConfiguracaoLoja", Mensagem = "Complete os dados da empresa (CNPJ, endereço, município).", Bloqueia = true });
        if (!certificadoConfigurado)
            pendencias.Add(new { Categoria = "ConfiguracaoLoja", Mensagem = "Envie o certificado digital A1.", Bloqueia = true });
        else if (certificadoVencido)
            pendencias.Add(new { Categoria = "ConfiguracaoLoja", Mensagem = "O certificado A1 está vencido.", Bloqueia = true });
        else if (certificadoPertoDeVencer)
            pendencias.Add(new { Categoria = "ConfiguracaoLoja", Mensagem = $"O certificado A1 vence em {diasParaVencerCertificado} dia(s).", Bloqueia = false });
        if (!temNaturezaPadrao)
            pendencias.Add(new { Categoria = "RegraFiscal", Mensagem = "Cadastre uma natureza de operação padrão.", Bloqueia = true });
        if (produtosSemNcm > 0)
            pendencias.Add(new { Categoria = "CadastroProduto", Mensagem = $"{produtosSemNcm} produto(s) sem NCM cadastrado.", Bloqueia = false });
        if (ibpt.ProdutosVencidos > 0)
            pendencias.Add(new { Categoria = "CadastroProduto", Mensagem = $"{ibpt.ProdutosVencidos} produto(s) com tabela IBPT vencida.", Bloqueia = false });
        if (pendentesCount > 0)
            pendencias.Add(new { Categoria = "Comunicacao", Mensagem = $"{pendentesCount} nota(s) pendente(s) ou em contingência.", Bloqueia = false });
        if (rejeitadas24h > 0)
            pendencias.Add(new { Categoria = "Comunicacao", Mensagem = $"{rejeitadas24h} nota(s) rejeitada(s) nas últimas 24h.", Bloqueia = false });
        // XML-002: operar sem o pacote de schemas é legítimo (a SEFAZ continua
        // validando), mas não pode ser invisível — senão o sistema parece ter uma
        // barreira antes da transmissão que na verdade não existe.
        if (!_schemaValidator.Disponivel)
            pendencias.Add(new { Categoria = "ConfiguracaoLoja", Mensagem = "Validação de schema XSD indisponível: erros de leiaute só serão descobertos pela rejeição da SEFAZ.", Bloqueia = false });

        // Homologação não bloqueia (a loja pode testar antes de ir pra produção), mas
        // também não deixa o status virar "Pronto" — nota emitida em Homologação não
        // tem valor fiscal, então "pronto pra emitir" seria enganoso nesse ambiente.
        var emHomologacao = cfg?.Ambiente != AmbienteFiscal.Producao;
        if (emHomologacao)
            pendencias.Add(new { Categoria = "Ambiente", Mensagem = "Loja ainda em ambiente de Homologação — notas emitidas não têm valor fiscal.", Bloqueia = false });

        var bloqueado = !empresaCompleta || !certificadoConfigurado || certificadoVencido || !temNaturezaPadrao;
        var status = bloqueado ? "Bloqueado" : pendencias.Count > 0 ? "RequerAtencao" : "Pronto";

        var proximaAcao =
            !empresaCompleta ? "Completar dados da empresa" :
            !certificadoConfigurado || certificadoVencido ? "Enviar certificado A1" :
            !temNaturezaPadrao ? "Cadastrar natureza fiscal padrão" :
            produtosSemNcm > 0 ? $"Corrigir {produtosSemNcm} produto(s) sem NCM" :
            pendentesCount > 0 ? "Reprocessar notas pendentes" :
            rejeitadas24h > 0 ? "Revisar notas rejeitadas" :
            emHomologacao ? "Mudar para o ambiente de Produção" :
            "Nenhuma ação necessária";

        return Ok(new
        {
            Status    = status,
            Ambiente  = (cfg?.Ambiente ?? AmbienteFiscal.Homologacao).ToString(),
            Checklist = checklist,
            Notas = new
            {
                Autorizadas24h          = Contagem24h(NotaFiscalStatus.Autorizada),
                Rejeitadas24h           = rejeitadas24h,
                PendentesTotal          = pendentesCount,
                PendenteMaisAntigaDesde = pendenteMaisAntiga,
            },
            Certificado = new
            {
                Configurado    = certificadoConfigurado,
                cfg?.CertificadoValidade,
                DiasParaVencer = diasParaVencerCertificado,
                Vencido        = certificadoVencido,
            },
            Produtos = new
            {
                ibpt.ProdutosAtivos,
                SemNcm = produtosSemNcm,
                ibpt.ProdutosPendentes,
                ibpt.ProdutosVencidos,
            },
            Pendencias  = pendencias,
            ProximaAcao = proximaAcao,
        });
    }

    /// <summary>Retorna a configuração fiscal da loja (CNPJ, endereço, regime tributário,
    /// certificado, CSC). Nunca inclui a senha do certificado nem o CSC token.</summary>
    // ── GET /api/fiscal/config ────────────────────────────────────────────────
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var cfg = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
        return Ok(FiscalConfigService.ToDto(cfg));
    }

    /// <summary>Atualiza a configuração fiscal (update parcial — só os campos enviados
    /// são alterados). Cria a linha de configuração se ainda não existir.</summary>
    /// <param name="req">Campos a atualizar; qualquer campo null/omitido mantém o valor atual.</param>
    // ── PUT /api/fiscal/config ────────────────────────────────────────────────
    [HttpPut("config")]
    public async Task<IActionResult> SaveConfig([FromBody] SaveFiscalConfigRequest req)
    {
        // Mesma escrita que o portal do contador usa — regras de regime, CSC,
        // IBPT e titularidade do certificado vivem em FiscalConfigService.
        var resultado = await _configService.SalvarAsync(req);
        if (!resultado.Ok) return BadRequest(new { Message = resultado.Erro });

        return Ok(FiscalConfigService.ToDto(resultado.Config));
    }

    [HttpGet("ibpt/status")]
    public async Task<IActionResult> GetIbptStatus(CancellationToken ct) =>
        Ok(await _ibpt.ObterStatusAsync(ct));

    /// <summary>Atualiza produtos incompletos ou já gerenciados pelo IBPT. Overrides manuais são preservados.</summary>
    [HttpPost("ibpt/sincronizar")]
    public async Task<IActionResult> SincronizarIbpt(CancellationToken ct)
    {
        try
        {
            var resultado = await _ibpt.SincronizarTodosAsync(ct);
            await _audit.LogAsync(
                "SincronizouTributosIbpt", "Product",
                details: $"atualizados={resultado.Atualizados}; manuais_preservados={resultado.IgnoradosManuais}; falhas={resultado.Falhas}",
                httpContext: HttpContext);
            return Ok(resultado);
        }
        catch (IbptIntegrationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Rede de segurança: se um timeout escapar do serviço, ele ainda é
            // indisponibilidade de terceiro, não erro do nosso servidor. 500 aqui
            // sugeriria ao lojista que o sistema quebrou e que adianta insistir.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = "O IBPT não respondeu dentro do tempo limite. " +
                          "Os produtos já sincronizados foram preservados; tente de novo mais tarde.",
            });
        }
    }

    /// <summary>Valida e salva o certificado digital A1 (.pfx) usado para assinar NFC-e.
    /// O arquivo e a senha são criptografados antes de persistir; um certificado inválido
    /// (senha errada, expirado, corrompido) é rejeitado sem alterar a configuração atual.</summary>
    /// <param name="file">Arquivo .pfx do certificado (máx 2 MB).</param>
    /// <param name="senha">Senha do certificado.</param>
    // ── POST /api/fiscal/certificado — upload do .pfx + senha ────────────────
    [HttpPost("certificado")]
    [RequestSizeLimit(2 * 1024 * 1024)] // 2 MB — certificados .pfx são pequenos
    public async Task<IActionResult> UploadCertificado(IFormFile file, [FromForm] string senha)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { Message = "Arquivo de certificado (.pfx) inválido ou vazio." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var (erro, info) = await _configService.SalvarCertificadoAsync(ms.ToArray(), senha);
        if (erro is not null) return BadRequest(new { Message = erro });

        return Ok(new
        {
            Message   = "Certificado validado e salvo com sucesso.",
            Validade  = info!.NotAfter,
            DiasRestantes = (int)(info.NotAfter.Date - DateTime.UtcNow.Date).TotalDays,
        });
    }

    /// <summary>Lista as naturezas de operação (CFOP/CSOSN) cadastradas, com a padrão primeiro.</summary>
    // ── GET /api/fiscal/naturezas-operacao ────────────────────────────────────
    [HttpGet("naturezas-operacao")]
    public async Task<IActionResult> ListNaturezas()
    {
        var naturezas = await _db.NaturezasOperacao
            .OrderByDescending(n => n.IsPadrao)
            .ThenBy(n => n.Descricao)
            .ToListAsync();

        return Ok(naturezas);
    }

    private static readonly string[] CsosnSuportados =
        { "101", "102", "103", "201", "202", "203", "300", "400", "500", "900" };

    private BadRequestObjectResult? ValidarCsosn(string? csosn)
    {
        if (string.IsNullOrWhiteSpace(csosn)) return null;
        if (!CsosnSuportados.Contains(csosn))
            return BadRequest(new
            {
                Message = $"CSOSN \"{csosn}\" não é suportado. Use um destes: {string.Join(", ", CsosnSuportados)}."
            });
        return null;
    }

    private BadRequestObjectResult? ValidarRegraFiscal(SaveNaturezaRequest req)
    {
        if (ValidarCsosn(req.Csosn) is BadRequestObjectResult erro) return erro;
        if (req.OrigemMercadoria is < 0 or > 8)
            return BadRequest(new { Message = "Origem da mercadoria deve estar entre 0 e 8." });
        if (req.IbsCbsCst is null || req.IbsCbsCst.Length != 3 || !req.IbsCbsCst.All(char.IsDigit))
            return BadRequest(new { Message = "CST IBS/CBS deve conter 3 dígitos." });
        if (req.IbsCbsClassTrib is null || req.IbsCbsClassTrib.Length != 6 || !req.IbsCbsClassTrib.All(char.IsDigit))
            return BadRequest(new { Message = "cClassTrib IBS/CBS deve conter 6 dígitos." });

        if (req.Csosn is "201" or "202" or "203")
        {
            if (req.ModalidadeBcSt is null or < 0 or > 6)
                return BadRequest(new { Message = "ICMS-ST exige modalidade da BC-ST entre 0 e 6." });
            if (req.AliquotaIcmsSt is null or <= 0 || req.AliquotaIcmsProprio is null or < 0)
                return BadRequest(new { Message = "ICMS-ST exige alíquota ST e alíquota da operação própria." });
            if (req.ModalidadeBcSt == 4 && req.PercentualMvaSt is null or < 0)
                return BadRequest(new { Message = "Modalidade MVA exige o percentual de MVA-ST." });
            if (req.ModalidadeBcSt is 0 or 1 or 2 or 3 or 5 && req.BaseStFixaEmCentavos is null or <= 0)
                return BadRequest(new { Message = "A modalidade selecionada exige base/pauta ST fixa por unidade." });
        }

        return ValidarRegraFiscalRegimeNormal(req);
    }

    private static readonly string[] CstSuportados =
        { "00", "10", "20", "30", "40", "41", "50", "60", "70", "90" };

    /// <summary>
    /// Valida os campos que só existem fora do Simples. A natureza não sabe em
    /// qual regime a loja está — e nem precisa saber: guarda os dois conjuntos e
    /// a emissão escolhe pelo CRT. O que se valida aqui é a coerência interna do
    /// conjunto do regime normal, pra o erro aparecer no cadastro em vez de
    /// virar rejeição da SEFAZ na hora da venda.
    /// </summary>
    private BadRequestObjectResult? ValidarRegraFiscalRegimeNormal(SaveNaturezaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Cst)) return null;

        if (!CstSuportados.Contains(req.Cst))
            return BadRequest(new
            {
                Message = $"CST de ICMS \"{req.Cst}\" não é suportado. Use um destes: {string.Join(", ", CstSuportados)}."
            });

        // Onde há ICMS destacado, a alíquota própria é obrigatória.
        if (req.Cst is "00" or "10" or "20" or "70" && req.AliquotaIcmsProprio is null or < 0)
            return BadRequest(new { Message = $"CST {req.Cst} exige a alíquota de ICMS da operação própria." });

        if (req.Cst is "20" or "70" && req.PercentualReducaoBc is null or <= 0)
            return BadRequest(new { Message = $"CST {req.Cst} exige o percentual de redução da base de cálculo." });

        // CSTs em que a loja é a substituta: mesmas exigências do ST no Simples.
        if (req.Cst is "10" or "30" or "70")
        {
            if (req.ModalidadeBcSt is null or < 0 or > 6)
                return BadRequest(new { Message = $"CST {req.Cst} exige modalidade da BC-ST entre 0 e 6." });
            if (req.AliquotaIcmsSt is null or <= 0)
                return BadRequest(new { Message = $"CST {req.Cst} exige a alíquota do ICMS-ST." });
            if (req.ModalidadeBcSt == 4 && req.PercentualMvaSt is null or < 0)
                return BadRequest(new { Message = "Modalidade MVA exige o percentual de MVA-ST." });
            if (req.ModalidadeBcSt is 0 or 1 or 2 or 3 or 5 && req.BaseStFixaEmCentavos is null or <= 0)
                return BadRequest(new { Message = "A modalidade selecionada exige base/pauta ST fixa por unidade." });
        }

        foreach (var (cst, tributo) in new[] { (req.CstPis, "PIS"), (req.CstCofins, "COFINS") })
        {
            if (string.IsNullOrWhiteSpace(cst)) continue;
            if (cst.Length != 2 || !cst.All(char.IsDigit))
                return BadRequest(new { Message = $"CST de {tributo} deve conter 2 dígitos." });
        }

        return null;
    }

    private static void AplicarRegraFiscal(NaturezaOperacao natureza, SaveNaturezaRequest req)
    {
        natureza.Descricao = req.Descricao;
        natureza.Cfop = req.Cfop;
        natureza.Csosn = req.Csosn;
        natureza.PercentualCreditoIcmsSn = req.Csosn is "101" or "201" ? req.PercentualCreditoSn : null;
        natureza.OrigemMercadoria = req.OrigemMercadoria;
        natureza.ModalidadeBcSt = req.ModalidadeBcSt;
        natureza.PercentualMvaSt = req.PercentualMvaSt;
        natureza.PercentualReducaoBcSt = req.PercentualReducaoBcSt;
        natureza.AliquotaIcmsSt = req.AliquotaIcmsSt;
        natureza.AliquotaIcmsProprio = req.AliquotaIcmsProprio;
        natureza.AliquotaFcpSt = req.AliquotaFcpSt;
        natureza.BaseStFixaEmCentavos = req.BaseStFixaEmCentavos;
        natureza.IbsCbsCst = req.IbsCbsCst;
        natureza.IbsCbsClassTrib = req.IbsCbsClassTrib;

        natureza.Cst = string.IsNullOrWhiteSpace(req.Cst) ? null : req.Cst;
        natureza.PercentualReducaoBc = req.PercentualReducaoBc;
        natureza.AliquotaFcp = req.AliquotaFcp;
        natureza.BaseStRetidaEmCentavos = req.BaseStRetidaEmCentavos;
        natureza.ValorStRetidoEmCentavos = req.ValorStRetidoEmCentavos;
        natureza.CstPis = string.IsNullOrWhiteSpace(req.CstPis) ? null : req.CstPis;
        natureza.CstCofins = string.IsNullOrWhiteSpace(req.CstCofins) ? null : req.CstCofins;
        natureza.AliquotaPis = req.AliquotaPis;
        natureza.AliquotaCofins = req.AliquotaCofins;
    }

    /// <summary>Cria uma natureza de operação (CFOP/CSOSN). Marcar como padrão desmarca
    /// atomicamente a natureza padrão anterior (só pode haver uma).</summary>
    /// <param name="req">CFOP, CSOSN (só os suportados pelo motor de emissão) e se é a padrão.</param>
    // ── POST /api/fiscal/naturezas-operacao ───────────────────────────────────
    [HttpPost("naturezas-operacao")]
    public async Task<IActionResult> CreateNatureza([FromBody] SaveNaturezaRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (ValidarRegraFiscal(req) is BadRequestObjectResult erro) return erro;

        var natureza = new NaturezaOperacao
        {
            IsPadrao  = req.IsPadrao,
        };
        AplicarRegraFiscal(natureza, req);

        // Trocar o padrão (limpar os outros + gravar este) precisa ser atômico:
        // duas requisições concorrentes marcando padrão=true só podem ter uma vencedora
        // graças ao índice único parcial ix_naturezas_operacao_unica_padrao.
        //
        // AppDbContext usa EnableRetryOnFailure — uma transação manual solta
        // não é permitida com uma execution strategy que faz retry (o EF
        // lança InvalidOperationException dentro do SaveChangesAsync); precisa
        // rodar o bloco inteiro através de CreateExecutionStrategy().
        var strategy = _db.Database.CreateExecutionStrategy();
        var conflito = false;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (natureza.IsPadrao)
                    await _db.NaturezasOperacao.Where(n => n.IsPadrao)
                        .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsPadrao, false));

                _db.NaturezasOperacao.Add(natureza);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();
                conflito = true;
            }
        });

        if (conflito)
            return Conflict(new { Message = "Outra natureza foi marcada como padrão ao mesmo tempo. Tente novamente." });

        return Ok(natureza);
    }

    /// <summary>Atualiza uma natureza de operação existente. Mesma regra de "padrão único"
    /// da criação — marcar esta como padrão desmarca qualquer outra atomicamente.</summary>
    /// <param name="id">Id da natureza de operação.</param>
    /// <param name="req">Novos valores de CFOP, CSOSN e se é a padrão.</param>
    // ── PUT /api/fiscal/naturezas-operacao/{id} ───────────────────────────────
    [HttpPut("naturezas-operacao/{id:guid}")]
    public async Task<IActionResult> UpdateNatureza(Guid id, [FromBody] SaveNaturezaRequest req)
    {
        if (ValidarRegraFiscal(req) is BadRequestObjectResult erro) return erro;

        var natureza = await _db.NaturezasOperacao.FindAsync(id);
        if (natureza is null) return NotFound();

        AplicarRegraFiscal(natureza, req);
        natureza.UpdatedAt = DateTime.UtcNow;

        // AppDbContext usa EnableRetryOnFailure — precisa rodar através de
        // CreateExecutionStrategy() (ver comentário em CreateNatureza acima).
        var strategy = _db.Database.CreateExecutionStrategy();
        var conflito = false;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (req.IsPadrao && !natureza.IsPadrao)
                    await _db.NaturezasOperacao.Where(n => n.IsPadrao && n.Id != id)
                        .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsPadrao, false));
                natureza.IsPadrao = req.IsPadrao;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();
                conflito = true;
            }
        });

        if (conflito)
            return Conflict(new { Message = "Outra natureza foi marcada como padrão ao mesmo tempo. Tente novamente." });

        return Ok(natureza);
    }

    /// <summary>Remove uma natureza de operação.</summary>
    /// <param name="id">Id da natureza de operação.</param>
    // ── DELETE /api/fiscal/naturezas-operacao/{id} ────────────────────────────
    [HttpDelete("naturezas-operacao/{id:guid}")]
    public async Task<IActionResult> DeleteNatureza(Guid id)
    {
        var natureza = await _db.NaturezasOperacao.FindAsync(id);
        if (natureza is null) return NotFound();

        _db.NaturezasOperacao.Remove(natureza);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Lista notas fiscais emitidas com paginação e filtro por status. Também
    /// retorna quantas notas estão paradas esperando o retry automático (pendentes ou em
    /// contingência) e há quanto tempo a mais antiga está parada, como sinal de alerta.</summary>
    /// <param name="status">Filtro por status (ex: "Autorizada", "PendenteEmissao", "Rejeitada").</param>
    /// <param name="page">Número da página (base 1, padrão 1).</param>
    /// <param name="pageSize">Registros por página (padrão 30).</param>
    // ── GET /api/fiscal/notas?status=&page=&pageSize= ─────────────────────────
    [HttpGet("notas")]
    public async Task<IActionResult> ListNotas(
        [FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var q = _db.NotasFiscaisEmitidas.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<NotaFiscalStatus>(status, out var statusEnum))
            q = q.Where(n => n.Status == statusEnum);

        var total = await q.CountAsync();
        var itens = await q.OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new
            {
                n.Id,
                Origem = n.Origem.ToString(),
                n.ComandaId,
                n.VendaAvulsaId,
                Status = n.Status.ToString(),
                n.ValorTotalEmCentavos,
                n.Serie,
                n.Numero,
                n.ChaveAcesso,
                n.Protocolo,
                n.MotivoRejeicao,
                n.EmitidoEm,
                n.CanceladoEm,
                n.InutilizadoEm,
                n.ErpEstornadoEm,
                n.ErpEstornoErro,
                n.TentativasReprocessamento,
                n.CreatedAt,
            })
            .ToListAsync();

        // Visibilidade pro admin: quantas notas estão paradas esperando o retry automático
        // (pendentes de verdade + em contingência aguardando retransmissão) e há quanto
        // tempo a mais antiga está parada — sinal de que algo precisa de atenção.
        var pendentesQuery = _db.NotasFiscaisEmitidas.Where(n =>
            n.Status == NotaFiscalStatus.PendenteEmissao ||
            n.Status == NotaFiscalStatus.AutorizadaContingencia ||
            n.Status == NotaFiscalStatus.ResultadoIncerto);
        var pendentesCount = await pendentesQuery.CountAsync();
        var pendenteMaisAntiga = pendentesCount > 0
            ? await pendentesQuery.OrderBy(n => n.CreatedAt).Select(n => n.CreatedAt).FirstAsync()
            : (DateTime?)null;

        return Ok(new
        {
            items = itens, total, totalPages = (int)Math.Ceiling(total / (double)pageSize),
            pendentesCount, pendenteMaisAntiga,
        });
    }

    /// <summary>Emissão manual tardia de NFC-e para uma comanda já fechada — usada quando o
    /// admin optou por NÃO emitir no fechamento (checkbox desmarcado) e decide emitir depois
    /// pelo histórico. Rejeita se já existe nota para esta comanda.</summary>
    /// <param name="id">Id da comanda.</param>
    // ── POST /api/fiscal/emitir/comanda/{id} ──────────────────────────────────
    // Emissão manual tardia — usada quando o admin optou por NÃO emitir no fechamento
    // (checkbox desmarcado) e decidiu emitir depois pelo histórico.
    [HttpPost("emitir/comanda/{id:guid}")]
    public async Task<IActionResult> EmitirNotaComanda(Guid id)
    {
        var jaExiste = await _db.NotasFiscaisEmitidas.AnyAsync(n => n.Origem == NotaFiscalOrigem.Comanda && n.ComandaId == id);
        if (jaExiste)
            return Conflict(new { Message = "Já existe uma nota fiscal para esta comanda. Use reprocessar/cancelar em vez de emitir de novo." });

        var nota = await _emissao.EmitirParaComandaAsync(id);
        return Ok(new { nota.Id, Status = nota.Status.ToString(), nota.MotivoRejeicao });
    }

    /// <summary>Emissão manual tardia de NFC-e para uma venda avulsa já registrada.
    /// Rejeita se já existe nota para esta venda.</summary>
    /// <param name="id">Id da venda avulsa.</param>
    // ── POST /api/fiscal/emitir/venda-avulsa/{id} ─────────────────────────────
    [HttpPost("emitir/venda-avulsa/{id:guid}")]
    public async Task<IActionResult> EmitirNotaVendaAvulsa(Guid id)
    {
        var jaExiste = await _db.NotasFiscaisEmitidas.AnyAsync(n => n.Origem == NotaFiscalOrigem.VendaAvulsa && n.VendaAvulsaId == id);
        if (jaExiste)
            return Conflict(new { Message = "Já existe uma nota fiscal para esta venda. Use reprocessar/cancelar em vez de emitir de novo." });

        var nota = await _emissao.EmitirParaVendaAvulsaAsync(id);
        return Ok(new { nota.Id, Status = nota.Status.ToString(), nota.MotivoRejeicao });
    }

    /// <summary>Tenta transmitir de novo uma nota pendente/rejeitada/em contingência.
    /// Notas já autorizadas voltam sem tentar de novo; acima do limite de tentativas
    /// também não tenta.</summary>
    /// <param name="id">Id da nota fiscal.</param>
    // ── POST /api/fiscal/notas/{id}/reprocessar ───────────────────────────────
    [HttpPost("notas/{id:guid}/reprocessar")]
    public async Task<IActionResult> ReprocessarNota(Guid id)
    {
        var nota = await _emissao.ReprocessarAsync(id);
        return Ok(new { nota.Id, Status = nota.Status.ToString(), nota.MotivoRejeicao });
    }

    /// <summary>Cancela uma NFC-e autorizada, dentro da janela legal de 30 minutos após a
    /// emissão. Exige justificativa com no mínimo 15 caracteres (exigência da SEFAZ).</summary>
    /// <param name="id">Id da nota fiscal.</param>
    /// <param name="req">Justificativa do cancelamento (mín. 15 caracteres).</param>
    // ── POST /api/fiscal/notas/{id}/cancelar ──────────────────────────────────
    [HttpPost("notas/{id:guid}/cancelar")]
    public async Task<IActionResult> CancelarNota(Guid id, [FromBody] CancelarNotaRequest req)
    {
        try
        {
            var nota = await _emissao.CancelarAsync(id, req.Justificativa);
            return Ok(new
            {
                nota.Id,
                Status = nota.Status.ToString(),
                nota.ErpEstornadoEm,
                nota.ErpEstornoErro,
            });
        }
        // FiscalNaoConfiguradoException herda de Exception, não de
        // InvalidOperationException — sem este catch, loja mal configurada (CNPJ
        // inválido, por exemplo) recebia 500 "Erro interno" em vez de saber o que
        // corrigir. Vale pra emissão, cancelamento e inutilização.
        catch (FiscalNaoConfiguradoException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Inutiliza na SEFAZ uma faixa de numeração que foi abandonada e não
    /// contém documento autorizado. A operação é fiscalmente irreversível.</summary>
    [HttpPost("inutilizacoes")]
    public async Task<IActionResult> InutilizarFaixa([FromBody] InutilizarFaixaRequest req)
    {
        try
        {
            var registro = await _emissao.InutilizarFaixaAsync(
                req.Ano, req.Serie, req.NumeroInicial, req.NumeroFinal, req.Justificativa);
            return Ok(new
            {
                registro.Id,
                registro.Ano,
                registro.Serie,
                registro.NumeroInicial,
                registro.NumeroFinal,
                registro.Protocolo,
                registro.InutilizadoEm,
            });
        }
        catch (FiscalNaoConfiguradoException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("notas/{id:guid}/reprocessar-estorno-erp")]
    public async Task<IActionResult> ReprocessarEstornoErp(Guid id)
    {
        try
        {
            var nota = await _emissao.ReprocessarEstornoErpAsync(id);
            return Ok(new { nota.Id, nota.ErpEstornadoEm, nota.ErpEstornoErro });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Retorna os dados formatados do cupom NFC-e (itens, total, chave de acesso,
    /// QR Code) pra exibir/imprimir no admin.</summary>
    /// <param name="id">Id da nota fiscal.</param>
    // ── GET /api/fiscal/notas/{id}/cupom ──────────────────────────────────────
    [HttpGet("notas/{id:guid}/cupom")]
    public async Task<IActionResult> ObterCupom(Guid id)
    {
        var cupom = await _emissao.ObterCupomAsync(id);
        return cupom is null ? NotFound() : Ok(cupom);
    }

    /// <summary>
    /// Conciliação fiscal do período: toda venda tributável com o documento que
    /// tem — ou a falta dele (CON-001). Diferente dos demais relatórios, parte
    /// das VENDAS e não das notas, então enxerga a venda fechada sem emissão,
    /// que hoje não aparece em lugar nenhum.
    /// </summary>
    /// <param name="inicio">Data inicial do período (inclusive).</param>
    /// <param name="fim">Data final do período (inclusive).</param>
    // ── GET /api/fiscal/conciliacao?inicio=&fim= ──────────────────────────────
    [HttpGet("conciliacao")]
    public async Task<IActionResult> GetConciliacao(
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        if (fim.Date < inicio.Date)
            return BadRequest(new { Message = "O período final não pode ser anterior ao inicial." });

        return Ok(await _conciliacao.ConciliarAsync(inicio.Date, fim.Date));
    }

    /// <summary>
    /// Painel de pendências fiscais (CON-002): resultado incerto, contingência
    /// vencendo, rejeição, venda sem documento, lacuna de numeração e exportação
    /// mensal atrasada — cada uma com severidade, idade, responsável e estado de
    /// resolução.
    ///
    /// A lista é reconciliada a partir do estado real, não acumulada por
    /// disparos: um alerta aberto aqui é uma pendência que existe agora.
    /// </summary>
    /// <param name="incluirResolvidos">Traz também o histórico já resolvido.</param>
    // ── GET /api/fiscal/alertas?incluirResolvidos= ────────────────────────────
    [HttpGet("alertas")]
    public async Task<IActionResult> GetAlertas([FromQuery] bool incluirResolvidos = false) =>
        Ok(await _alertas.ListarAsync(incluirResolvidos));

    /// <summary>
    /// Catálogo versionado de regras de IBS/CBS (RTC-001): o que está em vigor
    /// hoje para este contribuinte, com alíquotas, fonte oficial e data de
    /// consulta, mais o histórico de faixas. É o que o contador precisa ver para
    /// conferir se o motor está aplicando a regra certa — e para saber com que
    /// fonte ela foi registrada.
    /// </summary>
    // ── GET /api/fiscal/regras-ibs-cbs ────────────────────────────────────────
    [HttpGet("regras-ibs-cbs")]
    public async Task<IActionResult> GetRegrasIbsCbs(CancellationToken ct)
    {
        var cfg = await _db.FiscalConfigs.FindAsync(new object?[] { FiscalConfig.SingletonId }, ct);
        var perfil = cfg is null ? PerfilIbsCbs.SimplesNacional : CatalogoRegrasIbsCbs.PerfilDe(cfg);
        var hoje = DateOnly.FromDateTime(BrazilTime.NowBr().Date);
        var vigente = CatalogoRegrasIbsCbs.Para(hoje, perfil);
        var ambienteProducao = cfg?.Ambiente == AmbienteFiscal.Producao;

        return Ok(new
        {
            Perfil = perfil.ToString(),
            RevisaoRecomendadaEm = CatalogoRegrasIbsCbs.RevisaoRecomendadaEm,
            RevisaoVencida = hoje >= CatalogoRegrasIbsCbs.RevisaoRecomendadaEm,
            Vigente = vigente is null ? null : new
            {
                vigente.Versao,
                vigente.VigenciaInicio,
                vigente.VigenciaFim,
                vigente.AliquotaIbsUf,
                vigente.AliquotaIbsMun,
                vigente.AliquotaCbs,
                vigente.CstSuportados,
                vigente.DestaqueObrigatorio,
                vigente.FonteOficial,
                vigente.ConsultadoEm,
                vigente.Observacao,
                // O que efetivamente sai no XML hoje: homologação sempre destaca;
                // produção só quando a regra disser que o destaque já é exigido.
                DestacaNoXmlAgora = vigente.DestaqueObrigatorio || !ambienteProducao,
            },
            Catalogo = CatalogoRegrasIbsCbs.Todas.Select(r => new
            {
                r.Versao,
                r.VigenciaInicio,
                r.VigenciaFim,
                Perfis = r.Perfis.Select(p => p.ToString()),
                r.AliquotaIbsUf,
                r.AliquotaIbsMun,
                r.AliquotaCbs,
                r.DestaqueObrigatorio,
                r.FonteOficial,
                r.ConsultadoEm,
                r.Observacao,
            }),
        });
    }

    /// <summary>Recalcula as pendências agora, sem esperar o ciclo de 15 minutos.</summary>
    // ── POST /api/fiscal/alertas/sincronizar ──────────────────────────────────
    [HttpPost("alertas/sincronizar")]
    public async Task<IActionResult> SincronizarAlertas()
    {
        await _alertas.SincronizarAsync();
        return Ok(await _alertas.ListarAsync());
    }

    /// <summary>
    /// O usuário autenticado passa a responder por esta pendência. Assumir é ato
    /// próprio — por isso não recebe um id de usuário no corpo: ninguém atribui
    /// responsabilidade fiscal a outra pessoa por uma chamada de API.
    /// </summary>
    /// <param name="id">Id do alerta.</param>
    // ── POST /api/fiscal/alertas/{id}/assumir ─────────────────────────────────
    [HttpPost("alertas/{id:guid}/assumir")]
    public async Task<IActionResult> AssumirAlerta(Guid id)
    {
        try
        {
            var alerta = await _alertas.AtribuirResponsavelAsync(id, GetUserId());
            await _audit.LogAsync(
                "AssumiuAlertaFiscal", nameof(AlertaFiscal), alerta.Id.ToString(),
                details: $"{{\"tipo\":\"{alerta.Tipo}\",\"chave\":\"{alerta.Chave}\"}}",
                httpContext: HttpContext);
            return Ok(new { alerta.Id, alerta.ResponsavelUserId, alerta.ResponsavelDefinidoEm });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Devolve a pendência para a fila, sem responsável.</summary>
    /// <param name="id">Id do alerta.</param>
    // ── DELETE /api/fiscal/alertas/{id}/responsavel ───────────────────────────
    [HttpDelete("alertas/{id:guid}/responsavel")]
    public async Task<IActionResult> LiberarAlerta(Guid id)
    {
        try
        {
            var alerta = await _alertas.AtribuirResponsavelAsync(id, null);
            await _audit.LogAsync(
                "LiberouAlertaFiscal", nameof(AlertaFiscal), alerta.Id.ToString(),
                details: $"{{\"tipo\":\"{alerta.Tipo}\"}}", httpContext: HttpContext);
            return Ok(new { alerta.Id, alerta.ResponsavelUserId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Confirmação humana de que a pendência foi tratada. Não suprime o fato: se
    /// ele continuar verdadeiro, o próximo ciclo reabre o alerta — por isso a
    /// observação é obrigatória e vai para a trilha de auditoria.
    /// </summary>
    /// <param name="id">Id do alerta.</param>
    /// <param name="req">Observação descrevendo o que foi feito.</param>
    // ── POST /api/fiscal/alertas/{id}/resolver ────────────────────────────────
    [HttpPost("alertas/{id:guid}/resolver")]
    public async Task<IActionResult> ResolverAlerta(Guid id, [FromBody] ResolverAlertaFiscalRequest req)
    {
        try
        {
            var alerta = await _alertas.ResolverAsync(id, GetUserId(), req.Observacao);
            await _audit.LogAsync(
                "ResolveuAlertaFiscal", nameof(AlertaFiscal), alerta.Id.ToString(),
                details: $"{{\"tipo\":\"{alerta.Tipo}\",\"chave\":\"{alerta.Chave}\"}}",
                httpContext: HttpContext);
            return Ok(new { alerta.Id, alerta.ResolvidoEm, alerta.ResolucaoObservacao });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token inválido: identificador de usuário ausente.");
        return id;
    }

    /// <summary>Gera um .zip com os XMLs de todas as NFC-e emitidas no período, pra
    /// entregar ao contador.</summary>
    /// <param name="inicio">Data inicial do período (inclusive).</param>
    /// <param name="fim">Data final do período (inclusive).</param>
    // ── GET /api/fiscal/exportar-xmls?inicio=&fim= ────────────────────────────
    [HttpGet("exportar-xmls")]
    public async Task<IActionResult> ExportarXmls([FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        if (fim.Date < inicio.Date)
            return BadRequest(new { Message = "O período final não pode ser anterior ao inicial." });

        var (inicioUtc, fimExclusivoUtc) = FiscalXmlExportService.NormalizarPeriodoInclusivo(inicio, fim);
        var zipBytes = await _export.GerarZipAsync(inicioUtc, fimExclusivoUtc);
        var fileName = $"xmls-fiscais-{inicio:yyyy-MM-dd}-a-{fim:yyyy-MM-dd}.zip";

        return File(zipBytes, "application/zip", fileName);
    }

    /// <summary>Convida um contador por e-mail. Se ele já tem conta cadastrada, vincula
    /// direto com status Approved (quem convida é o próprio lojista). Se ainda não tem
    /// conta, registra um convite "cego" — o vínculo Approved é criado automaticamente
    /// quando esse e-mail se cadastrar em /contador/cadastro.</summary>
    /// <param name="request">E-mail do contador a convidar.</param>
    // ── POST /api/fiscal/contador/convidar ────────────────────────────────────
    // Vincula um contador JÁ CADASTRADO (via /contador/cadastro) a esta loja,
    // com o vínculo nascendo Approved direto — quem convida é o próprio lojista.
    // Se o e-mail ainda não tem conta de contador, não dá pra pré-criar o vínculo
    // (ContadorTenantLink.ContadorAccountId exige uma conta já existente);
    // o lojista precisa pedir pro contador se cadastrar primeiro.
    [HttpPost("contador/convidar")]
    public async Task<IActionResult> ConvidarContador([FromBody] ConvidarContadorRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var email = request.Email.Trim().ToLowerInvariant();
        var tenantSlug = await _catalog.Tenants
            .AsNoTracking()
            .Where(t => t.Id == _tenant.TenantId)
            .Select(t => t.Slug)
            .SingleOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(tenantSlug))
            return Problem("Não foi possível identificar o link público desta loja.");

        var invitationPath = $"/contador/cadastro?tenantSlug={Uri.EscapeDataString(tenantSlug)}";
        var conta = await _catalog.ContadorAccounts.FirstOrDefaultAsync(c => c.Email == email);

        // Convite cego: contador ainda não tem conta. Guarda o convite — quando
        // ele se cadastrar com esse e-mail em /contador/cadastro, o vínculo
        // Approved é criado automaticamente (ver AuthService.RegisterContadorAsync).
        if (conta is null)
        {
            var jaConvidado = await _catalog.ContadorConvitesEmail
                .AnyAsync(c => c.Email == email && c.TenantId == _tenant.TenantId);
            if (jaConvidado)
                return Conflict(new { Message = "Este e-mail já foi convidado — aguarde o contador se cadastrar." });

            _catalog.ContadorConvitesEmail.Add(new ContadorConviteEmail
            {
                Email    = email,
                TenantId = _tenant.TenantId,
            });
            await _catalog.SaveChangesAsync();

            return Ok(new ConvidarContadorResponse(
                "Convite registrado — envie o link de cadastro ao contador. Quando ele usar o e-mail convidado, o acesso a esta loja será liberado automaticamente.",
                invitationPath));
        }

        var jaVinculado = await _catalog.ContadorTenantLinks
            .AnyAsync(l => l.ContadorAccountId == conta.Id && l.TenantId == _tenant.TenantId);
        if (jaVinculado)
            return Conflict(new { Message = "Este contador já tem acesso (ou solicitação pendente) a esta loja." });

        _catalog.ContadorTenantLinks.Add(new ContadorTenantLink
        {
            ContadorAccountId = conta.Id,
            TenantId          = _tenant.TenantId,
            Status            = ContadorLinkStatus.Approved,
        });
        await _catalog.SaveChangesAsync();

        return Ok(new ConvidarContadorResponse(
            $"Contador {conta.Name} vinculado com sucesso.",
            null));
    }

    /// <summary>Lista os vínculos de contador desta loja (aprovados e pendentes de
    /// aprovação), com nome/e-mail do contador.</summary>
    // ── GET /api/fiscal/contador/solicitacoes ─────────────────────────────────
    [HttpGet("contador/solicitacoes")]
    public async Task<IActionResult> ListSolicitacoesContador()
    {
        var solicitacoes = await _catalog.ContadorTenantLinks
            .Where(l => l.TenantId == _tenant.TenantId)
            .Join(_catalog.ContadorAccounts, l => l.ContadorAccountId, c => c.Id, (l, c) => new
            {
                LinkId = l.Id,
                c.Name,
                c.Email,
                Status = l.Status.ToString(),
                l.CreatedAt,
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(solicitacoes);
    }

    /// <summary>Aprova uma solicitação de acesso de contador a esta loja.</summary>
    /// <param name="linkId">Id do vínculo contador↔loja.</param>
    // ── POST /api/fiscal/contador/solicitacoes/{linkId}/aprovar ───────────────
    [HttpPost("contador/solicitacoes/{linkId:guid}/aprovar")]
    public async Task<IActionResult> AprovarSolicitacaoContador(Guid linkId)
    {
        var link = await _catalog.ContadorTenantLinks
            .FirstOrDefaultAsync(l => l.Id == linkId && l.TenantId == _tenant.TenantId);
        if (link is null) return NotFound();

        link.Status = ContadorLinkStatus.Approved;
        await _catalog.SaveChangesAsync();

        return Ok(new { Message = "Solicitação aprovada." });
    }

    /// <summary>Recusa uma solicitação de acesso de contador — apaga o vínculo por completo
    /// (não guarda status "Rejected"), assim uma nova solicitação futura do mesmo contador
    /// não fica bloqueada por um pedido antigo recusado.</summary>
    /// <param name="linkId">Id do vínculo contador↔loja.</param>
    // ── POST /api/fiscal/contador/solicitacoes/{linkId}/recusar ───────────────
    // Apaga o vínculo (não guarda um status "Rejected") — assim, se o contador
    // solicitar de novo mais tarde, o "jaExiste" de SolicitarAcesso não bloqueia
    // pra sempre; um pedido recusado simplesmente deixa de existir.
    [HttpPost("contador/solicitacoes/{linkId:guid}/recusar")]
    public async Task<IActionResult> RecusarSolicitacaoContador(Guid linkId)
    {
        var link = await _catalog.ContadorTenantLinks
            .FirstOrDefaultAsync(l => l.Id == linkId && l.TenantId == _tenant.TenantId);
        if (link is null) return NotFound();

        _catalog.ContadorTenantLinks.Remove(link);
        await _catalog.SaveChangesAsync();

        return Ok(new { Message = "Solicitação recusada." });
    }

    /// <summary>Lista o mural de avisos trocados com o(s) contador(es) vinculado(s) —
    /// traz avisos de TODOS os vínculos aprovados desta loja (pode haver mais de um
    /// contador vinculado, ex: troca de escritório em andamento).</summary>
    // ── GET /api/fiscal/contador/avisos ───────────────────────────────────────
    // Traz os avisos de TODOS os vínculos aprovados desta loja (pode haver mais
    // de um contador vinculado, ex: troca de escritório em andamento).
    [HttpGet("contador/avisos")]
    public async Task<IActionResult> ListAvisosContador()
    {
        var linkIds = await _catalog.ContadorTenantLinks
            .Where(l => l.TenantId == _tenant.TenantId && l.Status == ContadorLinkStatus.Approved)
            .Select(l => l.Id)
            .ToListAsync();

        var avisos = await _catalog.ContadorAvisos
            .Where(a => linkIds.Contains(a.ContadorTenantLinkId))
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, a.Autor, a.Mensagem, a.CreatedAt })
            .ToListAsync();

        return Ok(avisos);
    }

    /// <summary>Posta um aviso do lojista pro contador no mural compartilhado. Se houver
    /// mais de um contador vinculado, exige informar qual (LinkId) — o lojista só pode
    /// escrever em vínculos da própria loja.</summary>
    /// <param name="request">Mensagem e, se houver múltiplos contadores vinculados, o LinkId de destino.</param>
    // ── POST /api/fiscal/contador/avisos ──────────────────────────────────────
    [HttpPost("contador/avisos")]
    public async Task<IActionResult> PostAvisoContador([FromBody] AvisoContadorRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var linksAprovados = await _catalog.ContadorTenantLinks
            .Where(l => l.TenantId == _tenant.TenantId && l.Status == ContadorLinkStatus.Approved)
            .ToListAsync();

        if (linksAprovados.Count == 0)
            return NotFound(new { Message = "Nenhum contador vinculado a esta loja." });

        ContadorTenantLink link;
        if (linksAprovados.Count == 1)
        {
            link = linksAprovados[0];
        }
        else
        {
            if (request.LinkId is null)
                return BadRequest(new { Message = "Há mais de um contador vinculado — informe qual (linkId)." });

            // Filtra pelos vínculos JÁ carregados (todos garantidamente desta loja),
            // em vez de buscar o linkId direto no banco — impede que um lojista
            // escreva num vínculo de outra loja adivinhando o Guid.
            var encontrado = linksAprovados.FirstOrDefault(l => l.Id == request.LinkId.Value);
            if (encontrado is null)
                return NotFound(new { Message = "Vínculo não encontrado para esta loja." });
            link = encontrado;
        }

        _catalog.ContadorAvisos.Add(new ContadorAviso
        {
            ContadorTenantLinkId = link.Id,
            Autor                = "Lojista",
            Mensagem             = request.Mensagem.Trim(),
        });
        await _catalog.SaveChangesAsync();

        return Ok(new { Message = "Aviso enviado." });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class CancelarNotaRequest
{
    [Required, MinLength(15)]
    public string Justificativa { get; init; } = "";
}

public class InutilizarFaixaRequest
{
    public int Ano { get; init; }
    public int Serie { get; init; }
    public int NumeroInicial { get; init; }
    public int NumeroFinal { get; init; }

    [Required, MinLength(15), MaxLength(255)]
    public string Justificativa { get; init; } = "";
}

public class SaveNaturezaRequest
{
    [Required, MaxLength(150)]
    public string Descricao { get; init; } = "";

    [Required, MaxLength(4)]
    public string Cfop { get; init; } = "";

    [MaxLength(3)]
    public string? Csosn { get; init; }

    /// <summary>% de crédito de ICMS (pCredSN) — só considerado quando Csosn = "101".</summary>
    [Range(0, 100)]
    public decimal? PercentualCreditoSn { get; init; }

    [Range(0, 8)]
    public int OrigemMercadoria { get; init; } = 0;

    [Range(0, 6)]
    public int? ModalidadeBcSt { get; init; }

    [Range(0, 1000)]
    public decimal? PercentualMvaSt { get; init; }

    [Range(0, 100)]
    public decimal? PercentualReducaoBcSt { get; init; }

    [Range(0, 100)]
    public decimal? AliquotaIcmsSt { get; init; }

    [Range(0, 100)]
    public decimal? AliquotaIcmsProprio { get; init; }

    [Range(0, 100)]
    public decimal? AliquotaFcpSt { get; init; }

    [Range(1, int.MaxValue)]
    public int? BaseStFixaEmCentavos { get; init; }

    [Required, RegularExpression("^[0-9]{3}$")]
    public string IbsCbsCst { get; init; } = "000";

    [Required, RegularExpression("^[0-9]{6}$")]
    public string IbsCbsClassTrib { get; init; } = "000001";

    public bool IsPadrao { get; init; }

    // ── Regime normal (Lucro Presumido/Real) ─────────────────────────────────
    // Convivem com os campos de CSOSN acima: a mesma natureza continua válida se
    // a empresa mudar de regime, e a emissão escolhe o par certo pelo CRT.

    /// <summary>CST do ICMS (00, 10, 20, 30, 40, 41, 50, 60, 70, 90).</summary>
    [MaxLength(2)]
    public string? Cst { get; init; }

    /// <summary>% de redução da base de cálculo da operação própria — CST 20 e 70.</summary>
    [Range(0, 100)]
    public decimal? PercentualReducaoBc { get; init; }

    /// <summary>Alíquota do FCP sobre a operação própria.</summary>
    [Range(0, 100)]
    public decimal? AliquotaFcp { get; init; }

    [Range(1, int.MaxValue)]
    public int? BaseStRetidaEmCentavos { get; init; }

    [Range(1, int.MaxValue)]
    public int? ValorStRetidoEmCentavos { get; init; }

    [MaxLength(2)]
    public string? CstPis { get; init; }

    [MaxLength(2)]
    public string? CstCofins { get; init; }

    [Range(0, 100)]
    public decimal? AliquotaPis { get; init; }

    [Range(0, 100)]
    public decimal? AliquotaCofins { get; init; }
}
