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
            .Where(c => c.ClosedAt >= hojeInicio && c.ClosedAt < hojeInicio.AddDays(1))
            .Select(c => new { c.TotalInCents, c.ClosedAt })
            .ToListAsync();

        var comandasOntem = await _db.Comandas
            .Where(c => c.ClosedAt >= ontemInicio && c.ClosedAt < hojeInicio)
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
            .Where(c => c.ClosedAt >= ha30Dias && c.TotalInCents > 0)
            .Select(c => (decimal)c.TotalInCents)
            .ToListAsync();

        var ticketsAnteriores = await _db.Comandas
            .Where(c => c.ClosedAt >= ha60Dias && c.ClosedAt < ha30Dias && c.TotalInCents > 0)
            .Select(c => (decimal)c.TotalInCents)
            .ToListAsync();

        var ticketMedio    = ticketsRecentes.Count > 0 ? ticketsRecentes.Average() / 100m : 0;
        var ticketAnterior = ticketsAnteriores.Count > 0 ? ticketsAnteriores.Average() / 100m : 0;

        // ── Clientes ──────────────────────────────────────────────────────────
        var totalClientes    = await _db.Users.CountAsync(u => u.IsActive && u.Role == UserRole.Customer);
        var novosClientesMes = await _db.Users.CountAsync(u => u.IsActive && u.Role == UserRole.Customer && u.CreatedAt >= inicioMes);

        var ultimasVisitas = await _db.Comandas
            .Where(c => c.ClosedAt != null)
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
        var cartao   = vendasHoje.Count(v => v.PaymentMethod is "Cartao" or "Credito" or "Debito");
        var dinheiro = vendasHoje.Count(v => v.PaymentMethod == "Dinheiro");

        var comandasAbertas = await _db.Comandas.CountAsync(c => c.Status == "Aberta");

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
            .Select(u => new { u.Id, u.Name, u.Email, u.PointsBalance })
            .ToListAsync();

        var estatisticas = await _db.Comandas
            .Where(c => c.ClosedAt != null)
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
            return new ClienteInsightDto
            {
                UserId       = u.Id,
                Nome         = u.Name,
                Email        = u.Email,
                GastoTotal   = stats?.GastoTotal ?? 0,
                TicketMedio  = stats is { NumVisitas: > 0 }
                    ? Math.Round(stats.GastoTotal / stats.NumVisitas, 2) : 0,
                NumVisitas   = stats?.NumVisitas ?? 0,
                UltimaVisita = ultima,
                Inativo30    = ultima == null || ultima < ha30Dias,
                Pontos       = u.PointsBalance,
            };
        })
        .Where(i => !apenasInativos || i.Inativo30)
        .OrderByDescending(i => i.GastoTotal)
        .ToList();

        return Ok(insights);
    }
}
