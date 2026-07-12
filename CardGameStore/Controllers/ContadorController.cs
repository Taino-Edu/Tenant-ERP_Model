// =============================================================================
// ContadorController.cs — Acesso fiscal read-only pro contador da loja.
//
// FiscalController inteiro é AdminOnly (config, certificado, naturezas de
// operação, emissão/cancelamento) — este controller separado existe pra dar
// ao Contador só o que ele precisa pra fechar impostos: listar notas, exportar
// XMLs, e ver os dados cadastrais da empresa. Nada de credencial (certificado,
// CSC token) nem ação administrativa. Mesmo espírito de MinhasNotasController,
// que já faz isso pro cliente.
//
// GET /api/contador/notas          → lista notas fiscais da loja (sem filtro de dono)
// GET /api/contador/exportar-xmls  → ZIP de XMLs autorizados/cancelados no período
// GET /api/contador/config         → dados cadastrais da empresa (sem certificado/CSC)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/contador")]
[Authorize(Policy = "ContadorOnly")]
[RequireModule("fiscal")]
[Produces("application/json")]
public class ContadorController : ControllerBase
{
    private readonly AppDbContext           _db;
    private readonly FiscalXmlExportService _export;

    public ContadorController(AppDbContext db, FiscalXmlExportService export)
    {
        _db     = db;
        _export = export;
    }

    // ── GET /api/contador/notas?inicio=&fim=&status=&page=&pageSize= ──────────
    [HttpGet("notas")]
    public async Task<IActionResult> ListNotas(
        [FromQuery] DateTime? inicio = null, [FromQuery] DateTime? fim = null,
        [FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var q = _db.NotasFiscaisEmitidas.AsQueryable();

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

    // ── GET /api/contador/exportar-xmls?inicio=&fim= ──────────────────────────
    [HttpGet("exportar-xmls")]
    public async Task<IActionResult> ExportarXmls([FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        if (fim <= inicio)
            return BadRequest(new { Message = "O período final deve ser depois do inicial." });

        var zipBytes = await _export.GerarZipAsync(inicio.ToUniversalTime(), fim.ToUniversalTime());
        var fileName = $"xmls-fiscais-{inicio:yyyy-MM-dd}-a-{fim:yyyy-MM-dd}.zip";

        return File(zipBytes, "application/zip", fileName);
    }

    // ── GET /api/contador/config ───────────────────────────────────────────────
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var cfg = await _db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId) ?? new FiscalConfig();

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
}
