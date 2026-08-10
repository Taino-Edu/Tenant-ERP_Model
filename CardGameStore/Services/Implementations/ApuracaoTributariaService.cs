// =============================================================================
// ApuracaoTributariaService.cs — Estimativa de carga tributária no Simples
// Nacional e no Lucro Presumido, sobre a mesma receita, para o contador
// comparar os dois regimes no portal.
//
// O que ESTE serviço sabe: receita de venda (comandas fechadas + vendas
// avulsas), o anexo/folha/alíquotas que o contador cadastrou, e as tabelas da
// LC 123/2006. O que ele NÃO sabe — e por isso nunca é "a guia": substituição
// tributária, receita de exportação, retenções na fonte, crédito de ICMS,
// receitas não passadas pelo PDV e segregação comércio × serviço. Cada uma
// dessas lacunas vira um item em Alertas, pra não ser confundida com precisão.
// =============================================================================

using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class ApuracaoTributariaService : IApuracaoTributariaService
{
    private readonly AppDbContext _db;

    public ApuracaoTributariaService(AppDbContext db) => _db = db;

    // ── Tabelas da LC 123/2006 (vigentes desde 2018) ─────────────────────────
    // Cada faixa: teto do RBT12, alíquota nominal (%), parcela a deduzir (R$).
    private record Faixa(decimal Teto, decimal Aliquota, decimal ParcelaDeduzir);

    private static readonly Dictionary<AnexoSimplesNacional, Faixa[]> Tabelas = new()
    {
        [AnexoSimplesNacional.I] = new[]
        {
            new Faixa(180_000m,   4.00m,      0m),
            new Faixa(360_000m,   7.30m,  5_940m),
            new Faixa(720_000m,   9.50m, 13_860m),
            new Faixa(1_800_000m, 10.70m, 22_500m),
            new Faixa(3_600_000m, 14.30m, 87_300m),
            new Faixa(4_800_000m, 19.00m, 378_000m),
        },
        [AnexoSimplesNacional.II] = new[]
        {
            new Faixa(180_000m,   4.50m,      0m),
            new Faixa(360_000m,   7.80m,  5_940m),
            new Faixa(720_000m,  10.00m, 13_860m),
            new Faixa(1_800_000m, 11.20m, 22_500m),
            new Faixa(3_600_000m, 14.70m, 85_500m),
            new Faixa(4_800_000m, 30.00m, 720_000m),
        },
        [AnexoSimplesNacional.III] = new[]
        {
            new Faixa(180_000m,   6.00m,      0m),
            new Faixa(360_000m,  11.20m,  9_360m),
            new Faixa(720_000m,  13.50m, 17_640m),
            new Faixa(1_800_000m, 16.00m, 35_640m),
            new Faixa(3_600_000m, 21.00m, 125_640m),
            new Faixa(4_800_000m, 33.00m, 648_000m),
        },
        [AnexoSimplesNacional.IV] = new[]
        {
            new Faixa(180_000m,   4.50m,      0m),
            new Faixa(360_000m,   9.00m,  8_100m),
            new Faixa(720_000m,  10.20m, 12_420m),
            new Faixa(1_800_000m, 14.00m, 39_780m),
            new Faixa(3_600_000m, 22.00m, 183_780m),
            new Faixa(4_800_000m, 33.00m, 828_000m),
        },
        [AnexoSimplesNacional.V] = new[]
        {
            new Faixa(180_000m,  15.50m,      0m),
            new Faixa(360_000m,  18.00m,  4_500m),
            new Faixa(720_000m,  19.50m,  9_900m),
            new Faixa(1_800_000m, 20.50m, 17_100m),
            new Faixa(3_600_000m, 23.00m, 62_100m),
            new Faixa(4_800_000m, 30.50m, 540_000m),
        },
    };

    private const decimal LimiteSimples    = 4_800_000m;
    private const decimal SublimiteIcmsIss = 3_600_000m;

    // Lucro Presumido — regime cumulativo de PIS/COFINS.
    private const decimal AliquotaPis           = 0.65m;
    private const decimal AliquotaCofins        = 3.00m;
    private const decimal AliquotaIrpj          = 15.00m;
    private const decimal AliquotaAdicionalIrpj = 10.00m;
    private const decimal AliquotaCsll          = 9.00m;
    private const decimal AliquotaInssPatronal  = 20.00m;
    /// <summary>Faixa mensal isenta do adicional de IRPJ (R$ 60.000 por trimestre).</summary>
    private const decimal LimiteMensalAdicionalIrpj = 20_000m;

    public async Task<List<ReceitaMensalDto>> ReceitaMensalAsync(DateTime primeiroMesBr, int meses)
    {
        var inicioBr = new DateTime(primeiroMesBr.Year, primeiroMesBr.Month, 1);
        var fimBr    = inicioBr.AddMonths(meses);
        var iniUtc   = BrazilTime.DateToUtcStart(inicioBr);
        var fimUtc   = BrazilTime.DateToUtcStart(fimBr);

        // Só duas colunas por linha e uma janela de poucos meses — agrupar em
        // memória evita depender da tradução de fuso horário pro SQL, que é
        // onde erros de virada de mês costumam nascer (a competência é o
        // calendário de Brasília, não o UTC em que o timestamp foi gravado).
        var comandas = await _db.Comandas.AsNoTracking()
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= iniUtc && c.ClosedAt < fimUtc)
            .Select(c => new { Data = c.ClosedAt!.Value, c.TotalInCents })
            .ToListAsync();

        var avulsas = await _db.VendasAvulsas.AsNoTracking()
            .Where(v => v.SoldAt >= iniUtc && v.SoldAt < fimUtc)
            .Select(v => new { Data = v.SoldAt, v.TotalInCents })
            .ToListAsync();

        var porCompetencia = comandas.Concat(avulsas)
            .Select(x => new
            {
                Br = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.Data, DateTimeKind.Utc), BrazilTime.Zone),
                x.TotalInCents,
            })
            .GroupBy(x => (x.Br.Year, x.Br.Month))
            .ToDictionary(g => g.Key, g => g.Sum(x => (decimal)x.TotalInCents) / 100m);

        var resultado = new List<ReceitaMensalDto>(meses);
        for (var i = 0; i < meses; i++)
        {
            var mes = inicioBr.AddMonths(i);
            porCompetencia.TryGetValue((mes.Year, mes.Month), out var receita);
            resultado.Add(new ReceitaMensalDto
            {
                Ano          = mes.Year,
                Mes          = mes.Month,
                Competencia  = $"{mes.Month:00}/{mes.Year}",
                ReceitaBruta = receita,
            });
        }
        return resultado;
    }

    public async Task<ApuracaoTributariaDto> ApurarAsync(DateTime inicioBr, DateTime fimBr)
    {
        inicioBr = inicioBr.Date;
        fimBr    = fimBr.Date;

        var cfg = await _db.FiscalConfigs.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == FiscalConfig.SingletonId) ?? new FiscalConfig();

        // RBT12 é a receita dos 12 meses ANTERIORES ao período apurado; o mês do
        // período entra no cálculo do DAS, não na base da alíquota.
        var mesApuracao = new DateTime(inicioBr.Year, inicioBr.Month, 1);
        var historico   = await ReceitaMensalAsync(mesApuracao.AddMonths(-12), 13);
        var doze        = historico.Take(12).ToList();

        var rbt12           = doze.Sum(m => m.ReceitaBruta);
        var mesesComReceita = doze.Count(m => m.ReceitaBruta > 0);

        var receitaPeriodo = await ReceitaDoPeriodoAsync(inicioBr, fimBr);
        var mesesNoPeriodo = Math.Round(((decimal)(fimBr.AddDays(1) - inicioBr).TotalDays) / 30.44m, 2);
        if (mesesNoPeriodo <= 0) mesesNoPeriodo = 1m;

        var folha12m   = cfg.FolhaPagamento12mEmCentavos / 100m;
        var folhaMes   = cfg.FolhaPagamentoMensalEmCentavos / 100m;

        var alertas = new List<string>();

        // Empresa nova (ou sistema novo) não tem 12 meses de histórico. A regra
        // legal é proporcionalizar a receita dos meses existentes; sem isso a
        // alíquota cairia artificialmente na 1ª faixa.
        var rbt12Parcial = mesesComReceita is > 0 and < 12;
        if (rbt12Parcial)
        {
            rbt12 = Math.Round(rbt12 / mesesComReceita * 12m, 2);
            alertas.Add($"Só há {mesesComReceita} mês(es) de venda no sistema — o RBT12 foi proporcionalizado " +
                        "para 12 meses, como manda o art. 18 §2º da LC 123. Confira contra a receita real da empresa.");
        }
        else if (mesesComReceita == 0)
        {
            alertas.Add("Nenhuma venda registrada nos 12 meses anteriores — sem RBT12, a apuração usa a 1ª faixa " +
                        "e serve apenas de referência.");
        }

        var simples = CalcularSimples(cfg, rbt12, receitaPeriodo, folha12m, alertas);
        var presumido = CalcularPresumido(cfg, receitaPeriodo, folhaMes, mesesNoPeriodo, alertas);

        if (cfg.AliquotaIcmsPercentual <= 0 && cfg.AliquotaIssPercentual <= 0)
            alertas.Add("ICMS e ISS não estão informados na configuração fiscal: o Lucro Presumido está sem esses " +
                        "tributos e sai artificialmente barato no comparativo.");
        if (folhaMes <= 0)
            alertas.Add("Folha de pagamento mensal zerada: o INSS patronal (20%), que existe no Presumido e está " +
                        "embutido no DAS dos anexos I a III, não entrou na conta.");

        alertas.Add("A receita considerada é a das vendas registradas no sistema (comandas fechadas e vendas " +
                    "avulsas), líquida de descontos concedidos. Vendas fora do PDV, substituição tributária, " +
                    "exportações e retenções na fonte não estão contempladas.");

        var maisEconomico = simples.ValorDas <= presumido.Total ? "SimplesNacional" : "LucroPresumido";

        return new ApuracaoTributariaDto
        {
            PeriodoInicio       = inicioBr,
            PeriodoFim          = fimBr,
            MesesNoPeriodo      = mesesNoPeriodo,
            ReceitaBrutaPeriodo = receitaPeriodo,
            Rbt12               = rbt12,
            Rbt12Parcial        = rbt12Parcial,
            MesesComReceita     = mesesComReceita,
            HistoricoReceita    = historico,
            FolhaPagamento12m    = folha12m,
            FolhaPagamentoMensal = folhaMes,
            Simples             = simples,
            Presumido           = presumido,
            RegimeMaisEconomico = maisEconomico,
            Economia            = Math.Abs(simples.ValorDas - presumido.Total),
            RegimeAtual         = cfg.RegimeTributario.ToString(),
            Alertas             = alertas,
        };
    }

    private async Task<decimal> ReceitaDoPeriodoAsync(DateTime inicioBr, DateTime fimBr)
    {
        var iniUtc = BrazilTime.DateToUtcStart(inicioBr);
        var fimUtc = BrazilTime.DateToUtcStart(fimBr.AddDays(1));

        var comandas = await _db.Comandas
            .Where(c => c.Status == ComandaStatus.Fechada && c.ClosedAt >= iniUtc && c.ClosedAt < fimUtc)
            .SumAsync(c => (long)c.TotalInCents);
        var avulsas = await _db.VendasAvulsas
            .Where(v => v.SoldAt >= iniUtc && v.SoldAt < fimUtc)
            .SumAsync(v => (long)v.TotalInCents);

        return (comandas + avulsas) / 100m;
    }

    /// <summary>
    /// Cálculo puro do Simples: sem banco, para poder ser exercitado direto em
    /// teste — é onde moram as tabelas da LC 123, a parte que mais dói se errar.
    /// </summary>
    public static ApuracaoSimplesDto CalcularSimples(
        FiscalConfig cfg, decimal rbt12, decimal receitaPeriodo, decimal folha12m, List<string> alertas)
    {
        var anexoAplicado = cfg.AnexoSimples;
        decimal? fatorR = null;

        // Fator R só reclassifica entre III e V — nos demais anexos a atividade
        // já define a tabela e a folha não muda nada.
        if (cfg.AnexoSimples is AnexoSimplesNacional.III or AnexoSimplesNacional.V)
        {
            fatorR = rbt12 > 0 ? Math.Round(folha12m / rbt12 * 100m, 2) : 0m;
            anexoAplicado = fatorR >= 28m ? AnexoSimplesNacional.III : AnexoSimplesNacional.V;
            if (folha12m <= 0)
                alertas.Add("Folha de 12 meses zerada: o fator R deu 0% e a apuração caiu no Anexo V, o mais caro. " +
                            "Informe a folha na configuração fiscal para o cálculo valer.");
        }

        var tabela = Tabelas[anexoAplicado];
        var indice = Array.FindIndex(tabela, f => rbt12 <= f.Teto);
        if (indice < 0) indice = tabela.Length - 1; // acima do teto: usa a última faixa
        var faixa = tabela[indice];

        // Alíquota efetiva = (RBT12 × nominal − parcela a deduzir) ÷ RBT12.
        // Sem RBT12 não há o que deduzir: cai na nominal da 1ª faixa.
        var aliquotaEfetiva = rbt12 > 0
            ? Math.Round((rbt12 * (faixa.Aliquota / 100m) - faixa.ParcelaDeduzir) / rbt12 * 100m, 4)
            : faixa.Aliquota;
        if (aliquotaEfetiva < 0) aliquotaEfetiva = 0m;

        var das = Math.Round(receitaPeriodo * aliquotaEfetiva / 100m, 2);

        var dto = new ApuracaoSimplesDto
        {
            AnexoConfigurado = cfg.AnexoSimples.ToString(),
            AnexoAplicado    = anexoAplicado.ToString(),
            FatorR           = fatorR,
            Faixa            = indice + 1,
            AliquotaNominal  = faixa.Aliquota,
            ParcelaDeduzir   = faixa.ParcelaDeduzir,
            AliquotaEfetiva  = aliquotaEfetiva,
            ValorDas         = das,
            ExcedeuLimite    = rbt12 > LimiteSimples,
            ExcedeuSublimite = rbt12 > SublimiteIcmsIss,
        };

        dto.Linhas.Add(new TributoLinhaDto
        {
            Tributo    = "DAS (guia única)",
            Base       = receitaPeriodo,
            Aliquota   = aliquotaEfetiva,
            Valor      = das,
            Observacao = $"Anexo {dto.AnexoAplicado}, faixa {dto.Faixa} — RBT12 de {rbt12:N2}",
        });

        if (dto.ExcedeuLimite)
            alertas.Add($"RBT12 de R$ {rbt12:N2} ultrapassa o limite de R$ 4.800.000 do Simples Nacional — " +
                        "há desenquadramento a tratar, o DAS estimado aqui não se aplica.");
        else if (dto.ExcedeuSublimite)
            alertas.Add($"RBT12 de R$ {rbt12:N2} ultrapassa o sublimite de R$ 3.600.000: ICMS e ISS saem do DAS e " +
                        "passam a ser recolhidos direto ao Estado/Município, fora do valor estimado acima.");

        if (anexoAplicado == AnexoSimplesNacional.IV)
            alertas.Add("No Anexo IV a CPP (INSS patronal) não está no DAS — some 20% da folha ao valor do Simples " +
                        "para comparar com o Lucro Presumido em pé de igualdade.");

        return dto;
    }

    /// <summary>Cálculo puro do Lucro Presumido — mesma razão de ser público que <see cref="CalcularSimples"/>.</summary>
    public static ApuracaoPresumidoDto CalcularPresumido(
        FiscalConfig cfg, decimal receita, decimal folhaMensal, decimal mesesNoPeriodo, List<string> alertas)
    {
        var baseIrpj = Math.Round(receita * cfg.PercentualPresuncaoIrpj / 100m, 2);
        var baseCsll = Math.Round(receita * cfg.PercentualPresuncaoCsll / 100m, 2);

        var irpj = Math.Round(baseIrpj * AliquotaIrpj / 100m, 2);

        // Adicional de 10% sobre o lucro presumido que exceder R$ 20.000/mês
        // (R$ 60.000 no trimestre); períodos parciais entram proporcionais.
        var limiteAdicional = Math.Round(LimiteMensalAdicionalIrpj * mesesNoPeriodo, 2);
        var excedente       = Math.Max(0m, baseIrpj - limiteAdicional);
        var adicional       = Math.Round(excedente * AliquotaAdicionalIrpj / 100m, 2);

        var csll   = Math.Round(baseCsll * AliquotaCsll / 100m, 2);
        var pis    = Math.Round(receita * AliquotaPis / 100m, 2);
        var cofins = Math.Round(receita * AliquotaCofins / 100m, 2);
        var icms   = Math.Round(receita * cfg.AliquotaIcmsPercentual / 100m, 2);
        var iss    = Math.Round(receita * cfg.AliquotaIssPercentual / 100m, 2);
        var inss   = Math.Round(folhaMensal * mesesNoPeriodo * AliquotaInssPatronal / 100m, 2);

        var total = irpj + adicional + csll + pis + cofins + icms + iss + inss;

        var dto = new ApuracaoPresumidoDto
        {
            BaseIrpj      = baseIrpj,
            Irpj          = irpj,
            AdicionalIrpj = adicional,
            BaseCsll      = baseCsll,
            Csll          = csll,
            Pis           = pis,
            Cofins        = cofins,
            Icms          = icms,
            Iss           = iss,
            InssPatronal  = inss,
            Total         = total,
            AliquotaEfetiva = receita > 0 ? Math.Round(total / receita * 100m, 4) : 0m,
        };

        dto.Linhas.Add(new TributoLinhaDto { Tributo = "IRPJ", Base = baseIrpj, Aliquota = AliquotaIrpj, Valor = irpj, Observacao = $"presunção de {cfg.PercentualPresuncaoIrpj:0.##}% sobre a receita" });
        if (adicional > 0)
            dto.Linhas.Add(new TributoLinhaDto { Tributo = "Adicional de IRPJ", Base = excedente, Aliquota = AliquotaAdicionalIrpj, Valor = adicional, Observacao = $"parcela do lucro presumido acima de R$ {limiteAdicional:N2}" });
        dto.Linhas.Add(new TributoLinhaDto { Tributo = "CSLL", Base = baseCsll, Aliquota = AliquotaCsll, Valor = csll, Observacao = $"presunção de {cfg.PercentualPresuncaoCsll:0.##}% sobre a receita" });
        dto.Linhas.Add(new TributoLinhaDto { Tributo = "PIS", Base = receita, Aliquota = AliquotaPis, Valor = pis, Observacao = "regime cumulativo" });
        dto.Linhas.Add(new TributoLinhaDto { Tributo = "COFINS", Base = receita, Aliquota = AliquotaCofins, Valor = cofins, Observacao = "regime cumulativo" });
        dto.Linhas.Add(new TributoLinhaDto { Tributo = "ICMS", Base = receita, Aliquota = cfg.AliquotaIcmsPercentual, Valor = icms, Observacao = cfg.AliquotaIcmsPercentual > 0 ? "alíquota média informada pelo contador" : "alíquota não informada — não entrou no total" });
        if (cfg.AliquotaIssPercentual > 0)
            dto.Linhas.Add(new TributoLinhaDto { Tributo = "ISS", Base = receita, Aliquota = cfg.AliquotaIssPercentual, Valor = iss, Observacao = "alíquota do município informada pelo contador" });
        dto.Linhas.Add(new TributoLinhaDto { Tributo = "INSS patronal", Base = Math.Round(folhaMensal * mesesNoPeriodo, 2), Aliquota = AliquotaInssPatronal, Valor = inss, Observacao = "20% sobre a folha informada — no Simples (anexos I a III) já está dentro do DAS" });

        if (cfg.PercentualPresuncaoIrpj >= 32m || cfg.PercentualPresuncaoCsll >= 32m)
            alertas.Add("Percentuais de presunção configurados como serviço (32%). Se a loja fatura mercadoria, " +
                        "o correto é 8% (IRPJ) e 12% (CSLL) — o Presumido está sendo superestimado.");

        return dto;
    }
}
