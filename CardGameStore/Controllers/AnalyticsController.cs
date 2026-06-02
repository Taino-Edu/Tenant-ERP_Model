// =============================================================================
// AnalyticsController.cs — Endpoints de analytics para o dashboard admin
// =============================================================================

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
[Authorize(Roles = "Admin")]
public class AnalyticsController : ControllerBase
{
    // Fuso horário de Brasília — funciona em Linux (IANA) e Windows (ID legado).
    private static readonly TimeZoneInfo BrazilZone = GetBrazilZone();
    private static TimeZoneInfo GetBrazilZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }

    /// <summary>
    /// Converte uma data local de Brasília no início UTC daquele dia.
    /// Ex.: 29/05 BR (UTC-3) → 29/05 03:00:00 UTC
    /// </summary>
    private static DateTime BrDateToUtcStart(DateTime brDate) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(brDate.Date, DateTimeKind.Unspecified), BrazilZone);

    private readonly AppDbContext         _db;
    private readonly IVendaAvulsaService  _vendas;

    public AnalyticsController(AppDbContext db, IVendaAvulsaService vendas)
    {
        _db     = db;
        _vendas = vendas;
    }

    // -------------------------------------------------------------------------
    // GET /api/analytics/dashboard
    // -------------------------------------------------------------------------
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardAnalyticsDto>> GetDashboard()
    {
        var agora       = DateTime.UtcNow;
        var hojeInicio  = agora.Date;
        var ontemInicio = hojeInicio.AddDays(-1);
        var ha30Dias    = hojeInicio.AddDays(-30);
        var ha60Dias    = hojeInicio.AddDays(-60);
        var inicioMes   = new DateTime(agora.Year, agora.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── Comandas fechadas ─────────────────────────────────────────────────
        var comandasHoje = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= hojeInicio && c.ClosedAt < hojeInicio.AddDays(1))
            .Select(c => new { c.TotalInCents, c.ClosedAt })
            .ToListAsync();

        var comandasOntem = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= ontemInicio && c.ClosedAt < hojeInicio)
            .SumAsync(c => (long)c.TotalInCents);

        // ── Vendas avulsas (MongoDB) ──────────────────────────────────────────
        var todasVendas  = (await _vendas.GetRecentAsync(500)).ToList();
        var vendasHoje   = todasVendas.Where(v => v.SoldAt >= hojeInicio).ToList();
        var vendasOntem  = todasVendas.Where(v => v.SoldAt >= ontemInicio && v.SoldAt < hojeInicio).ToList();

        var totalHoje  = (comandasHoje.Sum(c => c.TotalInCents) + vendasHoje.Sum(v => v.TotalInCents)) / 100m;
        var totalOntem = (comandasOntem + vendasOntem.Sum(v => (long)v.TotalInCents)) / 100m;
        var variacao   = totalOntem == 0 ? 0m : Math.Round((totalHoje - totalOntem) / totalOntem * 100, 1);

        // ── Ticket médio (últimos 30 dias, só comandas) ───────────────────────
        var ticketsRecentes = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= ha30Dias && c.TotalInCents > 0)
            .Select(c => (decimal)c.TotalInCents)
            .ToListAsync();

        var ticketsAnteriores = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= ha60Dias && c.ClosedAt < ha30Dias && c.TotalInCents > 0)
            .Select(c => (decimal)c.TotalInCents)
            .ToListAsync();

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

        // ── Top produtos (últimos 30 dias, via itens de comanda) ──────────────
        var topProdutos = await _db.ComandaItems
            .Where(i => i.AddedAt >= ha30Dias)
            .GroupBy(i => i.ItemNameSnapshot)
            .Select(g => new TopProductDto
            {
                Nome         = g.Key,
                QuantVendida = g.Sum(i => i.Quantity),
                Receita      = g.Sum(i => i.UnitPriceInCents * i.Quantity) / 100m,
            })
            .OrderByDescending(t => t.QuantVendida)
            .Take(5)
            .ToListAsync();

        // ── Formas de pagamento (vendas avulsas hoje) ─────────────────────────
        var pix      = vendasHoje.Count(v => v.PaymentMethod == "Pix");
        var cartao   = vendasHoje.Count(v => v.PaymentMethod is "CartaoCredito" or "CartaoDebito");
        var dinheiro = vendasHoje.Count(v => v.PaymentMethod == "Dinheiro");

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

    // -------------------------------------------------------------------------
    // GET /api/analytics/clientes
    // Insights por cliente: gasto, ticket médio, inatividade
    // -------------------------------------------------------------------------
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

        var insights = usuarios.Select(u =>
        {
            var stats = estatisticas.FirstOrDefault(e => e.UserId == u.Id);
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

    // -------------------------------------------------------------------------
    // GET /api/analytics/financeiro?inicio=2025-01-01&fim=2025-01-31
    // Controle financeiro: receita, custo e margem no período filtrado
    // -------------------------------------------------------------------------
    [HttpGet("financeiro")]
    public async Task<ActionResult<FinanceiroDto>> GetFinanceiro(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim)
    {
        // As datas chegam do frontend como datas locais de Brasília (ex: "2026-05-29").
        // Convertemos para o intervalo UTC correto: dia BR inicia às 03:00 UTC (UTC-3).
        var agoraBr   = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilZone);
        var dataBrIni = inicio.HasValue ? inicio.Value.Date : new DateTime(agoraBr.Year, agoraBr.Month, 1);
        var dataBrFim = fim.HasValue    ? fim.Value.Date    : agoraBr.Date;

        var ini = BrDateToUtcStart(dataBrIni);
        var end = BrDateToUtcStart(dataBrFim.AddDays(1)); // exclusivo: início do próximo dia BR em UTC

        // ── Receita de comandas no período ────────────────────────────────────
        var receitaComandas = await _db.Comandas
            .Where(c => c.ClosedAt >= ini && c.ClosedAt < end && c.Status == ComandaStatus.Fechada)
            .SumAsync(c => (decimal)c.TotalInCents) / 100m;

        // ── Receita de vendas avulsas (MongoDB) ───────────────────────────────
        var todasVendas    = (await _vendas.GetRecentAsync(2000)).ToList();
        var receitaAvulsa  = todasVendas
            .Where(v => v.SoldAt >= ini && v.SoldAt < end)
            .Sum(v => (decimal)v.TotalInCents) / 100m;

        var receita = receitaComandas + receitaAvulsa;

        // ── Custo (itens de comanda com ProductId no período) ─────────────────
        var itens = await _db.ComandaItems
            .Include(i => i.Product)
            .Include(i => i.Comanda)
            .Where(i => i.Comanda != null
                     && i.Comanda.ClosedAt >= ini
                     && i.Comanda.ClosedAt < end
                     && i.Comanda.Status == ComandaStatus.Fechada
                     && i.ProductId != null
                     && i.Product != null)
            .ToListAsync();

        var custo = itens
            .Sum(i => (decimal)i.Product!.CostPriceInCents * i.Quantity) / 100m;

        var margem       = receita - custo;
        var margemPercent = custo > 0 ? Math.Round(margem / custo * 100, 1) : 0;

        // ── Crediários em aberto (saldo real = total - já pago) ──────────────
        var crediarios = await _db.Crediarios
            .Where(c => c.Status == CrediariosStatus.Aberto)
            .SumAsync(c => (decimal)(c.ValorEmCentavos - c.ValorPagoEmCentavos)) / 100m;

        // ── Breakdown dia a dia (usando datas BR para os intervalos corretos) ──
        var totalDias = (int)(dataBrFim - dataBrIni).TotalDays + 1;
        var diaDia    = new List<DiaFinanceiroDto>();

        for (var d = 0; d < totalDias; d++)
        {
            var dBr  = dataBrIni.AddDays(d);
            var dIni = BrDateToUtcStart(dBr);
            var dFim = BrDateToUtcStart(dBr.AddDays(1)); // exclusivo

            var rComanda = await _db.Comandas
                .Where(c => c.ClosedAt >= dIni && c.ClosedAt < dFim && c.Status == ComandaStatus.Fechada)
                .SumAsync(c => (decimal)c.TotalInCents) / 100m;

            var rAvulsa = todasVendas
                .Where(v => v.SoldAt >= dIni && v.SoldAt < dFim)
                .Sum(v => (decimal)v.TotalInCents) / 100m;

            var cDia = itens
                .Where(i => i.Comanda!.ClosedAt >= dIni && i.Comanda.ClosedAt < dFim)
                .Sum(i => (decimal)i.Product!.CostPriceInCents * i.Quantity) / 100m;

            diaDia.Add(new DiaFinanceiroDto
            {
                Dia     = dBr.ToString("dd/MM"), // exibe data BR (não UTC)
                Receita = Math.Round(rComanda + rAvulsa, 2),
                Custo   = Math.Round(cDia, 2),
            });
        }

        // ── Breakdown por forma de pagamento ─────────────────────────────────
        // Comandas fechadas no período (com cliente para drill-down)
        var comandasPeriodo = await _db.Comandas
            .Include(c => c.User)
            .Where(c => c.ClosedAt >= ini && c.ClosedAt < end && c.Status == ComandaStatus.Fechada && c.PaymentMethod != null)
            .Select(c => new { c.PaymentMethod, c.TotalInCents, c.ClosedAt, ClienteNome = c.User != null ? c.User.Name : null })
            .ToListAsync();

        // Transações combinadas (comanda + avulsa) para agrupamento e drill-down
        var transacoesComanda = comandasPeriodo
            .Select(c => new TransacaoFinDto
            {
                Origem     = "Comanda",
                Cliente    = c.ClienteNome,
                Valor      = Math.Round(c.TotalInCents / 100m, 2),
                Data       = c.ClosedAt!.Value,
                Forma      = c.PaymentMethod!,
            });

        var avulsasPeriodo = todasVendas
            .Where(v => v.SoldAt >= ini && v.SoldAt < end)
            .ToList();

        var transacoesAvulsa = avulsasPeriodo
            .Select(v => new TransacaoFinDto
            {
                Origem     = "VendaAvulsa",
                Cliente    = v.ClientName,
                Valor      = Math.Round(v.TotalInCents / 100m, 2),
                Data       = v.SoldAt,
                Forma      = v.PaymentMethod,
            });

        // Agrupa por forma e inclui transações individuais (drill-down)
        var todasFormas = transacoesComanda.Concat(transacoesAvulsa)
            .GroupBy(t => t.Forma)
            .Select(g => new FormaPagamentoTotalDto
            {
                Forma      = g.Key,
                Total      = Math.Round(g.Sum(t => t.Valor), 2),
                Quantidade = g.Count(),
                Transacoes = g.OrderByDescending(t => t.Data).ToList(),
            })
            .OrderByDescending(f => f.Total)
            .ToList();

        // ── Top produtos com margem ───────────────────────────────────────────
        var topProdutos = itens
            .GroupBy(i => i.ItemNameSnapshot)
            .Select(g =>
            {
                var r = g.Sum(i => (decimal)i.UnitPriceInCents * i.Quantity) / 100m;
                var c = g.Sum(i => (decimal)i.Product!.CostPriceInCents * i.Quantity) / 100m;
                return new TopProductFinDto
                {
                    Nome    = g.Key,
                    Qtd     = g.Sum(i => i.Quantity),
                    Receita = Math.Round(r, 2),
                    Custo   = Math.Round(c, 2),
                    Margem  = Math.Round(r - c, 2),
                };
            })
            .OrderByDescending(t => t.Receita)
            .Take(10)
            .ToList();

        return Ok(new FinanceiroDto
        {
            Receita         = Math.Round(receita, 2),
            ReceitaComandas = Math.Round(receitaComandas, 2),
            ReceitaAvulsa   = Math.Round(receitaAvulsa, 2),
            Custo           = Math.Round(custo, 2),
            Margem          = Math.Round(margem, 2),
            MargemPercent   = margemPercent,
            Crediarios      = Math.Round(crediarios, 2),
            DiaDia             = diaDia,
            TopProdutos        = topProdutos,
            PagamentosPorForma = todasFormas,
        });
    }
}
