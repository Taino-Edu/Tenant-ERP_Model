// =============================================================================
// ExportController.cs — Exportação self-service dos dados do próprio tenant.
//
// Reduz o medo de lock-in na hora de fechar venda com lojista novo (ver
// BACKLOG "migração de dados"): o admin da loja consegue baixar os dados dele
// a qualquer momento, sem depender de pedir pra nós. Só exportação por
// enquanto — importação fica pra uma fase depois, com escopo próprio (formato
// de origem varia por sistema concorrente).
//
// GET /api/export/produtos   → CSV de todos os produtos (ativos e inativos)
// GET /api/export/clientes   → CSV de clientes (Role=Customer; nunca inclui
//                               hash de senha, refresh token ou token de reset)
// GET /api/export/crediario  → CSV dos crediários em aberto (saldo devedor)
// =============================================================================

using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/export")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public class ExportController : ControllerBase
{
    private readonly AppDbContext   _db;
    private readonly IAuditService  _audit;

    public ExportController(AppDbContext db, IAuditService audit)
    {
        _db    = db;
        _audit = audit;
    }

    /// <summary>Exporta todos os produtos do catálogo (ativos e inativos).</summary>
    /// <param name="formato">"csv" (padrão) ou "xlsx".</param>
    [HttpGet("produtos")]
    public async Task<IActionResult> ExportProdutos([FromQuery] string formato = "csv")
    {
        var produtos = await _db.Products.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();

        var headers = new[]
        {
            "Id", "Nome", "Categoria", "Descricao", "CodigoBarras", "NCM", "CEST",
            "TributosFederaisPercentual", "TributosEstaduaisPercentual", "TributosMunicipaisPercentual", "FonteTributos",
            "TributosAutomaticos", "IbptVersao", "IbptChave", "TributosVigenciaInicio", "TributosVigenciaFim", "TributosAtualizadosEm",
            "PrecoVenda", "PrecoCusto", "PrecoPromocional",
            "Estoque", "EstoqueMinimo", "Ativo", "Destaque", "CriadoEm",
        };
        var linhas = produtos.Select(p => new object?[]
        {
            p.Id, p.Name, p.Category, p.Description, p.Barcode, p.Ncm, p.Cest,
            p.PercentualTributosFederais, p.PercentualTributosEstaduais, p.PercentualTributosMunicipais, p.FonteTributos,
            p.TributosPreenchidosAutomaticamente, p.IbptVersao, p.IbptChave,
            p.TributosVigenciaInicio, p.TributosVigenciaFim, p.TributosAtualizadosEm,
            p.PriceInCents / 100m, p.CostPriceInCents / 100m,
            p.DiscountPriceInCents.HasValue ? p.DiscountPriceInCents.Value / 100m : (decimal?)null,
            p.StockQuantity, p.MinimumStock, p.IsActive, p.IsFeatured, p.CreatedAt,
        });

        await _audit.LogAsync("ExportouDados", "Product", details: $"{produtos.Count} produtos", httpContext: HttpContext);

        return ExportFile(headers, linhas, "produtos", formato);
    }

    /// <summary>Exporta a base de clientes (não inclui staff/admin nem qualquer dado de
    /// autenticação — só o necessário pra recadastro em outro sistema).</summary>
    /// <param name="formato">"csv" (padrão) ou "xlsx".</param>
    [HttpGet("clientes")]
    public async Task<IActionResult> ExportClientes([FromQuery] string formato = "csv")
    {
        var clientes = await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Customer)
            .OrderBy(u => u.Name)
            .ToListAsync();

        var headers = new[]
        {
            "Id", "Nome", "Email", "CPF", "WhatsApp",
            "SaldoPontos", "SaldoCashback", "Ativo", "CadastradoEm",
        };
        var linhas = clientes.Select(u => new object?[]
        {
            u.Id, u.Name, u.Email, u.Cpf, u.WhatsApp,
            u.PointsBalance, u.BalanceInCents / 100m, u.IsActive, u.CreatedAt,
        });

        await _audit.LogAsync("ExportouDados", "User", details: $"{clientes.Count} clientes", httpContext: HttpContext);

        return ExportFile(headers, linhas, "clientes", formato);
    }

    /// <summary>Exporta os crediários em aberto (saldo devedor) — histórico já quitado
    /// fica de fora de propósito, é o que importa pra continuar cobrando após migrar.</summary>
    /// <param name="formato">"csv" (padrão) ou "xlsx".</param>
    [HttpGet("crediario")]
    public async Task<IActionResult> ExportCrediario([FromQuery] string formato = "csv")
    {
        var crediarios = await _db.Crediarios.AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.Status == CrediariosStatus.Aberto)
            .OrderBy(c => c.DataVencimento)
            .ToListAsync();

        // ClienteCPF/ClienteEmail entram pra esse CSV poder voltar via /api/import/crediario
        // sem ambiguidade — nome sozinho duplica entre clientes diferentes.
        var headers = new[]
        {
            "ClienteNome", "ClienteCPF", "ClienteEmail", "ClienteWhatsApp", "ValorTotal", "ValorPago", "SaldoRestante",
            "DataAbertura", "DataVencimento", "Vencido", "Observacao",
        };
        var linhas = crediarios.Select(c => new object?[]
        {
            c.User.Name, c.User.Cpf, c.User.Email, c.User.WhatsApp,
            c.ValorEmCentavos / 100m, c.ValorPagoEmCentavos / 100m, c.SaldoRestanteEmCentavos / 100m,
            c.DataAbertura, c.DataVencimento, c.DataVencimento < DateTime.UtcNow, c.Observacao,
        });

        await _audit.LogAsync("ExportouDados", "Crediario", details: $"{crediarios.Count} crediários em aberto", httpContext: HttpContext);

        return ExportFile(headers, linhas, "crediario-em-aberto", formato);
    }

    /// <summary>Gera o arquivo no formato pedido — "xlsx" pra planilha real (tipos
    /// numéricos/data preservados, útil pra tabela dinâmica), qualquer outro valor
    /// cai no CSV padrão (compatibilidade com quem já usava sem o parâmetro).</summary>
    private FileContentResult ExportFile(string[] headers, IEnumerable<object?[]> linhas, string nomeBase, string formato)
    {
        var data = linhas as object?[][] ?? linhas.ToArray(); // evita enumerar 2x se vier de uma query já materializada
        if (string.Equals(formato, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = ExcelWriter.Build(headers, data, nomeBase);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{nomeBase}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
        }

        var csvBytes = CsvWriter.Build(headers, data);
        return File(csvBytes, "text/csv", $"{nomeBase}-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }
}
