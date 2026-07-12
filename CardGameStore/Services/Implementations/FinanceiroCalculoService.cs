// =============================================================================
// FinanceiroCalculoService.cs — Cálculo de receita/custo/margem mesclando
// Comanda+VendaAvulsa. Extraído de AnalyticsController.GetFinanceiro pra ser
// reutilizado também pelo fechamento formal de período (FechamentoBackgroundService).
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class FinanceiroCalculoService : IFinanceiroCalculoService
{
    // Fuso horário de Brasília — funciona em Linux (IANA) e Windows (ID legado).
    private static readonly TimeZoneInfo BrazilZone = GetBrazilZone();
    private static TimeZoneInfo GetBrazilZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }

    private readonly AppDbContext        _db;
    private readonly IVendaAvulsaService _vendas;

    public FinanceiroCalculoService(AppDbContext db, IVendaAvulsaService vendas)
    {
        _db     = db;
        _vendas = vendas;
    }

    public async Task<FinanceiroDto> CalcularAsync(
        DateTime iniUtc, DateTime endUtc,
        DateTime dataBrIni, DateTime dataBrFim,
        string? filterPaymentMethod = null)
    {
        var ini = iniUtc;
        var end = endUtc;

        var hasPmFilter = !string.IsNullOrWhiteSpace(filterPaymentMethod);

        // ── Comandas base query ───────────────────────────────────────────────
        IQueryable<Comanda> comandasBaseQ = _db.Comandas
            .Where(c => c.ClosedAt >= ini && c.ClosedAt < end && c.Status == ComandaStatus.Fechada);

        if (hasPmFilter)
            comandasBaseQ = comandasBaseQ.Where(c =>
                c.PaymentMethod == filterPaymentMethod ||
                c.SecondPaymentMethod == filterPaymentMethod);

        // ── Receita de comandas ───────────────────────────────────────────────
        var receitaComandas = await comandasBaseQ
            .SumAsync(c => (decimal)c.TotalInCents) / 100m;

        // ── Vendas avulsas ──────────────────────────────────────────
        var todasVendas = (await _vendas.GetRecentAsync(2000, ini)).ToList();
        var avulsasPeriodo = todasVendas
            .Where(v => v.SoldAt >= ini && v.SoldAt < end);

        if (hasPmFilter)
            avulsasPeriodo = avulsasPeriodo.Where(v =>
                v.PaymentMethod == filterPaymentMethod ||
                v.SecondPaymentMethod == filterPaymentMethod);

        var avulsasList = avulsasPeriodo.ToList();
        var receitaAvulsa = avulsasList.Sum(v => (decimal)v.TotalInCents) / 100m;

        var receita = receitaComandas + receitaAvulsa;

        // ── Itens de comanda — com categoria e método de pagamento do pai ─────
        var itensRaw = await _db.ComandaItems
            .Where(i => i.Comanda!.ClosedAt >= ini
                     && i.Comanda.ClosedAt < end
                     && i.Comanda.Status == ComandaStatus.Fechada
                     && i.ProductId != null
                     && i.Product != null)
            .Select(i => new {
                i.ItemNameSnapshot,
                i.UnitPriceInCents,
                i.Quantity,
                i.CostPriceSnapshotInCents,
                ComandaClosedAt          = i.Comanda!.ClosedAt,
                ComandaPaymentMethod     = i.Comanda.PaymentMethod,
                ComandaSecondPayment     = i.Comanda.SecondPaymentMethod,
                Categoria                = i.Product!.Category,
            })
            .ToListAsync();

        var itens = hasPmFilter
            ? itensRaw.Where(i =>
                i.ComandaPaymentMethod == filterPaymentMethod ||
                i.ComandaSecondPayment == filterPaymentMethod).ToList()
            : itensRaw;

        var custoComandas = itens
            .Sum(i => (decimal)i.CostPriceSnapshotInCents * i.Quantity) / 100m;

        var custoAvulsa = avulsasList
            .SelectMany(v => v.Items)
            .Sum(i => (decimal)i.UnitCostInCents * i.Quantity) / 100m;

        var custo = custoComandas + custoAvulsa;
        var margem        = receita - custo;
        var margemPercent = custo > 0 ? Math.Round(margem / custo * 100, 1) : 0;

        // ── Crediários em aberto ──────────────────────────────────────────────
        var crediarios = await _db.Crediarios
            .Where(c => c.Status == CrediariosStatus.Aberto)
            .SumAsync(c => (decimal)(c.ValorEmCentavos - c.ValorPagoEmCentavos)) / 100m;

        // ── Breakdown dia a dia ───────────────────────────────────────────────
        // Uma passada só por cada lista, acumulando num dicionário por dia BR
        // (em vez do loop anterior que refiltrava as listas inteiras a cada dia
        // — O(dias×linhas) virou O(linhas+dias)). Mesma regra de bucket do
        // código original: um item pertence ao dia D se, convertido pro fuso de
        // Brasília, cai naquele dia — equivalente a checar
        // BrDateToUtcStart(D) <= x < BrDateToUtcStart(D+1).
        var comandasDoPeriodo = await comandasBaseQ
            .Select(c => new { c.ClosedAt, c.TotalInCents })
            .ToListAsync();

        var receitaPorDia = new Dictionary<DateTime, decimal>();
        var custoPorDia    = new Dictionary<DateTime, decimal>();

        static void Acc(Dictionary<DateTime, decimal> dict, DateTime diaBr, decimal valor)
        {
            dict[diaBr] = dict.TryGetValue(diaBr, out var atual) ? atual + valor : valor;
        }

        foreach (var c in comandasDoPeriodo)
        {
            if (c.ClosedAt is null) continue;
            var diaBr = TimeZoneInfo.ConvertTimeFromUtc(c.ClosedAt.Value, BrazilZone).Date;
            Acc(receitaPorDia, diaBr, c.TotalInCents / 100m);
        }

        foreach (var v in avulsasList)
        {
            var diaBr = TimeZoneInfo.ConvertTimeFromUtc(v.SoldAt, BrazilZone).Date;
            Acc(receitaPorDia, diaBr, v.TotalInCents / 100m);

            foreach (var i in v.Items)
                Acc(custoPorDia, diaBr, (decimal)i.UnitCostInCents * i.Quantity / 100m);
        }

        foreach (var i in itens)
        {
            if (i.ComandaClosedAt is null) continue;
            var diaBr = TimeZoneInfo.ConvertTimeFromUtc(i.ComandaClosedAt.Value, BrazilZone).Date;
            Acc(custoPorDia, diaBr, (decimal)i.CostPriceSnapshotInCents * i.Quantity / 100m);
        }

        var totalDias = (int)(dataBrFim - dataBrIni).TotalDays + 1;
        var diaDia    = new List<DiaFinanceiroDto>();

        for (var d = 0; d < totalDias; d++)
        {
            var dBr = dataBrIni.AddDays(d);
            receitaPorDia.TryGetValue(dBr, out var receitaDia);
            custoPorDia.TryGetValue(dBr, out var custoDia);

            diaDia.Add(new DiaFinanceiroDto
            {
                Dia     = dBr.ToString("yyyy-MM-dd"),
                Receita = Math.Round(receitaDia, 2),
                Custo   = Math.Round(custoDia, 2),
            });
        }

        // ── Breakdown por forma de pagamento ─────────────────────────────────
        var comandasPeriodo = await comandasBaseQ
            .Include(c => c.User)
            .Where(c => c.PaymentMethod != null)
            .Select(c => new
            {
                c.PaymentMethod,
                c.TotalInCents,
                c.PointsApplied,
                c.SecondPaymentMethod,
                c.SecondPaymentAmountInCents,
                c.ClosedAt,
                ClienteNome = c.User != null ? c.User.Name : null,
            })
            .ToListAsync();

        static string fmtReais(decimal v) => $"R$ {v:F2}".Replace('.', ',');
        var transacoesComanda = comandasPeriodo
            .SelectMany(c =>
            {
                // TotalInCents já sai líquido de PointsApplied/DiscountInCents no fechamento
                // (ComandaService.CloseComandaAsync) — não subtrair de novo aqui.
                var net        = c.TotalInCents;
                var hasSecond  = !string.IsNullOrEmpty(c.SecondPaymentMethod) && c.SecondPaymentAmountInCents > 0;
                var secondAmt  = hasSecond ? c.SecondPaymentAmountInCents : 0;
                var primaryAmt = Math.Max(0, net - secondAmt);
                var label2nd   = hasSecond ? $"+ {c.SecondPaymentMethod} {fmtReais(secondAmt / 100m)}" : null;
                var label1st   = hasSecond ? $"+ {c.PaymentMethod} {fmtReais(primaryAmt / 100m)}" : null;

                var list = new List<TransacaoFinDto>
                {
                    new() { Origem = "Comanda", Cliente = c.ClienteNome,
                            Valor = Math.Round(primaryAmt / 100m, 2), Data = c.ClosedAt!.Value,
                            Nota = label2nd, Forma = c.PaymentMethod! }
                };
                if (hasSecond)
                    list.Add(new TransacaoFinDto
                    {
                        Origem = "Comanda", Cliente = c.ClienteNome,
                        Valor = Math.Round(secondAmt / 100m, 2), Data = c.ClosedAt!.Value,
                        Nota = label1st, Forma = c.SecondPaymentMethod!,
                    });
                return list;
            });

        var transacoesAvulsa = avulsasList
            .Select(v => new TransacaoFinDto
            {
                Origem = "VendaAvulsa", Cliente = v.ClientName,
                Valor = Math.Round(v.TotalInCents / 100m, 2), Data = v.SoldAt,
                Forma = v.PaymentMethod,
            });

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

        // ── Top produtos: comandas + PDV com breakdown por origem ─────────────
        var topDeComandas = itens
            .GroupBy(i => i.ItemNameSnapshot)
            .ToDictionary(g => g.Key, g => new
            {
                Categoria   = g.First().Categoria,
                Qtd         = g.Sum(i => i.Quantity),
                Receita     = Math.Round(g.Sum(i => (decimal)i.UnitPriceInCents * i.Quantity) / 100m, 2),
                Custo       = Math.Round(g.Sum(i => (decimal)i.CostPriceSnapshotInCents * i.Quantity) / 100m, 2),
            });

        var topDePdv = avulsasList
            .SelectMany(v => v.Items)
            .GroupBy(i => i.ProductName)
            .ToDictionary(g => g.Key, g => new
            {
                Categoria = g.First().ProductCategory ?? "Outros",
                Qtd       = g.Sum(i => i.Quantity),
                Receita   = Math.Round(g.Sum(i => i.UnitPriceInReais * i.Quantity), 2),
                Custo     = Math.Round(g.Sum(i => (decimal)i.UnitCostInCents * i.Quantity) / 100m, 2),
            });

        var todosNomes = topDeComandas.Keys.Union(topDePdv.Keys);

        var topProdutos = todosNomes.Select(nome =>
        {
            topDeComandas.TryGetValue(nome, out var c);
            topDePdv.TryGetValue(nome, out var a);
            var recC = c?.Receita ?? 0m;
            var recA = a?.Receita ?? 0m;
            var tot  = recC + recA;
            var cus  = (c?.Custo ?? 0m) + (a?.Custo ?? 0m);
            return new TopProductFinDto
            {
                Nome            = nome,
                Categoria       = c?.Categoria ?? a?.Categoria ?? "Outros",
                Qtd             = (c?.Qtd ?? 0) + (a?.Qtd ?? 0),
                QtdComandas     = c?.Qtd ?? 0,
                QtdAvulsa       = a?.Qtd ?? 0,
                Receita         = Math.Round(tot, 2),
                ReceitaComandas = Math.Round(recC, 2),
                ReceitaAvulsa   = Math.Round(recA, 2),
                Custo           = Math.Round(cus, 2),
                Margem          = Math.Round(tot - cus, 2),
            };
        })
        .OrderByDescending(t => t.Receita)
        .Take(30)
        .ToList();

        // ── Pagamentos de crediário recebidos no período ──────────────────────
        var pgtoCrediarioPeriodo = await _db.PagamentosCrediario
            .Include(p => p.Crediario).ThenInclude(c => c.User)
            .Where(p => p.CreatedAt >= ini && p.CreatedAt < end)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var recebidoCrediario = pgtoCrediarioPeriodo.Sum(p => p.ValorEmReais);

        var pagamentosCrediarioPeriodo = pgtoCrediarioPeriodo.Select(p => new PagamentoCrediarioPeriodoDto
        {
            ClienteNome     = p.Crediario?.User?.Name ?? "—",
            ClienteWhatsApp = p.Crediario?.User?.WhatsApp,
            ValorEmReais    = p.ValorEmReais,
            FormaPagamento  = p.FormaPagamento,
            Observacao      = p.Observacao,
            CreatedAt       = p.CreatedAt,
        }).ToList();

        return new FinanceiroDto
        {
            Receita                    = Math.Round(receita, 2),
            ReceitaComandas            = Math.Round(receitaComandas, 2),
            ReceitaAvulsa              = Math.Round(receitaAvulsa, 2),
            Custo                      = Math.Round(custo, 2),
            Margem                     = Math.Round(margem, 2),
            MargemPercent              = margemPercent,
            Crediarios                 = Math.Round(crediarios, 2),
            RecebidoCrediario          = Math.Round(recebidoCrediario, 2),
            DiaDia                     = diaDia,
            TopProdutos                = topProdutos,
            PagamentosPorForma         = todasFormas,
            PagamentosCrediarioPeriodo = pagamentosCrediarioPeriodo,
        };
    }

    /// <summary>Converte uma data local de Brasília no início UTC daquele dia.</summary>
    private static DateTime BrDateToUtcStart(DateTime brDate) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(brDate.Date, DateTimeKind.Unspecified), BrazilZone);

    public async Task<FechamentoPeriodo> FecharJanelaAsync(TipoFechamento tipo, DateTime dataInicioBr, DateTime dataFimBrInclusive)
    {
        var dataIni = dataInicioBr.Date;
        var dataFim = dataFimBrInclusive.Date;
        var ini = BrDateToUtcStart(dataIni);
        var end = BrDateToUtcStart(dataFim.AddDays(1));

        var dto = await CalcularAsync(ini, end, dataIni, dataFim);
        var (custoComandasCents, custoAvulsaCents) = await CalcularCustoSeparadoCentsAsync(ini, end);

        var receitaComandasCents = (long)Math.Round(dto.ReceitaComandas * 100m, MidpointRounding.AwayFromZero);
        var receitaAvulsaCents   = (long)Math.Round(dto.ReceitaAvulsa   * 100m, MidpointRounding.AwayFromZero);
        var margemCents          = receitaComandasCents + receitaAvulsaCents - custoComandasCents - custoAvulsaCents;

        var fechamento = await _db.FechamentosPeriodo.FirstOrDefaultAsync(f =>
            f.Tipo == tipo && f.DataInicio == dataIni && f.DataFim == dataFim);

        if (fechamento is null)
        {
            fechamento = new FechamentoPeriodo { Tipo = tipo, DataInicio = dataIni, DataFim = dataFim };
            _db.FechamentosPeriodo.Add(fechamento);
        }

        fechamento.ReceitaComandas = receitaComandasCents;
        fechamento.ReceitaAvulsa   = receitaAvulsaCents;
        fechamento.CustoComandas   = custoComandasCents;
        fechamento.CustoAvulsa     = custoAvulsaCents;
        fechamento.Margem          = margemCents;
        fechamento.CreatedAt       = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (_db.Entry(fechamento).State == EntityState.Added)
        {
            // Corrida: outra chamada concorrente inseriu essa janela entre o
            // FirstOrDefaultAsync e o SaveChanges — o índice único em
            // (Tipo, DataInicio, DataFim) é a rede de segurança de verdade,
            // essa checagem prévia só evita ruído de log no caso comum.
            // Descarta a tentativa local e reaplica como update em cima da
            // linha que a outra chamada já criou.
            _db.Entry(fechamento).State = EntityState.Detached;
            fechamento = await _db.FechamentosPeriodo.FirstAsync(f =>
                f.Tipo == tipo && f.DataInicio == dataIni && f.DataFim == dataFim);
            fechamento.ReceitaComandas = receitaComandasCents;
            fechamento.ReceitaAvulsa   = receitaAvulsaCents;
            fechamento.CustoComandas   = custoComandasCents;
            fechamento.CustoAvulsa     = custoAvulsaCents;
            fechamento.Margem          = margemCents;
            fechamento.CreatedAt       = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return fechamento;
    }

    /// <summary>
    /// Só o custo, separado por origem, em centavos — usado pelo fechamento
    /// (que grava CustoComandas/CustoAvulsa separados). Mesma regra de
    /// filtro/join que CalcularAsync usa pro custo, sem o resto do
    /// levantamento (categoria, nome do item etc.) que o fechamento não precisa.
    /// </summary>
    private async Task<(long ComandasCents, long AvulsaCents)> CalcularCustoSeparadoCentsAsync(DateTime ini, DateTime end)
    {
        var custoComandasCents = await _db.ComandaItems
            .Where(i => i.Comanda!.ClosedAt >= ini
                     && i.Comanda.ClosedAt < end
                     && i.Comanda.Status == ComandaStatus.Fechada
                     && i.ProductId != null)
            .SumAsync(i => (long)i.CostPriceSnapshotInCents * i.Quantity);

        var vendas = await _vendas.GetRecentAsync(2000, ini);
        var custoAvulsaCents = vendas
            .Where(v => v.SoldAt >= ini && v.SoldAt < end)
            .SelectMany(v => v.Items)
            .Sum(i => (long)i.UnitCostInCents * i.Quantity);

        return (custoComandasCents, custoAvulsaCents);
    }
}
