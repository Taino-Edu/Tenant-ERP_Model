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

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
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

    // ── GET /api/contador-portal/clientes ─────────────────────────────────────
    [HttpGet("clientes")]
    public async Task<IActionResult> ListClientes()
    {
        var contadorId = GetContadorId();

        var clientes = await _catalog.ContadorTenantLinks
            .Where(l => l.ContadorAccountId == contadorId)
            .Join(_catalog.Tenants, l => l.TenantId, t => t.Id, (l, t) => new
            {
                TenantId = t.Id,
                t.Slug,
                Status = l.Status.ToString(),
            })
            .ToListAsync();

        return Ok(clientes);
    }

    // ── POST /api/contador-portal/solicitar-acesso ────────────────────────────
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

    // ── GET /api/contador-portal/clientes/{tenantId}/notas ────────────────────
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

        var q = db.NotasFiscaisEmitidas.AsQueryable();
        if (inicio.HasValue) q = q.Where(n => n.CreatedAt >= inicio.Value.ToUniversalTime());
        if (fim.HasValue)    q = q.Where(n => n.CreatedAt <= fim.Value.ToUniversalTime());
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

    // ── GET /api/contador-portal/clientes/{tenantId}/config ───────────────────
    [HttpGet("clientes/{tenantId:guid}/config")]
    public async Task<IActionResult> GetConfig(Guid tenantId)
    {
        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

        var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cfg = await db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId) ?? new FiscalConfig();

        return Ok(new
        {
            cfg.Cnpj,
            cfg.RazaoSocial,
            cfg.InscricaoEstadual,
            cfg.Logradouro,
            cfg.Numero,
            cfg.Complemento,
            cfg.Bairro,
            cfg.Municipio,
            cfg.Uf,
            cfg.Cep,
            RegimeTributario = cfg.RegimeTributario.ToString(),
        });
    }

    // ── GET /api/contador-portal/clientes/{tenantId}/exportar-xmls ────────────
    [HttpGet("clientes/{tenantId:guid}/exportar-xmls")]
    public async Task<IActionResult> ExportarXmls(Guid tenantId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        if (fim <= inicio)
            return BadRequest(new { Message = "O período final deve ser depois do inicial." });

        var tenant = await AutorizarEObterTenantAsync(tenantId);
        if (tenant is null) return Forbid();

        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

        var export = scope.ServiceProvider.GetRequiredService<FiscalXmlExportService>();
        var zipBytes = await export.GerarZipAsync(inicio.ToUniversalTime(), fim.ToUniversalTime());
        var fileName = $"xmls-fiscais-{inicio:yyyy-MM-dd}-a-{fim:yyyy-MM-dd}.zip";

        return File(zipBytes, "application/zip", fileName);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

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
        if (tenant is null || !tenant.EnabledModules.Contains("fiscal")) return null;

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
