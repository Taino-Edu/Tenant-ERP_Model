// =============================================================================
// FiscalController.cs — Configuração fiscal, certificado A1, naturezas de
// operação e exportação de XMLs de NFC-e pro contador.
// =============================================================================

using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.DTOs;
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
[Produces("application/json")]
public class FiscalController : ControllerBase
{
    private readonly AppDbContext              _db;
    private readonly EncryptionService         _enc;
    private readonly FiscalCertificadoService  _certificado;
    private readonly FiscalXmlExportService    _export;
    private readonly INfceEmissionService      _emissao;
    private readonly CatalogDbContext          _catalog;
    private readonly ITenantContext            _tenant;

    public FiscalController(
        AppDbContext db, EncryptionService enc, FiscalCertificadoService certificado,
        FiscalXmlExportService export, INfceEmissionService emissao,
        CatalogDbContext catalog, ITenantContext tenant)
    {
        _db          = db;
        _enc         = enc;
        _certificado = certificado;
        _export      = export;
        _emissao     = emissao;
        _catalog     = catalog;
        _tenant      = tenant;
    }

    /// <summary>Retorna a configuração fiscal da loja (CNPJ, endereço, regime tributário,
    /// certificado, CSC). Nunca inclui a senha do certificado nem o CSC token.</summary>
    // ── GET /api/fiscal/config ────────────────────────────────────────────────
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var cfg = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
        return Ok(ToDto(cfg));
    }

    /// <summary>Atualiza a configuração fiscal (update parcial — só os campos enviados
    /// são alterados). Cria a linha de configuração se ainda não existir.</summary>
    /// <param name="req">Campos a atualizar; qualquer campo null/omitido mantém o valor atual.</param>
    // ── PUT /api/fiscal/config ────────────────────────────────────────────────
    [HttpPut("config")]
    public async Task<IActionResult> SaveConfig([FromBody] SaveFiscalConfigRequest req)
    {
        var cfg = await GetOrCreateConfigAsync();

        if (req.Cnpj is not null)
            cfg.Cnpj = req.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "");
        if (req.RazaoSocial       is not null) cfg.RazaoSocial       = req.RazaoSocial;
        if (req.InscricaoEstadual is not null) cfg.InscricaoEstadual = req.InscricaoEstadual;
        if (req.EmailContador     is not null) cfg.EmailContador     = req.EmailContador;
        if (req.SerieNfce.HasValue)            cfg.SerieNfce         = req.SerieNfce.Value;

        if (req.Logradouro          is not null) cfg.Logradouro          = req.Logradouro;
        if (req.Numero              is not null) cfg.Numero              = req.Numero;
        if (req.Complemento         is not null) cfg.Complemento         = req.Complemento;
        if (req.Bairro              is not null) cfg.Bairro              = req.Bairro;
        if (req.CodigoMunicipioIbge is not null) cfg.CodigoMunicipioIbge = req.CodigoMunicipioIbge;
        if (req.Municipio           is not null) cfg.Municipio           = req.Municipio;
        if (req.Uf                  is not null) cfg.Uf                  = req.Uf.ToUpperInvariant();
        if (req.Cep                 is not null) cfg.Cep                 = req.Cep.Replace("-", "");
        if (req.CscId               is not null) cfg.CscId               = req.CscId;
        // M14: criptografado com o mesmo EncryptionService do certificado — em claro, um
        // vazamento do banco permitiria gerar QR Codes válidos em nome da loja.
        if (req.CscToken            is not null) cfg.CscTokenEncrypted   = _enc.Encrypt(req.CscToken);

        if (req.RegimeTributario is not null)
        {
            if (!Enum.TryParse<RegimeTributario>(req.RegimeTributario, out var regime))
                return BadRequest(new { Message = $"Regime tributário \"{req.RegimeTributario}\" inválido." });

            // F10: a montagem de itens (NfceEmissionService.MontarIcmsSimplesNacional) só sabe
            // gerar classes ICMSSN* (CSOSN do Simples Nacional) — Lucro Presumido/Real exigiria
            // CST de ICMS normal (regime não-cumulativo), que este sistema não calcula. Permitir
            // a escolha aqui geraria CRT×CSOSN inconsistente no XML: 100% de rejeição da SEFAZ.
            if (regime != RegimeTributario.SimplesNacional)
                return BadRequest(new
                {
                    Message = "Este sistema só emite NFC-e pra empresas no Simples Nacional — a montagem " +
                               "de impostos usa CSOSN, incompatível com Lucro Presumido/Real (exigiria CST " +
                               "de ICMS normal, não implementado). Consulte o contador antes de mudar de regime.",
                });

            cfg.RegimeTributario = regime;
        }

        if (req.Ambiente is not null &&
            Enum.TryParse<AmbienteFiscal>(req.Ambiente, out var ambiente))
            cfg.Ambiente = ambiente;

        if (req.FormasPagamentoAutoEmissao is not null)
        {
            var invalidas = req.FormasPagamentoAutoEmissao.Where(f => !PaymentMethod.IsValid(f)).ToList();
            if (invalidas.Count > 0)
                return BadRequest(new { Message = $"Forma(s) de pagamento inválida(s): {string.Join(", ", invalidas)}." });

            cfg.FormasPagamentoAutoEmissao = string.Join(",", req.FormasPagamentoAutoEmissao.Distinct());
        }

        cfg.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ToDto(cfg));
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
        if (string.IsNullOrWhiteSpace(senha))
            return BadRequest(new { Message = "Informe a senha do certificado." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var pfxBytes = ms.ToArray();

        CertificadoInfo info;
        try
        {
            info = _certificado.Validar(pfxBytes, senha);
        }
        catch (CertificadoInvalidoException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }

        var cfg = await GetOrCreateConfigAsync();

        cfg.CertificadoPfxEncrypted        = _enc.Encrypt(Convert.ToBase64String(pfxBytes));
        cfg.CertificadoSenhaEncrypted      = _enc.Encrypt(senha);
        cfg.CertificadoValidade            = info.NotAfter;
        cfg.CertificadoUploadedAt          = DateTime.UtcNow;
        cfg.CertificadoUltimoAlertaLimiar  = null; // reseta o ciclo de alertas pro novo certificado
        cfg.UpdatedAt                      = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Message   = "Certificado validado e salvo com sucesso.",
            Validade  = info.NotAfter,
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

    /// <summary>CSOSN que o motor de emissão sabe montar sozinho — ver
    /// NfceEmissionService.MontarIcmsSimplesNacional. 201/202/203 exigem ICMS-ST como
    /// substituto (MVA, base reduzida) que ninguém aqui calcula, por isso ficam de fora.</summary>
    private static readonly string[] CsosnSuportados = { "101", "102", "103", "300", "400", "500", "900" };

    private BadRequestObjectResult? ValidarCsosn(string? csosn)
    {
        if (string.IsNullOrWhiteSpace(csosn)) return null;
        if (!CsosnSuportados.Contains(csosn))
            return BadRequest(new
            {
                Message = csosn is "201" or "202" or "203"
                    ? $"CSOSN {csosn} exige ICMS-ST como substituto tributário (MVA, base reduzida) — este sistema não calcula isso sozinho. Consulte o contador ou use um CSOSN sem ICMS-ST."
                    : $"CSOSN \"{csosn}\" não é suportado. Use um destes: {string.Join(", ", CsosnSuportados)}."
            });
        return null;
    }

    /// <summary>Cria uma natureza de operação (CFOP/CSOSN). Marcar como padrão desmarca
    /// atomicamente a natureza padrão anterior (só pode haver uma).</summary>
    /// <param name="req">CFOP, CSOSN (só os suportados pelo motor de emissão) e se é a padrão.</param>
    // ── POST /api/fiscal/naturezas-operacao ───────────────────────────────────
    [HttpPost("naturezas-operacao")]
    public async Task<IActionResult> CreateNatureza([FromBody] SaveNaturezaRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (ValidarCsosn(req.Csosn) is BadRequestObjectResult erro) return erro;

        var natureza = new NaturezaOperacao
        {
            Descricao = req.Descricao,
            Cfop      = req.Cfop,
            Csosn     = req.Csosn,
            PercentualCreditoIcmsSn = req.Csosn == "101" ? req.PercentualCreditoSn : null,
            IsPadrao  = req.IsPadrao,
        };

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
        if (ValidarCsosn(req.Csosn) is BadRequestObjectResult erro) return erro;

        var natureza = await _db.NaturezasOperacao.FindAsync(id);
        if (natureza is null) return NotFound();

        natureza.Descricao = req.Descricao;
        natureza.Cfop      = req.Cfop;
        natureza.Csosn     = req.Csosn;
        natureza.PercentualCreditoIcmsSn = req.Csosn == "101" ? req.PercentualCreditoSn : null;
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
                n.TentativasReprocessamento,
                n.CreatedAt,
            })
            .ToListAsync();

        // Visibilidade pro admin: quantas notas estão paradas esperando o retry automático
        // (pendentes de verdade + em contingência aguardando retransmissão) e há quanto
        // tempo a mais antiga está parada — sinal de que algo precisa de atenção.
        var pendentesQuery = _db.NotasFiscaisEmitidas.Where(n =>
            n.Status == NotaFiscalStatus.PendenteEmissao || n.Status == NotaFiscalStatus.AutorizadaContingencia);
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
            return Ok(new { nota.Id, Status = nota.Status.ToString() });
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

    /// <summary>Gera um .zip com os XMLs de todas as NFC-e emitidas no período, pra
    /// entregar ao contador.</summary>
    /// <param name="inicio">Data inicial do período (inclusive).</param>
    /// <param name="fim">Data final do período — deve ser depois de <paramref name="inicio"/>.</param>
    // ── GET /api/fiscal/exportar-xmls?inicio=&fim= ────────────────────────────
    [HttpGet("exportar-xmls")]
    public async Task<IActionResult> ExportarXmls([FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        if (fim <= inicio)
            return BadRequest(new { Message = "O período final deve ser depois do inicial." });

        // F11: inicio/fim chegam do query string com Kind=Unspecified — .ToUniversalTime()
        // assumiria o fuso do SERVIDOR (UTC em container), não o de Brasília, deslocando a
        // janela em 3h (notas entre 21h-24h de Brasília caindo no ZIP do dia/mês errado). O job
        // automático (FiscalXmlExportBackgroundService) já fazia essa conversão certa.
        var zipBytes = await _export.GerarZipAsync(BrazilTime.ToUtcFromLocal(inicio), BrazilTime.ToUtcFromLocal(fim));
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

            return Ok(new { Message = "Convite registrado — quando esse e-mail se cadastrar no portal do contador (/contador/cadastro), o acesso a esta loja é liberado automaticamente." });
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

        return Ok(new { Message = $"Contador {conta.Name} vinculado com sucesso." });
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

    /// <summary>
    /// Busca a linha única de configuração fiscal pelo ID fixo, criando-a se necessário.
    /// Como o ID é fixo, uma segunda inserção concorrente vira uma violação de PK —
    /// nesse caso, descarta a tentativa local e relê a linha que a outra requisição criou.
    /// </summary>
    private async Task<FiscalConfig> GetOrCreateConfigAsync()
    {
        var cfg = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
        if (cfg is not null) return cfg;

        cfg = new FiscalConfig();
        _db.FiscalConfigs.Add(cfg);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(cfg).State = EntityState.Detached;
            cfg = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId)
                ?? throw new InvalidOperationException("Falha ao obter configuração fiscal após conflito de concorrência.");
        }

        return cfg;
    }

    private static object ToDto(FiscalConfig? cfg)
    {
        cfg ??= new FiscalConfig();

        int? diasParaVencer = cfg.CertificadoValidade.HasValue
            ? (int)(cfg.CertificadoValidade.Value.Date - DateTime.UtcNow.Date).TotalDays
            : null;

        return new
        {
            cfg.Cnpj,
            cfg.RazaoSocial,
            cfg.InscricaoEstadual,
            cfg.Logradouro,
            cfg.Numero,
            cfg.Complemento,
            cfg.Bairro,
            cfg.CodigoMunicipioIbge,
            cfg.Municipio,
            cfg.Uf,
            cfg.Cep,
            CscConfigurado = !string.IsNullOrWhiteSpace(cfg.CscId) && !string.IsNullOrWhiteSpace(cfg.CscTokenEncrypted),
            cfg.CscId, // não sensível isoladamente; o token nunca é retornado
            RegimeTributario = cfg.RegimeTributario.ToString(),
            Ambiente         = cfg.Ambiente.ToString(),
            cfg.SerieNfce,
            cfg.ProximoNumeroNfce,
            cfg.EmailContador,
            cfg.CertificadoConfigurado,
            cfg.CertificadoValidade,
            DiasParaVencer = diasParaVencer,
            FormasPagamentoAutoEmissao = string.IsNullOrWhiteSpace(cfg.FormasPagamentoAutoEmissao)
                ? Array.Empty<string>()
                : cfg.FormasPagamentoAutoEmissao.Split(',', StringSplitOptions.RemoveEmptyEntries),
        };
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class SaveFiscalConfigRequest
{
    public string? Cnpj              { get; init; }
    public string? RazaoSocial       { get; init; }
    public string? InscricaoEstadual { get; init; }
    public string? Logradouro          { get; init; }
    public string? Numero              { get; init; }
    public string? Complemento         { get; init; }
    public string? Bairro              { get; init; }
    public string? CodigoMunicipioIbge { get; init; }
    public string? Municipio           { get; init; }
    public string? Uf                  { get; init; }
    public string? Cep                 { get; init; }
    public string? CscId               { get; init; }
    public string? CscToken            { get; init; }
    public string? RegimeTributario  { get; init; }
    public string? Ambiente          { get; init; }
    public int?    SerieNfce         { get; init; }
    public string? EmailContador     { get; init; }

    /// <summary>Formas de pagamento que emitem NFC-e automaticamente ao fechar a venda, sem perguntar. Null = não altera.</summary>
    public string[]? FormasPagamentoAutoEmissao { get; init; }
}

public class CancelarNotaRequest
{
    [Required, MinLength(15)]
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

    public bool IsPadrao { get; init; }
}
