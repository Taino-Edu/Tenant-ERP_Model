// =============================================================================
// AnalyticsController.cs — Endpoints de analytics para o dashboard admin
// =============================================================================

using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Middleware;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Policy = "AdminOnly")]
[RequireOperatorPermission(Permissao.Dashboard)]
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

        // O dashboard antigo materializava até 60 dias de VendaAvulsaDto, incluindo
        // o JSON completo de itens, nomes e metadados, em toda requisição. Em uma
        // massa de 50 mil vendas isso adicionava centenas de MB sob concorrência.
        // Cada métrica simples agora é agregada no PostgreSQL; só as vendas de hoje
        // (necessárias para a curva horária) retornam como projeção escalar pequena.
        var vendasHoje = await _db.VendasAvulsas.AsNoTracking()
            .Where(v => v.SoldAt >= hojeInicio && v.SoldAt < hojeInicio.AddDays(1))
            .Select(v => new { v.SoldAt, v.TotalInCents, v.PaymentMethod })
            .ToListAsync();

        var vendasOntemTotal = await _db.VendasAvulsas.AsNoTracking()
            .Where(v => v.SoldAt >= ontemInicio && v.SoldAt < hojeInicio)
            .SumAsync(v => (long)v.TotalInCents);

        var vendasUlt30Stats = await _db.VendasAvulsas.AsNoTracking()
            .Where(v => v.SoldAt >= ha30Dias && v.SoldAt < DateTime.UtcNow.AddMinutes(5) && v.TotalInCents > 0)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Sum(v => (long)v.TotalInCents), Count = g.Count() })
            .FirstOrDefaultAsync();

        var vendasAnt30Stats = await _db.VendasAvulsas.AsNoTracking()
            .Where(v => v.SoldAt >= ha60Dias && v.SoldAt < ha30Dias && v.TotalInCents > 0)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Sum(v => (long)v.TotalInCents), Count = g.Count() })
            .FirstOrDefaultAsync();

        var totalHoje  = (comandasHoje.Sum(c => c.TotalInCents) + vendasHoje.Sum(v => v.TotalInCents)) / 100m;
        var totalOntem = (comandasOntem + vendasOntemTotal) / 100m;
        var variacao   = totalOntem == 0 ? 0m : Math.Round((totalHoje - totalOntem) / totalOntem * 100, 1);

        // ── Ticket médio (últimos 30 dias — comandas + vendas avulsas) ────────────
        var ticketsComandaRecentes = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= ha30Dias && c.TotalInCents > 0)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Sum(c => (long)c.TotalInCents), Count = g.Count() })
            .FirstOrDefaultAsync();

        var ticketsComandaAnteriores = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= ha60Dias && c.ClosedAt < ha30Dias && c.TotalInCents > 0)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Sum(c => (long)c.TotalInCents), Count = g.Count() })
            .FirstOrDefaultAsync();

        var totalTicketsRecentes = (ticketsComandaRecentes?.Total ?? 0) + (vendasUlt30Stats?.Total ?? 0);
        var qtdTicketsRecentes = (ticketsComandaRecentes?.Count ?? 0) + (vendasUlt30Stats?.Count ?? 0);
        var totalTicketsAnteriores = (ticketsComandaAnteriores?.Total ?? 0) + (vendasAnt30Stats?.Total ?? 0);
        var qtdTicketsAnteriores = (ticketsComandaAnteriores?.Count ?? 0) + (vendasAnt30Stats?.Count ?? 0);

        var ticketMedio = qtdTicketsRecentes > 0
            ? totalTicketsRecentes / (decimal)qtdTicketsRecentes / 100m
            : 0;
        var ticketAnterior = qtdTicketsAnteriores > 0
            ? totalTicketsAnteriores / (decimal)qtdTicketsAnteriores / 100m
            : 0;

        // ── Clientes ──────────────────────────────────────────────────────────
        var totalClientes    = await _db.Users.CountAsync(u => u.IsActive && u.Role == UserRole.Customer);
        var novosClientesMes = await _db.Users.CountAsync(u => u.IsActive && u.Role == UserRole.Customer && u.CreatedAt >= inicioMes);

        var clientesAtivos = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= ha30Dias)
            .Select(c => c.UserId)
            .Distinct()
            .CountAsync();
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
        var topProdutos = await _db.Database.SqlQuery<TopProductDto>($"""
            WITH itens_vendidos AS (
                SELECT
                    item.item_name_snapshot AS nome,
                    item.quantity AS quantidade,
                    (item.unit_price_in_cents * item.quantity)::bigint AS receita_centavos
                FROM comanda_items AS item
                INNER JOIN comandas AS comanda ON comanda.id = item.comanda_id
                WHERE comanda.status = 'Fechada'
                  AND comanda.closed_at >= {ha30Dias}

                UNION ALL

                SELECT
                    item ->> 'ProductName' AS nome,
                    COALESCE((item ->> 'Quantity')::integer, 0) AS quantidade,
                    (COALESCE((item ->> 'UnitPriceInCents')::integer, 0)
                        * COALESCE((item ->> 'Quantity')::integer, 0))::bigint AS receita_centavos
                FROM vendas_avulsas AS venda
                CROSS JOIN LATERAL jsonb_array_elements(venda.items_json) AS item
                WHERE venda.sold_at >= {ha30Dias}
            )
            SELECT
                nome AS "Nome",
                SUM(quantidade)::integer AS "QuantVendida",
                ROUND(SUM(receita_centavos)::numeric / 100, 2) AS "Receita"
            FROM itens_vendidos
            WHERE nome IS NOT NULL AND nome <> ''
            GROUP BY nome
            ORDER BY SUM(quantidade) DESC
            LIMIT 5
            """).ToListAsync();

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
    [RequireOperatorPermission(Permissao.Financeiro)]
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
    /// Saldos atuais de estoque, contas a receber/pagar e estimativas do ciclo
    /// financeiro para o período selecionado.
    /// </summary>
    [HttpGet("financeiro/capital-giro")]
    [RequireOperatorPermission(Permissao.Financeiro)]
    public async Task<ActionResult<CapitalGiroDto>> GetCapitalGiro(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim)
    {
        var agoraBr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilTime.Zone);
        var dataBrIni = inicio.HasValue ? inicio.Value.Date : new DateTime(agoraBr.Year, agoraBr.Month, 1);
        var dataBrFim = fim.HasValue ? fim.Value.Date : agoraBr.Date;
        if (dataBrFim < dataBrIni)
            return BadRequest(new { Message = "A data final não pode ser anterior à data inicial." });

        var ini = BrazilTime.DateToUtcStart(dataBrIni);
        var end = BrazilTime.DateToUtcStart(dataBrFim.AddDays(1));
        return Ok(await _financeiro.CalcularCapitalGiroAsync(ini, end, dataBrIni, dataBrFim));
    }

    /// <summary>Agenda de entradas e saídas abertas pelos próximos 7 a 90 dias.</summary>
    [HttpGet("financeiro/agenda-caixa")]
    [RequireOperatorPermission(Permissao.Financeiro)]
    public async Task<ActionResult<AgendaCaixaDto>> GetAgendaCaixa([FromQuery] int dias = 30)
    {
        if (dias is < 7 or > 90)
            return BadRequest(new { Message = "O horizonte deve ficar entre 7 e 90 dias." });

        return Ok(await _financeiro.CalcularAgendaCaixaAsync(dias));
    }

    /// <summary>Estoque atual cruzado com vendas, margem e cobertura do período.</summary>
    [HttpGet("financeiro/estoque-inteligente")]
    [RequireOperatorPermission(Permissao.Financeiro)]
    public async Task<ActionResult<EstoqueInteligenteDto>> GetEstoqueInteligente(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim)
    {
        var agoraBr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilTime.Zone);
        var dataBrIni = inicio.HasValue ? inicio.Value.Date : new DateTime(agoraBr.Year, agoraBr.Month, 1);
        var dataBrFim = fim.HasValue ? fim.Value.Date : agoraBr.Date;
        if (dataBrFim < dataBrIni)
            return BadRequest(new { Message = "A data final não pode ser anterior à data inicial." });

        var ini = BrazilTime.DateToUtcStart(dataBrIni);
        var end = BrazilTime.DateToUtcStart(dataBrFim.AddDays(1));
        return Ok(await _financeiro.CalcularEstoqueInteligenteAsync(ini, end, dataBrIni, dataBrFim));
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
    [RequireOperatorPermission(Permissao.Financeiro)]
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
    [RequireOperatorPermission(Permissao.Financeiro)]
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
