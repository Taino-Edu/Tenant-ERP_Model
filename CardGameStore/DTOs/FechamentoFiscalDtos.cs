// =============================================================================
// FechamentoFiscalDtos.cs — Fechamento mensal do portal do contador: o pedido
// de fechamento, o snapshot calculado e a montagem dos CSVs que vão no ZIP.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;

namespace CardGameStore.DTOs;

public class FecharCompetenciaRequest
{
    [Range(2000, 2100)]
    public int Ano { get; init; }

    [Range(1, 12)]
    public int Mes { get; init; }

    [MaxLength(2000)]
    public string? Observacao { get; init; }
}

/// <summary>Uma nota da competência, no recorte que vai pro relatório.</summary>
public class NotaFechamentoDto
{
    public DateTime Data            { get; set; }
    public int?     Serie           { get; set; }
    public int?     Numero          { get; set; }
    public string?  ChaveAcesso     { get; set; }
    public string   Origem          { get; set; } = string.Empty;
    public string   Status          { get; set; } = string.Empty;
    public long     ValorEmCentavos { get; set; }
}

/// <summary>Tudo que o fechamento do mês consolida, antes de virar snapshot ou ZIP.</summary>
public record FechamentoSnapshot(
    FinanceiroDto           Dre,
    ApuracaoTributariaDto   Apuracao,
    int                     NotasAutorizadas,
    int                     NotasCanceladas,
    decimal                 ValorNotasAutorizadas,
    int                     NotasEntrada,
    decimal                 ValorNotasEntrada,
    List<NotaFechamentoDto> Notas,
    List<string>            Pendencias);

/// <summary>
/// Monta os CSVs do pacote mensal. Separador ";" e vírgula decimal: é o que o
/// Excel em português abre sem passar pelo assistente de importação — e é nele
/// que o contador vai abrir.
/// </summary>
public static class ContadorRelatorioCsv
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private static string Valor(decimal v) => v.ToString("N2", PtBr);

    /// <summary>Escapa aspas e envolve o campo — nome de categoria pode conter ";".</summary>
    private static string Texto(string? v) => $"\"{(v ?? string.Empty).Replace("\"", "\"\"")}\"";

    public static IEnumerable<(string Nome, string Conteudo)> Montar(
        string slug, int ano, int mes, FechamentoSnapshot snapshot)
    {
        var competencia = $"{mes:00}-{ano}";
        return new[]
        {
            ($"dre-{slug}-{competencia}.csv",      MontarDre(snapshot)),
            ($"notas-{slug}-{competencia}.csv",    MontarNotas(snapshot)),
            ($"apuracao-{slug}-{competencia}.csv", MontarApuracao(snapshot)),
        };
    }

    private static string MontarDre(FechamentoSnapshot s)
    {
        var dre = s.Dre;
        var sb = new StringBuilder();
        sb.AppendLine("Linha;Valor (R$)");

        void Linha(string label, decimal valor) => sb.AppendLine($"{Texto(label)};{Valor(valor)}");

        Linha("Receita bruta", dre.ReceitaBruta);
        Linha("(-) Descontos e abatimentos", -dre.Deducoes);
        Linha("(-) Impostos sobre vendas", -dre.ImpostosSobreVendas);
        Linha("Receita líquida", dre.ReceitaLiquidaDre);
        Linha("(-) CMV", -dre.Custo);
        Linha("Lucro bruto", dre.ReceitaLiquidaDre - dre.Custo);
        foreach (var item in dre.DespesasPorCategoria)
            Linha($"(-) {item.Categoria}", -item.Valor);
        Linha("Resultado operacional", dre.ResultadoOperacional);
        Linha("(+/-) Resultado financeiro", dre.ResultadoFinanceiro);
        Linha("(-) IRPJ / CSLL", -dre.ImpostosSobreLucro);
        Linha("Resultado líquido", dre.ResultadoLiquido);

        if (s.Pendencias.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Pendências apontadas no fechamento");
            foreach (var pendencia in s.Pendencias) sb.AppendLine(Texto(pendencia));
        }

        return sb.ToString();
    }

    private static string MontarNotas(FechamentoSnapshot s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Data;Serie;Numero;Chave de acesso;Origem;Status;Valor (R$)");
        foreach (var n in s.Notas)
            sb.AppendLine(string.Join(';',
                n.Data.ToString("dd/MM/yyyy"),
                n.Serie?.ToString() ?? string.Empty,
                n.Numero?.ToString() ?? string.Empty,
                // Chave sempre como texto: 44 dígitos viram notação científica
                // se o Excel decidir que é número.
                Texto(n.ChaveAcesso),
                n.Origem,
                n.Status,
                Valor(n.ValorEmCentavos / 100m)));

        sb.AppendLine();
        sb.AppendLine($"Autorizadas;{s.NotasAutorizadas};;;;;{Valor(s.ValorNotasAutorizadas)}");
        sb.AppendLine($"Canceladas;{s.NotasCanceladas}");
        sb.AppendLine($"NF-e de entrada;{s.NotasEntrada};;;;;{Valor(s.ValorNotasEntrada)}");
        return sb.ToString();
    }

    private static string MontarApuracao(FechamentoSnapshot s)
    {
        var a = s.Apuracao;
        var sb = new StringBuilder();

        sb.AppendLine("Base de cálculo;Valor");
        sb.AppendLine($"{Texto("Receita bruta do período")};{Valor(a.ReceitaBrutaPeriodo)}");
        sb.AppendLine($"{Texto("RBT12 (12 meses anteriores)")};{Valor(a.Rbt12)}");
        sb.AppendLine($"{Texto("Folha 12 meses")};{Valor(a.FolhaPagamento12m)}");
        sb.AppendLine();

        sb.AppendLine($"{Texto($"SIMPLES NACIONAL — Anexo {a.Simples.AnexoAplicado}, faixa {a.Simples.Faixa}")}");
        sb.AppendLine("Tributo;Base (R$);Alíquota (%);Valor (R$);Observação");
        foreach (var l in a.Simples.Linhas)
            sb.AppendLine($"{Texto(l.Tributo)};{Valor(l.Base)};{Valor(l.Aliquota)};{Valor(l.Valor)};{Texto(l.Observacao)}");
        sb.AppendLine($"{Texto("Total do Simples")};;;{Valor(a.Simples.ValorDas)}");
        sb.AppendLine();

        sb.AppendLine($"{Texto("LUCRO PRESUMIDO")}");
        sb.AppendLine("Tributo;Base (R$);Alíquota (%);Valor (R$);Observação");
        foreach (var l in a.Presumido.Linhas)
            sb.AppendLine($"{Texto(l.Tributo)};{Valor(l.Base)};{Valor(l.Aliquota)};{Valor(l.Valor)};{Texto(l.Observacao)}");
        sb.AppendLine($"{Texto("Total do Presumido")};;;{Valor(a.Presumido.Total)}");
        sb.AppendLine();

        sb.AppendLine($"{Texto("Regime mais econômico no período")};{Texto(a.RegimeMaisEconomico)}");
        sb.AppendLine($"{Texto("Diferença entre os regimes")};{Valor(a.Economia)}");
        sb.AppendLine($"{Texto("Regime configurado na loja")};{Texto(a.RegimeAtual)}");

        sb.AppendLine();
        sb.AppendLine(Texto("Ressalvas"));
        foreach (var alerta in a.Alertas) sb.AppendLine(Texto(alerta));

        return sb.ToString();
    }
}
