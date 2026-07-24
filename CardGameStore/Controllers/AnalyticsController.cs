// =============================================================================
// AnalyticsController.cs — Endpoints de analytics para o dashboard admin
// =============================================================================

using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Policy = "AdminOnly")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext              _db;
    private readonly IVendaAvulsaService       _vendas;
    private readonly IFinanceiroCalculoService _financeiro;

    public AnalyticsController(AppDbContext db, IVendaAvulsaService vendas, IFinanceiroCalculoService financeiro)
    {
        _db         = db;
        _vendas     = vendas;
        _financeiro = financeiro;
    }

    /// <summary>
    /// Resumo do painel geral do admin: vendas de hoje/ontem, comandas abertas,
    /// ticket médio, clientes ativos/inativos, curva horária de vendas do dia,
    /// top 5 produtos e formas de pagamento — tudo dos últimos 30-60 dias.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardAnalyticsDto>> GetDashboard()
    {
        var agoraBr     = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilTime.Zone);
        var hojeInicio  = BrazilTime.DateToUtcStart(agoraBr.Date);
        var ontemInicio = hojeInicio.AddDays(-1);
        var ha30Dias    = hojeInicio.AddDays(-30);
        var ha60Dias    = hojeInicio.AddDays(-60);
        var inicioMes   = BrazilTime.DateToUtcStart(new DateTime(agoraBr.Year, agoraBr.Month, 1));

        // ── Comandas fechadas ─────────────────────────────────────────────────
        var comandasHoje = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= hojeInicio && c.ClosedAt < hojeInicio.AddDays(1))
            .Select(c => new { c.TotalInCents, c.ClosedAt, c.PaymentMethod })
            .ToListAsync();

        var comandasOntem = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= ontemInicio && c.ClosedAt < hojeInicio)
            .SumAsync(c => (long)c.TotalInCents);

        // ── Vendas avulsas — 60 dias cobre todas as métricas do dashboard ──
        // M8: GetInPeriodAsync (sem limite) em vez de GetRecentAsync(5000, ...) — loja com
        // mais de 5.000 vendas avulsas em 60 dias (~83/dia) tinha "hoje"/"ontem" errados,
        // já que o corte por "mais recentes" pode ser consumido antes de cobrir todo o período.
        var vendas60Dias = (await _vendas.GetInPeriodAsync(ha60Dias, DateTime.UtcNow.AddMinutes(5))).ToList();
        var vendasHoje   = vendas60Dias.Where(v => v.SoldAt >= hojeInicio).ToList();
        var vendasOntem  = vendas60Dias.Where(v => v.SoldAt >= ontemInicio && v.SoldAt < hojeInicio).ToList();
        var vendasUlt30  = vendas60Dias.Where(v => v.SoldAt >= ha30Dias).ToList();
        var vendasAnt30  = vendas60Dias.Where(v => v.SoldAt >= ha60Dias && v.SoldAt < ha30Dias).ToList();

        var totalHoje  = (comandasHoje.Sum(c => c.TotalInCents) + vendasHoje.Sum(v => v.TotalInCents)) / 100m;
        var totalOntem = (comandasOntem + vendasOntem.Sum(v => (long)v.TotalInCents)) / 100m;
        var variacao   = totalOntem == 0 ? 0m : Math.Round((totalHoje - totalOntem) / totalOntem * 100, 1);

        // ── Ticket médio (últimos 30 dias — comandas + vendas avulsas) ────────────
        var ticketsRecentes = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= ha30Dias && c.TotalInCents > 0)
            .Select(c => (decimal)c.TotalInCents)
            .ToListAsync();
        ticketsRecentes.AddRange(vendasUlt30.Where(v => v.TotalInCents > 0).Select(v => (decimal)v.TotalInCents));

        var ticketsAnteriores = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= ha60Dias && c.ClosedAt < ha30Dias && c.TotalInCents > 0)
            .Select(c => (decimal)c.TotalInCents)
            .ToListAsync();
        ticketsAnteriores.AddRange(vendasAnt30.Where(v => v.TotalInCents > 0).Select(v => (decimal)v.TotalInCents));

        var ticketMedio    = ticketsRecentes.Count > 0 ? ticketsRecentes.Average() / 100m : 0;
        var ticketAnterior = ticketsAnteriores.Count > 0 ? ticketsAnteriores.Average() / 100m : 0;

        // ── Clientes ──────────────────────────────────────────────────────────
        var totalClientes    = await _db.Users.CountAsync(u => u.IsActive && u.Role == UserRole.Customer);
        var novosClientesMes = await _db.Users.CountAsync(u => u.IsActive && u.Role == UserRole.Customer && u.CreatedAt >= inicioMes);

        var ultimasVisitas = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt != null)
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Ultima = g.Max(c => c.ClosedAt) })
            .ToListAsync();

        var clientesAtivos   = ultimasVisitas.Count(v => v.Ultima >= ha30Dias);
        var clientesInativos = Math.Max(0, totalClientes - clientesAtivos);

        // ── Curva horária do dia ──────────────────────────────────────────────
        var curva = Enumerable.Range(9, 16).Select(h =>
        {
            var ini = hojeInicio.AddHours(h);
            var fim = ini.AddHours(1);
            var vc  = comandasHoje.Where(c => c.ClosedAt >= ini && c.ClosedAt < fim).Sum(c => c.TotalInCents);
            var vv  = vendasHoje.Where(v => v.SoldAt >= ini && v.SoldAt < fim).Sum(v => v.TotalInCents);
            return new HourlyRevenueDto { Hora = $"{h}h", Valor = (vc + vv) / 100m };
        }).ToList();

        // ── Top produtos (últimos 30 dias — comandas + vendas avulsas) ───────────
        var topComandaItens = await _db.ComandaItems
            .Where(i => i.AddedAt >= ha30Dias)
            .GroupBy(i => i.ItemNameSnapshot)
            .Select(g => new TopProductDto
            {
                Nome         = g.Key,
                QuantVendida = g.Sum(i => i.Quantity),
                Receita      = g.Sum(i => i.UnitPriceInCents * i.Quantity) / 100m,
            })
            .ToListAsync();

        var topAvulsaItens = vendasUlt30
            .SelectMany(v => v.Items)
            .GroupBy(i => i.ProductName)
            .Select(g => new TopProductDto
            {
                Nome         = g.Key,
                QuantVendida = g.Sum(i => i.Quantity),
                Receita      = Math.Round(g.Sum(i => (decimal)i.Quantity * i.UnitPriceInReais), 2),
            })
            .ToList();

        var topProdutos = topComandaItens.Concat(topAvulsaItens)
            .GroupBy(t => t.Nome)
            .Select(g => new TopProductDto
            {
                Nome         = g.Key,
                QuantVendida = g.Sum(t => t.QuantVendida),
                Receita      = Math.Round(g.Sum(t => t.Receita), 2),
            })
            .OrderByDescending(t => t.QuantVendida)
            .Take(5)
            .ToList();

        // ── Formas de pagamento (vendas avulsas + comandas hoje) ─────────────────
        var pix      = vendasHoje.Count(v => v.PaymentMethod == PaymentMethod.Pix)
                     + comandasHoje.Count(c => c.PaymentMethod == PaymentMethod.Pix);
        var cartao   = vendasHoje.Count(v => v.PaymentMethod is PaymentMethod.CartaoCredito or PaymentMethod.CartaoDebito)
                     + comandasHoje.Count(c => c.PaymentMethod is PaymentMethod.CartaoCredito or PaymentMethod.CartaoDebito);
        var dinheiro = vendasHoje.Count(v => v.PaymentMethod == PaymentMethod.Dinheiro)
                     + comandasHoje.Count(c => c.PaymentMethod == PaymentMethod.Dinheiro);

        var comandasAbertas = await _db.Comandas.CountAsync(c => c.Status == ComandaStatus.Aberta);

        return Ok(new DashboardAnalyticsDto
        {
            VendasHoje             = totalHoje,
            VendasOntem            = totalOntem,
            VariacaoPercDia        = variacao,
            ComandasAbertas        = comandasAbertas,
            VendasAvulsasHoje      = vendasHoje.Count,
            TicketMedio            = Math.Round(ticketMedio, 2),
            TicketMedioAnterior    = Math.Round(ticketAnterior, 2),
            TotalClientes          = totalClientes,
            ClientesAtivos30Dias   = clientesAtivos,
            ClientesInativos30Dias = clientesInativos,
            NovosClientesMes       = novosClientesMes,
            CurvaVendasDia         = curva,
            TopProdutos            = topProdutos,
            PagamentosPix          = pix,
            PagamentosCartao       = cartao,
            PagamentosDinheiro     = dinheiro,
        });
    }

    /// <summary>
    /// Lista clientes ativos com insights individuais: gasto total, ticket médio,
    /// número de visitas, última visita, saldo/vencimento de pontos e se está
    /// inativo há mais de 30 dias.
    /// </summary>
    /// <param name="apenasInativos">Se true, retorna só clientes sem visita nos últimos 30 dias.</param>
    [HttpGet("clientes")]
    public async Task<ActionResult<List<ClienteInsightDto>>> GetClienteInsights(
        [FromQuery] bool apenasInativos = false)
    {
        var ha30Dias = DateTime.UtcNow.AddDays(-30);

        var usuarios = await _db.Users
            .Where(u => u.IsActive && u.Role == UserRole.Customer)
            .Select(u => new { u.Id, u.Name, u.Email, u.WhatsApp, u.PointsBalance, u.PointsExpiresAt })
            .ToListAsync();

        var estatisticas = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt != null)
            .GroupBy(c => c.UserId)
            .Select(g => new
            {
                UserId       = g.Key,
                NumVisitas   = g.Count(),
                GastoTotal   = g.Sum(c => c.TotalInCents) / 100m,
                UltimaVisita = (DateTime?)g.Max(c => c.ClosedAt),
            })
            .ToListAsync();

        var statsDict = estatisticas.ToDictionary(e => e.UserId);
        var insights = usuarios.Select(u =>
        {
            statsDict.TryGetValue(u.Id, out var stats);
            var ultima = stats?.UltimaVisita;
            int? pontosVencemEm = u.PointsExpiresAt.HasValue
                ? (int)Math.Round((u.PointsExpiresAt.Value - DateTime.UtcNow).TotalDays)
                : null;
            return new ClienteInsightDto
            {
                UserId        = u.Id,
                Nome          = u.Name,
                Email         = u.Email,
                WhatsApp      = u.WhatsApp,
                GastoTotal    = stats?.GastoTotal ?? 0,
                TicketMedio   = stats is { NumVisitas: > 0 }
                    ? Math.Round(stats.GastoTotal / stats.NumVisitas, 2) : 0,
                NumVisitas    = stats?.NumVisitas ?? 0,
                UltimaVisita  = ultima,
                Inativo30     = ultima == null || ultima < ha30Dias,
                Pontos        = u.PointsBalance,
                PontosVencemEm = pontosVencemEm,
            };
        })
        .Where(i => !apenasInativos || i.Inativo30)
        .OrderByDescending(i => i.GastoTotal)
        .ToList();

        return Ok(insights);
    }

    /// <summary>
    /// Calcula receita, custo e margem (comandas + vendas avulsas) no período
    /// filtrado, calendário de Brasília. Sem filtro, usa o mês corrente.
    /// </summary>
    /// <param name="inicio">Início do período (data local, padrão: dia 1 do mês corrente).</param>
    /// <param name="fim">Fim do período, inclusive (data local, padrão: hoje).</param>
    /// <param name="filterPaymentMethod">Filtra o cálculo por uma forma de pagamento específica (ex: "Pix").</param>
    [HttpGet("financeiro")]
    public async Task<ActionResult<FinanceiroDto>> GetFinanceiro(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] string?   filterPaymentMethod = null)
    {
        var agoraBr   = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilTime.Zone);
        var dataBrIni = inicio.HasValue ? inicio.Value.Date : new DateTime(agoraBr.Year, agoraBr.Month, 1);
        var dataBrFim = fim.HasValue    ? fim.Value.Date    : agoraBr.Date;

        var ini = BrazilTime.DateToUtcStart(dataBrIni);
        var end = BrazilTime.DateToUtcStart(dataBrFim.AddDays(1));

        var dto = await _financeiro.CalcularAsync(ini, end, dataBrIni, dataBrFim, filterPaymentMethod);
        return Ok(dto);
    }

    /// <summary>
    /// Consulta um snapshot de período já fechado (FechamentoPeriodo), se existir —
    /// usado pra preferir o número congelado em vez de recalcular ao vivo. 404 se
    /// essa janela específica nunca foi fechada.
    /// </summary>
    /// <param name="tipo">Granularidade da janela: "Dia", "Semana" ou "Mes".</param>
    /// <param name="inicio">Primeiro dia da janela.</param>
    /// <param name="fim">Último dia da janela.</param>
    [HttpGet("fechamentos")]
    public async Task<ActionResult<FechamentoPeriodoDto>> GetFechamento(
        [FromQuery] string   tipo,
        [FromQuery] DateTime inicio,
        [FromQuery] DateTime fim)
    {
        if (!Enum.TryParse<TipoFechamento>(tipo, ignoreCase: true, out var tipoEnum))
            return BadRequest(new { Message = "Tipo inválido — use Dia, Semana ou Mes." });

        // Kind=Utc carimbado na marra — DataInicio/DataFim são timestamptz e
        // [FromQuery] DateTime chega com Kind=Unspecified (ver mesmo comentário
        // em FinanceiroCalculoService.CalcularAsync/FecharJanelaAsync).
        var inicioUtc = DateTime.SpecifyKind(inicio.Date, DateTimeKind.Utc);
        var fimUtc    = DateTime.SpecifyKind(fim.Date, DateTimeKind.Utc);
        var fechamento = await _db.FechamentosPeriodo.AsNoTracking().FirstOrDefaultAsync(f =>
            f.Tipo == tipoEnum && f.DataInicio == inicioUtc && f.DataFim == fimUtc);

        if (fechamento is null) return NotFound();

        return Ok(MapFechamento(fechamento));
    }

    /// <summary>
    /// Fecha (ou refecha) uma janela financeira na hora — serve tanto de backfill
    /// (se o job noturno não rodou) quanto de "reabrir" (rodar de novo sobre uma
    /// janela já fechada recalcula e sobrescreve; é upsert por Tipo/DataInicio/DataFim).
    /// </summary>
    [HttpPost("fechamentos/fechar-agora")]
    public async Task<ActionResult<FechamentoPeriodoDto>> FecharAgora([FromBody] FecharJanelaRequest request)
    {
        if (!Enum.TryParse<TipoFechamento>(request.Tipo, ignoreCase: true, out var tipoEnum))
            return BadRequest(new { Message = "Tipo inválido — use Dia, Semana ou Mes." });

        if (request.DataFim.Date < request.DataInicio.Date)
            return BadRequest(new { Message = "DataFim não pode ser antes de DataInicio." });

        var fechamento = await _financeiro.FecharJanelaAsync(tipoEnum, request.DataInicio, request.DataFim);
        return Ok(MapFechamento(fechamento));
    }

    private static FechamentoPeriodoDto MapFechamento(FechamentoPeriodo f) => new()
    {
        Id              = f.Id,
        Tipo            = f.Tipo.ToString(),
        DataInicio      = f.DataInicio.ToString("yyyy-MM-dd"),
        DataFim         = f.DataFim.ToString("yyyy-MM-dd"),
        ReceitaComandas = f.ReceitaComandas / 100m,
        ReceitaAvulsa   = f.ReceitaAvulsa   / 100m,
        Receita         = (f.ReceitaComandas + f.ReceitaAvulsa) / 100m,
        CustoComandas   = f.CustoComandas / 100m,
        CustoAvulsa     = f.CustoAvulsa   / 100m,
        Custo           = (f.CustoComandas + f.CustoAvulsa) / 100m,
        Margem          = f.Margem / 100m,
        CreatedAt       = f.CreatedAt,
    };
}
