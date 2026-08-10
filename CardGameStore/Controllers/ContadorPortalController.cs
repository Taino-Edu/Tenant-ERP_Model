// =============================================================================
// ContadorPortalController.cs — Portal cross-tenant do Contador.
//
// A conta do Contador vive só no catálogo (CatalogDbContext, schema "public") e
// pode estar vinculada a vários tenants (clientes). Este controller resolve,
// pra cada requisição de dados de UM cliente específico, qual tenant é esse —
// e SÓ libera dados depois de confirmar um ContadorTenantLink com Status
// Approved entre este contador e aquele tenant (AutorizarEObterTenantAsync).
// Sem essa checagem, bastaria adivinhar um tenantId pra ler a fiscal de
// qualquer loja — é o ponto mais sensível de todo o recurso.
//
// Reaproveita o mesmo padrão do TenantProvisioningService pra trocar o schema
// no meio da requisição: abre um novo DI scope, seta o ITenantContext daquele
// scope pro tenant já autorizado, e resolve o AppDbContext (com o
// TenantConnectionInterceptor) fresco a partir do mesmo scope.
//
// GET  /api/contador-portal/clientes                        → lojas vinculadas (Approved/Pending)
// POST /api/contador-portal/solicitar-acesso                 → pede acesso a mais uma loja, por slug
// GET  /api/contador-portal/clientes/{tenantId}/notas        → notas fiscais da loja
// GET  /api/contador-portal/clientes/{tenantId}/config       → dados cadastrais (sem certificado/CSC)
// GET  /api/contador-portal/clientes/{tenantId}/exportar-xmls → ZIP de XMLs no período
// =============================================================================

using System.Text.Json;
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

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/contador-portal")]
[Authorize(Policy = "ContadorOnly")]
[Produces("application/json")]
public class ContadorPortalController : ControllerBase
{
    private readonly CatalogDbContext              _catalog;
    private readonly IServiceScopeFactory          _scopeFactory;
    private readonly ILogger<ContadorPortalController> _logger;

    public ContadorPortalController(
        CatalogDbContext catalog, IServiceScopeFactory scopeFactory, ILogger<ContadorPortalController> logger)
    {
        _catalog      = catalog;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>
    /// Lista as lojas vinculadas a este contador (aprovadas ou pendentes). Pra
    /// vínculos aprovados, também traz um sinal rápido de saúde fiscal: validade
    /// do certificado A1 e data da última nota emitida.
    /// </summary>
    [HttpGet("clientes")]
    public async Task<IActionResult> ListClientes()
    {
        var contadorId = GetContadorId();

        var links = await _catalog.ContadorTenantLinks
            .Where(l => l.ContadorAccountId == contadorId)
            .Join(_catalog.Tenants, l => l.TenantId, t => t.Id, (l, t) => new { t.Id, t.Slug, t.SchemaName, t.EnabledModules, l.Status })
            .ToListAsync();

        var clientes = new List<object>();
        foreach (var link in links)
        {
            DateTime? certificadoValidade = null;
            DateTime? ultimaNotaEm       = null;

            // Só vale a pena abrir o schema do tenant se o vínculo já está
            // aprovado — Pending não tem acesso a dado nenhum mesmo.
            if (link.Status == ContadorLinkStatus.Approved)
            {
                using var scope = _scopeFactory.CreateScope();
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.Set(link.Id, link.SchemaName, link.EnabledModules);

                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cfg = await db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
                certificadoValidade = cfg?.CertificadoValidade;

                ultimaNotaEm = await db.NotasFiscaisEmitidas
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => (DateTime?)n.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            clientes.Add(new
            {
                TenantId = link.Id,
                link.Slug,
                Status = link.Status.ToString(),
                CertificadoValidade = certificadoValidade,
                UltimaNotaEm        = ultimaNotaEm,
            });
        }

        return Ok(clientes);
    }

    /// <summary>
    /// Solicita acesso a uma loja pelo slug — cria um vínculo Pending que só vira
    /// utilizável depois que o lojista aprovar em /admin/fiscal. 404 se o slug não
    /// existe, 409 se já existe solicitação (ou vínculo) pra essa loja.
    /// </summary>
    [HttpPost("solicitar-acesso")]
    public async Task<IActionResult> SolicitarAcesso([FromBody] SolicitarAcessoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var contadorId = GetContadorId();
        var slug = request.TenantSlug.Trim().ToLowerInvariant();

        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Slug == slug);
        if (tenant is null)
            return NotFound(new { Message = "Loja não encontrada. Confira o código/slug com o lojista." });

        var jaExiste = await _catalog.ContadorTenantLinks
            .AnyAsync(l => l.ContadorAccountId == contadorId && l.TenantId == tenant.Id);
        if (jaExiste)
            return Conflict(new { Message = "Você já solicitou (ou já tem) acesso a esta loja." });

        _catalog.ContadorTenantLinks.Add(new ContadorTenantLink
        {
            ContadorAccountId = contadorId,
            TenantId          = tenant.Id,
            Status            = ContadorLinkStatus.Pending,
        });
        await _catalog.SaveChangesAsync();

        _logger.LogInformation("Contador {ContadorId} solicitou acesso à loja '{Slug}'", contadorId, slug);
        return Ok(new { Message = "Solicitação enviada. Aguarde a aprovação do lojista." });
    }

    /// <summary>
    /// Lista as notas fiscais emitidas por uma loja vinculada, com paginação e
    /// filtros. 403 se este contador não tem vínculo aprovado com essa loja.
    /// </summary>
    /// <param name="tenantId">Id da loja (precisa ter vínculo Approved com este contador).</param>
    /// <param name="inicio">Filtra notas emitidas a partir desta data.</param>
    /// <param name="fim">Filtra notas emitidas até esta data.</param>
    /// <param name="status">Filtra por status da nota (ex: "Autorizada", "Cancelada").</param>
    /// <param name="page">Número da página (base 1, padrão 1).</param>
    /// <param name="pageSize">Registros por página (padrão 30).</param>
    [HttpGet("clientes/{tenantId:guid}/notas")]
    public async Task<IActionResult> ListNotas(
        Guid tenantId,
        [FromQuery] DateTime? inicio = null, [FromQuery] DateTime? fim = null,
        [FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // F11: inicio/fim vêm do query string com Kind=Unspecified — .ToUniversalTime()
        // assumiria o fuso do SERVIDOR (UTC em container), não o de Brasília.
        var q = db.NotasFiscaisEmitidas.AsQueryable();
        if (inicio.HasValue) q = q.Where(n => n.CreatedAt >= BrazilTime.ToUtcFromLocal(inicio.Value));
        if (fim.HasValue)    q = q.Where(n => n.CreatedAt <= BrazilTime.ToUtcFromLocal(fim.Value));
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<NotaFiscalStatus>(status, out var statusEnum))
            q = q.Where(n => n.Status == statusEnum);

        var total = await q.CountAsync();
        var itens = await q.OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new
            {
                n.Id,
                Origem = n.Origem.ToString(),
                Status = n.Status.ToString(),
                n.ValorTotalEmCentavos,
                n.Serie,
                n.Numero,
                n.ChaveAcesso,
                n.EmitidoEm,
                n.CanceladoEm,
                n.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { items = itens, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    /// <summary>NF-e de entrada encontradas para o CNPJ, incluindo a situação do recebimento físico.</summary>
    [HttpGet("clientes/{tenantId:guid}/notas-recebidas")]
    public async Task<IActionResult> ListNotasRecebidas(
        Guid tenantId, [FromQuery] DateTime? inicio = null, [FromQuery] DateTime? fim = null)
    {
        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>()
            .Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var q = db.NotasDestinadas.AsNoTracking();
        if (inicio.HasValue)
        {
            var iniUtc = BrazilTime.DateToUtcStart(inicio.Value);
            q = q.Where(n => (n.DataEmissao ?? n.CreatedAt) >= iniUtc);
        }
        if (fim.HasValue)
        {
            var fimUtc = BrazilTime.DateToUtcStart(fim.Value.Date.AddDays(1));
            q = q.Where(n => (n.DataEmissao ?? n.CreatedAt) < fimUtc);
        }

        var items = await q.OrderByDescending(n => n.DataEmissao ?? n.CreatedAt)
            .Take(300)
            .Select(n => new
            {
                n.Id, n.ChaveAcesso, n.EmitenteCnpj, n.EmitenteNome, n.Valor,
                n.DataEmissao, n.Status, n.Situacao, n.ContasGeradas,
                n.EstoqueRecebidoEm, n.ItensEstoqueRecebidos, n.Erro,
            })
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>DRE gerencial por competência, usando o mesmo cálculo exibido ao lojista.</summary>
    [HttpGet("clientes/{tenantId:guid}/dre")]
    public async Task<IActionResult> GetDre(
        Guid tenantId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        if (fim.Date < inicio.Date)
            return BadRequest(new { Message = "O período final não pode ser anterior ao inicial." });
        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>()
            .Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);
        var financeiro = scope.ServiceProvider.GetRequiredService<IFinanceiroCalculoService>();
        var inicioBr = inicio.Date;
        var fimBr = fim.Date;
        var dto = await financeiro.CalcularAsync(
            BrazilTime.DateToUtcStart(inicioBr),
            BrazilTime.DateToUtcStart(fimBr.AddDays(1)),
            inicioBr, fimBr);
        return Ok(dto);
    }

    /// <summary>
    /// Configuração fiscal completa da loja vinculada — os mesmos campos que o
    /// lojista vê em /admin/fiscal, com os mesmos segredos omitidos (senha do
    /// certificado, CSC token e token IBPT nunca saem daqui). 403 se não vinculado.
    /// </summary>
    [HttpGet("clientes/{tenantId:guid}/config")]
    public async Task<IActionResult> GetConfig(Guid tenantId)
    {
        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = CriarEscopoDoTenant(tenant);
        var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cfg = await db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId) ?? new FiscalConfig();

        return Ok(FiscalConfigService.ToDto(cfg));
    }

    /// <summary>
    /// Altera a configuração fiscal da loja vinculada. Passa exatamente pelas
    /// mesmas validações do lojista (FiscalConfigService) — inclusive o bloqueio
    /// de regime diferente do Simples Nacional, que a emissão de NFC-e não sabe
    /// montar, e a guarda de titularidade do certificado ao ligar Produção.
    /// </summary>
    [HttpPut("clientes/{tenantId:guid}/config")]
    public async Task<IActionResult> SaveConfig(Guid tenantId, [FromBody] SaveFiscalConfigRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = CriarEscopoDoTenant(tenant);
        var configService = scope.ServiceProvider.GetRequiredService<FiscalConfigService>();

        var resultado = await configService.SalvarAsync(request);
        if (!resultado.Ok) return BadRequest(new { Message = resultado.Erro });

        _logger.LogInformation("Contador {ContadorId} alterou a configuração fiscal do tenant {TenantId}",
            GetContadorId(), tenantId);

        return Ok(FiscalConfigService.ToDto(resultado.Config));
    }

    /// <summary>
    /// Envia o certificado digital A1 da loja vinculada. Mesmas recusas do
    /// lojista: .pfx inválido, senha errada, ou certificado de CNPJ diferente do
    /// emitente — assinar NFC-e com certificado de terceiro é uso indevido.
    /// </summary>
    [HttpPost("clientes/{tenantId:guid}/certificado")]
    [RequestSizeLimit(2 * 1024 * 1024)] // 2 MB — certificados .pfx são pequenos
    public async Task<IActionResult> UploadCertificado(Guid tenantId, IFormFile file, [FromForm] string senha)
    {
        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        if (file is null || file.Length == 0)
            return BadRequest(new { Message = "Arquivo de certificado (.pfx) inválido ou vazio." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        using var scope = CriarEscopoDoTenant(tenant);
        var configService = scope.ServiceProvider.GetRequiredService<FiscalConfigService>();

        var (erro, info) = await configService.SalvarCertificadoAsync(ms.ToArray(), senha);
        if (erro is not null) return BadRequest(new { Message = erro });

        _logger.LogInformation("Contador {ContadorId} substituiu o certificado A1 do tenant {TenantId}",
            GetContadorId(), tenantId);

        return Ok(new
        {
            Message       = "Certificado validado e salvo com sucesso.",
            Validade      = info!.NotAfter,
            DiasRestantes = (int)(info.NotAfter.Date - DateTime.UtcNow.Date).TotalDays,
        });
    }

    /// <summary>
    /// Comparativo de carga tributária do período no Simples Nacional e no Lucro
    /// Presumido. Estimativa de apuração — ver ApuracaoTributariaService.
    /// </summary>
    [HttpGet("clientes/{tenantId:guid}/apuracao")]
    public async Task<IActionResult> GetApuracao(
        Guid tenantId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        if (fim.Date < inicio.Date)
            return BadRequest(new { Message = "O período final não pode ser anterior ao inicial." });

        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = CriarEscopoDoTenant(tenant);
        var apuracao = scope.ServiceProvider.GetRequiredService<IApuracaoTributariaService>();

        return Ok(await apuracao.ApurarAsync(inicio.Date, fim.Date));
    }

    /// <summary>
    /// Conciliação entre vendas e documentos fiscais do cliente (CON-001). É a
    /// visão que mostra a venda fechada sem nota — a que não aparece em nenhum
    /// relatório que parta das notas emitidas.
    /// </summary>
    [HttpGet("clientes/{tenantId:guid}/conciliacao")]
    public async Task<IActionResult> GetConciliacao(
        Guid tenantId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        if (fim.Date < inicio.Date)
            return BadRequest(new { Message = "O período final não pode ser anterior ao inicial." });

        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = CriarEscopoDoTenant(tenant);
        var conciliacao = scope.ServiceProvider.GetRequiredService<IConciliacaoFiscalService>();

        return Ok(await conciliacao.ConciliarAsync(inicio.Date, fim.Date));
    }

    /// <summary>Fechamentos mensais já travados, do mais recente pro mais antigo.</summary>
    [HttpGet("clientes/{tenantId:guid}/fechamentos")]
    public async Task<IActionResult> ListFechamentos(Guid tenantId)
    {
        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = CriarEscopoDoTenant(tenant);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fechamentos = await db.FechamentosFiscaisMensais.AsNoTracking()
            .OrderByDescending(f => f.Ano).ThenByDescending(f => f.Mes)
            .Take(36)
            .ToListAsync();

        return Ok(fechamentos.Select(MapFechamento));
    }

    /// <summary>
    /// Fecha uma competência: calcula DRE, notas e apuração do mês e grava o
    /// snapshot. Uma competência só fecha uma vez — refazer exige reabrir antes
    /// (DELETE), pra que o número declarado ao Fisco não mude sem rastro.
    /// </summary>
    [HttpPost("clientes/{tenantId:guid}/fechamentos")]
    public async Task<IActionResult> FecharCompetencia(
        Guid tenantId, [FromBody] FecharCompetenciaRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        var (inicioBr, fimBr) = CompetenciaParaPeriodo(request.Ano, request.Mes);
        if (inicioBr > BrazilTime.NowBr().Date)
            return BadRequest(new { Message = "Não dá pra fechar uma competência que ainda não começou." });

        using var scope = CriarEscopoDoTenant(tenant);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jaFechada = await db.FechamentosFiscaisMensais
            .AnyAsync(f => f.Ano == request.Ano && f.Mes == request.Mes);
        if (jaFechada)
            return Conflict(new { Message = "Esta competência já está fechada. Reabra antes de fechar de novo." });

        var snapshot = await MontarSnapshotAsync(scope, inicioBr, fimBr);

        var contadorId = GetContadorId();
        var contador = await _catalog.ContadorAccounts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contadorId);

        var fechamento = new FechamentoFiscalMensal
        {
            Ano                    = request.Ano,
            Mes                    = request.Mes,
            PeriodoInicio          = BrazilTime.DateToUtcStart(inicioBr),
            PeriodoFim             = BrazilTime.DateToUtcStart(fimBr),
            ReceitaBruta           = snapshot.Dre.ReceitaBruta,
            Deducoes               = snapshot.Dre.Deducoes,
            ImpostosSobreVendas    = snapshot.Dre.ImpostosSobreVendas,
            ReceitaLiquida         = snapshot.Dre.ReceitaLiquidaDre,
            CustoMercadoriaVendida = snapshot.Dre.Custo,
            DespesasOperacionais   = snapshot.Dre.DespesasOperacionais,
            ResultadoOperacional   = snapshot.Dre.ResultadoOperacional,
            ResultadoLiquido       = snapshot.Dre.ResultadoLiquido,
            NotasAutorizadas       = snapshot.NotasAutorizadas,
            NotasCanceladas        = snapshot.NotasCanceladas,
            ValorNotasAutorizadas  = snapshot.ValorNotasAutorizadas,
            NotasEntrada           = snapshot.NotasEntrada,
            ValorNotasEntrada      = snapshot.ValorNotasEntrada,
            RegimeApurado          = snapshot.Apuracao.RegimeAtual,
            ImpostoApurado         = snapshot.Apuracao.RegimeAtual == "SimplesNacional"
                                        ? snapshot.Apuracao.Simples.ValorDas
                                        : snapshot.Apuracao.Presumido.Total,
            AliquotaEfetiva        = snapshot.Apuracao.RegimeAtual == "SimplesNacional"
                                        ? snapshot.Apuracao.Simples.AliquotaEfetiva
                                        : snapshot.Apuracao.Presumido.AliquotaEfetiva,
            PayloadJson            = JsonSerializer.Serialize(new { snapshot.Dre, snapshot.Apuracao, snapshot.Pendencias }),
            Observacao             = string.IsNullOrWhiteSpace(request.Observacao) ? null : request.Observacao.Trim(),
            FechadoPorContadorId   = contadorId,
            FechadoPorNome         = contador?.Name,
        };

        db.FechamentosFiscaisMensais.Add(fechamento);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Índice único em (ano, mês): duas requisições simultâneas de
            // fechamento não podem gerar dois snapshots da mesma competência.
            return Conflict(new { Message = "Esta competência já está fechada. Reabra antes de fechar de novo." });
        }

        _logger.LogInformation("Contador {ContadorId} fechou a competência {Mes}/{Ano} do tenant {TenantId}",
            contadorId, request.Mes, request.Ano, tenantId);

        return Ok(MapFechamento(fechamento));
    }

    /// <summary>Reabre uma competência fechada — apaga o snapshot pra que possa ser refeito.</summary>
    [HttpDelete("clientes/{tenantId:guid}/fechamentos/{fechamentoId:guid}")]
    public async Task<IActionResult> ReabrirCompetencia(Guid tenantId, Guid fechamentoId)
    {
        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = CriarEscopoDoTenant(tenant);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fechamento = await db.FechamentosFiscaisMensais.FindAsync(fechamentoId);
        if (fechamento is null) return NotFound(new { Message = "Fechamento não encontrado." });

        db.FechamentosFiscaisMensais.Remove(fechamento);
        await db.SaveChangesAsync();

        _logger.LogWarning("Contador {ContadorId} reabriu a competência {Mes}/{Ano} do tenant {TenantId}",
            GetContadorId(), fechamento.Mes, fechamento.Ano, tenantId);

        return Ok(new { Message = $"Competência {fechamento.Mes:00}/{fechamento.Ano} reaberta." });
    }

    /// <summary>
    /// Pacote de fechamento da competência: XMLs de saída e entrada com nome
    /// identificável mais os relatórios em CSV (DRE, notas, apuração).
    /// </summary>
    [HttpGet("clientes/{tenantId:guid}/pacote-mensal")]
    public async Task<IActionResult> BaixarPacoteMensal(Guid tenantId, [FromQuery] int ano, [FromQuery] int mes)
    {
        if (mes is < 1 or > 12) return BadRequest(new { Message = "Mês inválido." });

        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        var (inicioBr, fimBr) = CompetenciaParaPeriodo(ano, mes);

        using var scope = CriarEscopoDoTenant(tenant);
        var snapshot = await MontarSnapshotAsync(scope, inicioBr, fimBr);

        var export = scope.ServiceProvider.GetRequiredService<FiscalXmlExportService>();
        var zip = await export.GerarPacoteMensalAsync(
            BrazilTime.DateToUtcStart(inicioBr),
            BrazilTime.DateToUtcStart(fimBr.AddDays(1)),
            ContadorRelatorioCsv.Montar(tenant.Slug, ano, mes, snapshot));

        return File(zip, "application/zip", $"fechamento-{tenant.Slug}-{ano}-{mes:00}.zip");
    }

    /// <summary>Estoque da loja para conferência e manutenção da classificação fiscal.</summary>
    [HttpGet("clientes/{tenantId:guid}/produtos")]
    public async Task<IActionResult> ListProdutos(Guid tenantId)
    {
        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>()
            .Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var produtos = await db.Products.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.Category, p.Barcode, p.StockQuantity, p.IsActive,
                p.Ncm, p.Cest, p.PercentualTributosFederais,
                p.PercentualTributosEstaduais, p.PercentualTributosMunicipais,
                p.FonteTributos, p.TributosAtualizadosEm,
            })
            .ToListAsync();

        // Evidência do NCM: item conciliado + NF-e de fornecedor. Preferimos a
        // entrada cujo NCM ainda coincide com o cadastro; se não houver, mostramos
        // a mais recente como divergência, sem esconder a fonte documental.
        var produtoIds = produtos.Select(p => p.Id).ToList();
        var evidencias = await db.NfeReceiptItems.AsNoTracking()
            .Where(i => i.ProductId.HasValue && produtoIds.Contains(i.ProductId.Value) &&
                        !i.Ignored && i.SourceNcm != null)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                ProdutoId = i.ProductId!.Value,
                NcmNotaEntrada = i.SourceNcm!,
                i.ItemNumber,
                i.NotaDestinada.ChaveAcesso,
                i.NotaDestinada.EmitenteNome,
                DataNotaEntrada = i.NotaDestinada.DataEmissao ?? i.CreatedAt,
            })
            .ToListAsync();

        var retorno = produtos.Select(p =>
        {
            var origem = evidencias.FirstOrDefault(e =>
                             e.ProdutoId == p.Id && e.NcmNotaEntrada == p.Ncm)
                         ?? evidencias.FirstOrDefault(e => e.ProdutoId == p.Id);
            return new
            {
                p.Id, p.Name, p.Category, p.Barcode, p.StockQuantity, p.IsActive,
                p.Ncm, p.Cest, p.PercentualTributosFederais,
                p.PercentualTributosEstaduais, p.PercentualTributosMunicipais,
                p.FonteTributos, p.TributosAtualizadosEm,
                NcmNotaEntrada = origem?.NcmNotaEntrada,
                NcmOrigemChave = origem?.ChaveAcesso,
                NcmOrigemEmitente = origem?.EmitenteNome,
                NcmOrigemData = origem?.DataNotaEntrada,
                NcmOrigemItem = origem?.ItemNumber,
                NcmOrigemConfere = origem is not null && origem.NcmNotaEntrada == p.Ncm,
            };
        });
        return Ok(retorno);
    }

    /// <summary>Altera somente NCM, CEST e tributos; quantidade e dados comerciais ficam protegidos.</summary>
    [HttpPut("clientes/{tenantId:guid}/produtos/{produtoId:guid}/fiscal")]
    public async Task<IActionResult> UpdateProdutoFiscal(
        Guid tenantId, Guid produtoId, [FromBody] ContadorProdutoFiscalRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        static string? Digitos(string? valor) => string.IsNullOrWhiteSpace(valor)
            ? null : new string(valor.Where(char.IsDigit).ToArray());
        var ncm = Digitos(request.Ncm);
        var cest = Digitos(request.Cest);
        if (ncm is not null && ncm.Length != 8)
            return BadRequest(new { Message = "NCM deve conter 8 dígitos." });
        if (cest is not null && cest.Length != 7)
            return BadRequest(new { Message = "CEST deve conter 7 dígitos." });

        using var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>()
            .Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var produto = await db.Products.FindAsync(produtoId);
        if (produto is null) return NotFound(new { Message = "Produto não encontrado." });

        produto.Ncm = ncm;
        produto.Cest = cest;
        produto.PercentualTributosFederais = request.PercentualTributosFederais;
        produto.PercentualTributosEstaduais = request.PercentualTributosEstaduais;
        produto.PercentualTributosMunicipais = request.PercentualTributosMunicipais;
        produto.FonteTributos = string.IsNullOrWhiteSpace(request.FonteTributos) ? null : request.FonteTributos.Trim();
        produto.TributosPreenchidosAutomaticamente = false;
        produto.TributosAtualizadosEm = DateTime.UtcNow;
        produto.TributosVigenciaInicio = null;
        produto.TributosVigenciaFim = null;
        produto.IbptVersao = null;
        produto.IbptChave = null;
        produto.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        _logger.LogInformation("Contador {ContadorId} atualizou dados fiscais do produto {ProdutoId} no tenant {TenantId}",
            GetContadorId(), produtoId, tenantId);
        return Ok(new { Message = "Dados fiscais atualizados." });
    }

    /// <summary>Baixa um ZIP com os XMLs das notas fiscais emitidas no período.</summary>
    /// <param name="tenantId">Id da loja (precisa ter vínculo Approved com este contador).</param>
    /// <param name="inicio">Início do período.</param>
    /// <param name="fim">Fim do período (inclusive).</param>
    [HttpGet("clientes/{tenantId:guid}/exportar-xmls")]
    public async Task<IActionResult> ExportarXmls(Guid tenantId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        if (fim.Date < inicio.Date)
            return BadRequest(new { Message = "O período final não pode ser anterior ao inicial." });

        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

        var export = scope.ServiceProvider.GetRequiredService<FiscalXmlExportService>();
        var (inicioUtc, fimExclusivoUtc) = FiscalXmlExportService.NormalizarPeriodoInclusivo(inicio, fim);
        var zipBytes = await export.GerarZipAsync(inicioUtc, fimExclusivoUtc);
        var fileName = $"xmls-fiscais-{inicio:yyyy-MM-dd}-a-{fim:yyyy-MM-dd}.zip";

        return File(zipBytes, "application/zip", fileName);
    }

    /// <summary>Lista o mural de avisos trocados entre este contador e a loja vinculada.</summary>
    [HttpGet("clientes/{tenantId:guid}/avisos")]
    public async Task<IActionResult> ListAvisos(Guid tenantId)
    {
        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        var contadorId = GetContadorId();
        var link = await _catalog.ContadorTenantLinks.FirstOrDefaultAsync(l =>
            l.ContadorAccountId == contadorId && l.TenantId == tenantId && l.Status == ContadorLinkStatus.Approved);
        if (link is null) return Forbid();

        var avisos = await _catalog.ContadorAvisos
            .Where(a => a.ContadorTenantLinkId == link.Id)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, a.Autor, a.Mensagem, a.CreatedAt })
            .ToListAsync();

        return Ok(avisos);
    }

    /// <summary>Envia um aviso no mural compartilhado com a loja vinculada.</summary>
    [HttpPost("clientes/{tenantId:guid}/avisos")]
    public async Task<IActionResult> PostAviso(Guid tenantId, [FromBody] AvisoContadorRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        var contadorId = GetContadorId();
        var link = await _catalog.ContadorTenantLinks.FirstOrDefaultAsync(l =>
            l.ContadorAccountId == contadorId && l.TenantId == tenantId && l.Status == ContadorLinkStatus.Approved);
        if (link is null) return Forbid();

        _catalog.ContadorAvisos.Add(new ContadorAviso
        {
            ContadorTenantLinkId = link.Id,
            Autor                = "Contador",
            Mensagem             = request.Mensagem.Trim(),
        });
        await _catalog.SaveChangesAsync();

        return Ok(new { Message = "Aviso enviado." });
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Abre um DI scope já apontando pro schema do tenant autorizado. É o mesmo
    /// padrão do TenantProvisioningService: o AppDbContext (e tudo que depende
    /// dele) precisa ser resolvido DEPOIS do ITenantContext estar setado, senão
    /// o interceptor de conexão manda a query pro schema errado.
    /// </summary>
    private IServiceScope CriarEscopoDoTenant(Tenant tenant)
    {
        var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>()
            .Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);
        return scope;
    }

    /// <summary>Competência (ano/mês) → primeiro e último dia no calendário de Brasília.</summary>
    private static (DateTime InicioBr, DateTime FimBr) CompetenciaParaPeriodo(int ano, int mes)
    {
        var inicio = new DateTime(ano, mes, 1);
        return (inicio, inicio.AddMonths(1).AddDays(-1));
    }

    /// <summary>
    /// Junta num único objeto tudo que o fechamento do mês precisa: DRE,
    /// apuração dos dois regimes, contagem de notas e as pendências que o
    /// contador deveria resolver antes de considerar o mês fechado.
    /// </summary>
    private static async Task<FechamentoSnapshot> MontarSnapshotAsync(
        IServiceScope scope, DateTime inicioBr, DateTime fimBr)
    {
        var db         = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var financeiro = scope.ServiceProvider.GetRequiredService<IFinanceiroCalculoService>();
        var apuracaoService = scope.ServiceProvider.GetRequiredService<IApuracaoTributariaService>();

        var iniUtc = BrazilTime.DateToUtcStart(inicioBr);
        var fimUtc = BrazilTime.DateToUtcStart(fimBr.AddDays(1));

        var dre = await financeiro.CalcularAsync(iniUtc, fimUtc, inicioBr, fimBr);
        var apuracao = await apuracaoService.ApurarAsync(inicioBr, fimBr);

        var notas = await db.NotasFiscaisEmitidas.AsNoTracking()
            .Where(n => n.CreatedAt >= iniUtc && n.CreatedAt < fimUtc)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new NotaFechamentoDto
            {
                Data              = n.EmitidoEm ?? n.CreatedAt,
                Serie             = n.Serie,
                Numero            = n.Numero,
                ChaveAcesso       = n.ChaveAcesso,
                Origem            = n.Origem.ToString(),
                Status            = n.Status.ToString(),
                ValorEmCentavos   = n.ValorTotalEmCentavos,
            })
            .ToListAsync();

        var entradas = await db.NotasDestinadas.AsNoTracking()
            .Where(n => (n.DataEmissao ?? n.CreatedAt) >= iniUtc && (n.DataEmissao ?? n.CreatedAt) < fimUtc)
            .Select(n => new { n.Valor, n.EstoqueRecebidoEm, n.Status })
            .ToListAsync();

        var autorizadas = notas.Where(n => n.Status is "Autorizada" or "AutorizadaContingencia").ToList();
        var canceladas  = notas.Where(n => n.Status == "Cancelada").ToList();

        var produtosSemNcm = await db.Products.CountAsync(p => p.IsActive && string.IsNullOrEmpty(p.Ncm));
        var entradasSemConferencia = entradas.Count(n => n.EstoqueRecebidoEm == null && n.Status != "cancelada");

        var pendencias = new List<string>();
        if (produtosSemNcm > 0)
            pendencias.Add($"{produtosSemNcm} produto(s) ativo(s) sem NCM — a classificação fiscal fica incompleta.");
        if (entradasSemConferencia > 0)
            pendencias.Add($"{entradasSemConferencia} NF-e de entrada sem conferência física do estoque.");
        if (dre.LancamentosNaoClassificados > 0)
            pendencias.Add($"R$ {dre.LancamentosNaoClassificados:N2} em lançamentos sem classificação contábil, fora do resultado.");
        if (notas.Any(n => n.Status is "PendenteEmissao" or "Rejeitada"))
            pendencias.Add($"{notas.Count(n => n.Status is "PendenteEmissao" or "Rejeitada")} nota(s) pendente(s) ou rejeitada(s) na competência.");

        return new FechamentoSnapshot(
            dre, apuracao,
            autorizadas.Count, canceladas.Count,
            autorizadas.Sum(n => n.ValorEmCentavos) / 100m,
            entradas.Count, entradas.Sum(n => n.Valor),
            notas, pendencias);
    }

    private static object MapFechamento(FechamentoFiscalMensal f) => new
    {
        f.Id, f.Ano, f.Mes,
        Competencia = $"{f.Mes:00}/{f.Ano}",
        f.PeriodoInicio, f.PeriodoFim,
        f.ReceitaBruta, f.Deducoes, f.ImpostosSobreVendas, f.ReceitaLiquida,
        f.CustoMercadoriaVendida, f.DespesasOperacionais,
        f.ResultadoOperacional, f.ResultadoLiquido,
        f.NotasAutorizadas, f.NotasCanceladas, f.ValorNotasAutorizadas,
        f.NotasEntrada, f.ValorNotasEntrada,
        f.RegimeApurado, f.ImpostoApurado, f.AliquotaEfetiva,
        f.Observacao, f.FechadoPorNome, f.FechadoEm,
    };

    /// <summary>
    /// Único ponto de decisão sobre se este contador pode ver os dados fiscais
    /// de um tenant específico: exige um ContadorTenantLink Approved entre os
    /// dois, o tenant Active, e o módulo "fiscal" habilitado NAQUELE tenant (não
    /// no tenant ambiente resolvido por Host, que pra requisições no domínio raiz
    /// é sempre o tenant-zero — checar o ambiente aqui daria falso-positivo).
    /// </summary>
    private async Task<Tenant?> AutorizarEObterTenantAsync(Guid tenantId)
    {
        var contadorId = GetContadorId();

        var aprovado = await _catalog.ContadorTenantLinks.AnyAsync(l =>
            l.ContadorAccountId == contadorId &&
            l.TenantId == tenantId &&
            l.Status == ContadorLinkStatus.Approved);

        if (!aprovado) return null;

        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active);
        if (tenant is null || !tenant.EnabledModules.Contains("contador")) return null;

        return tenant;
    }

    private Guid GetContadorId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token inválido: identificador de contador ausente.");
        return id;
    }
}
