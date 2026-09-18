// =============================================================================
// CondicoesComerciais.cs — A conta do que uma loja deve em cada competência, a
// partir do que foi negociado com ela.
//
// Sem banco e sem relógio de propósito: o gerador mensal, o recálculo depois de
// uma renegociação e a prévia que a tela mostra passam todos por aqui. Se cada
// um fizesse a própria conta, a prévia prometeria um valor e a cobrança sairia
// com outro.
// =============================================================================

using System.Globalization;
using CardGameStore.Multitenancy;

namespace CardGameStore.Services;

/// <summary>Mensalidade de uma competência depois dos descontos.</summary>
public record MensalidadeCalculada(decimal Base, decimal Desconto, decimal Valor, string? DescricaoDesconto);

/// <summary>Uma parcela da implantação.</summary>
public record ParcelaImplantacao(int Numero, int Total, decimal Valor, DateTime Vencimento)
{
    public DateTime Competencia => CondicoesComerciais.Competencia(Vencimento);
}

public static class CondicoesComerciais
{
    /// <summary>Teto de parcelas da implantação. Doze cobre qualquer negociação
    /// razoável; acima disso a parcela fica perto do piso de R$ 5,00 do Asaas e a
    /// tarifa fixa por cobrança come a margem.</summary>
    public const int MaxParcelasImplantacao = 12;

    /// <summary>Dia 1 do mês, 00:00 UTC — a forma canônica de competência.</summary>
    public static DateTime Competencia(DateTime data) =>
        new(data.Year, data.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>O dia pedido dentro da competência, sem estourar em mês curto:
    /// 31 vira 28 (ou 29) em fevereiro.</summary>
    public static DateTime DiaNoMes(DateTime competencia, int dia)
    {
        var ultimo = DateTime.DaysInMonth(competencia.Year, competencia.Month);
        return new DateTime(competencia.Year, competencia.Month, Math.Clamp(dia, 1, ultimo), 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime SoODia(DateTime data) =>
        new(data.Year, data.Month, data.Day, 0, 0, 0, DateTimeKind.Utc);

    // ── Mensalidade ──────────────────────────────────────────────────────────

    /// <summary>Vencimento da mensalidade da competência, ou null quando a loja
    /// ainda não entrou em cobrança nela. A primeira vence exatamente no início
    /// combinado; as seguintes, no dia de vencimento negociado.</summary>
    public static DateTime? VencimentoDaMensalidade(Tenant tenant, DateTime competencia)
    {
        if (tenant.BillingStartsOn is not { } inicio) return null;

        var comp       = Competencia(competencia);
        var compInicio = Competencia(inicio);

        if (comp < compInicio) return null;
        if (comp == compInicio) return SoODia(inicio);

        return DiaNoMes(comp, tenant.BillingDueDay ?? inicio.Day);
    }

    public static bool DescontoVale(TenantBillingDiscount desconto, DateTime competencia)
    {
        var comp = Competencia(competencia);
        return desconto.StartMonth <= comp && (desconto.EndMonth is null || desconto.EndMonth >= comp);
    }

    /// <summary>Aplica os descontos vigentes na competência.
    ///
    /// Como eles empilham: os percentuais SOMAM entre si (10% + 20% = 30%, não
    /// 28%) e incidem sobre a mensalidade base; depois os valores fixos são
    /// abatidos. A soma dos percentuais para em 100% e o resultado nunca fica
    /// negativo. A ordem fixa é o que torna a conta previsível pra quem negocia:
    /// "10% do parceiro mais R$ 30 de boas-vindas" dá o mesmo número
    /// independentemente de qual foi cadastrado primeiro.</summary>
    public static MensalidadeCalculada CalcularMensalidade(
        decimal mensalidadeBase, IEnumerable<TenantBillingDiscount> descontos, DateTime competencia)
    {
        var vigentes = descontos
            .Where(d => DescontoVale(d, competencia))
            .OrderBy(d => d.Kind)
            .ThenBy(d => d.CreatedAt)
            .ToList();

        if (mensalidadeBase <= 0 || vigentes.Count == 0)
            return new MensalidadeCalculada(mensalidadeBase, 0m, Math.Max(mensalidadeBase, 0m), null);

        var percentual = Math.Min(100m, vigentes.Where(d => d.Kind == TenantDiscountKind.Percentual).Sum(d => d.Value));
        var aposPercentual = decimal.Round(mensalidadeBase * (100m - percentual) / 100m, 2, MidpointRounding.AwayFromZero);
        var fixo = vigentes.Where(d => d.Kind == TenantDiscountKind.ValorFixo).Sum(d => d.Value);
        var valor = Math.Max(0m, aposPercentual - fixo);

        return new MensalidadeCalculada(mensalidadeBase, mensalidadeBase - valor, valor, Descrever(vigentes));
    }

    private static string Descrever(IEnumerable<TenantBillingDiscount> descontos)
    {
        var texto = string.Join(" + ", descontos.Select(d => d.Kind == TenantDiscountKind.Percentual
            ? $"{d.Description} {Numero(d.Value, "0.##")}%"
            : $"{d.Description} R$ {Numero(d.Value, "0.00")}"));

        return texto.Length <= 300 ? texto : texto[..297] + "...";
    }

    /// <summary>Vírgula decimal sem depender da cultura do servidor: o container
    /// pode rodar em modo de globalização invariante, e aí "pt-BR" formataria
    /// com ponto.</summary>
    internal static string Numero(decimal valor, string formato) =>
        valor.ToString(formato, CultureInfo.InvariantCulture).Replace('.', ',');

    // ── Implantação ──────────────────────────────────────────────────────────

    /// <summary>Parcelas da implantação, uma por mês a partir do primeiro
    /// vencimento.
    ///
    /// <paramref name="pagas"/> (número da parcela → valor pago) existe para a
    /// renegociação: o que já foi pago não muda, e o saldo se redistribui entre
    /// as parcelas em aberto. Sem isso, trocar 3 parcelas por 4 depois da
    /// primeira paga recalcularia a primeira também.
    ///
    /// Arredondamento: as parcelas em aberto recebem o valor truncado nos
    /// centavos, e a última delas absorve a sobra — R$ 100 em 3 vira 33,33 +
    /// 33,33 + 33,34, e a soma bate exatamente com o combinado.</summary>
    public static IReadOnlyList<ParcelaImplantacao> ParcelasDaImplantacao(
        decimal valorTotal,
        int parcelas,
        DateTime? primeiroVencimento,
        IReadOnlyDictionary<int, decimal>? pagas = null)
    {
        if (valorTotal <= 0 || primeiroVencimento is null) return [];

        var total    = Math.Clamp(parcelas, 1, MaxParcelasImplantacao);
        var primeira = SoODia(primeiroVencimento.Value);
        pagas ??= new Dictionary<int, decimal>();

        var abertas = Enumerable.Range(1, total).Where(n => !pagas.ContainsKey(n)).ToList();
        var saldo   = Math.Max(0m, valorTotal - pagas.Values.Sum());
        var valorParcela = abertas.Count == 0 ? 0m : Math.Floor(saldo / abertas.Count * 100m) / 100m;

        var resultado = new List<ParcelaImplantacao>(total);
        for (var numero = 1; numero <= total; numero++)
        {
            var vencimento = numero == 1
                ? primeira
                : DiaNoMes(Competencia(primeira).AddMonths(numero - 1), primeira.Day);

            decimal valor;
            if (pagas.TryGetValue(numero, out var pago))
                valor = pago;
            else if (numero == abertas[^1])
                valor = saldo - valorParcela * (abertas.Count - 1);
            else
                valor = valorParcela;

            resultado.Add(new ParcelaImplantacao(numero, total, valor, vencimento));
        }

        return resultado;
    }

    /// <summary>Quando a implantação ganha valor sem data combinada (campo da
    /// lista de lojas, conversão do CRM), a primeira parcela vai junto com a
    /// primeira mensalidade se a loja ainda está no teste; senão, em sete dias.
    /// Nunca "hoje": a fatura sairia vencendo no dia em que chega ao lojista.</summary>
    public static DateTime PrimeiroVencimentoPadraoDaImplantacao(Tenant tenant, DateTime hoje)
    {
        var dia = SoODia(hoje);
        return tenant.BillingStartsOn is { } inicio && SoODia(inicio) > dia
            ? SoODia(inicio)
            : dia.AddDays(7);
    }
}
