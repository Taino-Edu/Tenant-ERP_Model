// =============================================================================
// FechamentoFiscalMensal.cs — Fechamento contábil de uma competência (mês),
// travado no momento em que o contador fecha. Vive no schema do tenant, como
// FechamentoPeriodo — a diferença é o propósito: FechamentoPeriodo é o corte
// gerencial de caixa (dia/semana/mês, recalculável por upsert), este aqui é o
// documento que o contador declara como base da escrituração e por isso NÃO
// pode ser regravado: reabrir é uma ação explícita e auditada (DELETE).
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("fechamentos_fiscais_mensais")]
public class FechamentoFiscalMensal
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Ano da competência (ex.: 2026).</summary>
    [Column("ano")]
    public int Ano { get; set; }

    /// <summary>Mês da competência, 1–12.</summary>
    [Column("mes")]
    public int Mes { get; set; }

    /// <summary>Primeiro dia da competência no calendário de Brasília.</summary>
    [Column("periodo_inicio")]
    public DateTime PeriodoInicio { get; set; }

    /// <summary>Último dia da competência no calendário de Brasília (inclusive).</summary>
    [Column("periodo_fim")]
    public DateTime PeriodoFim { get; set; }

    // ── Números congelados da DRE do mês (R$) ────────────────────────────────

    [Column("receita_bruta", TypeName = "numeric(14,2)")]
    public decimal ReceitaBruta { get; set; }

    [Column("deducoes", TypeName = "numeric(14,2)")]
    public decimal Deducoes { get; set; }

    [Column("impostos_sobre_vendas", TypeName = "numeric(14,2)")]
    public decimal ImpostosSobreVendas { get; set; }

    [Column("receita_liquida", TypeName = "numeric(14,2)")]
    public decimal ReceitaLiquida { get; set; }

    [Column("custo_mercadoria_vendida", TypeName = "numeric(14,2)")]
    public decimal CustoMercadoriaVendida { get; set; }

    [Column("despesas_operacionais", TypeName = "numeric(14,2)")]
    public decimal DespesasOperacionais { get; set; }

    [Column("resultado_operacional", TypeName = "numeric(14,2)")]
    public decimal ResultadoOperacional { get; set; }

    [Column("resultado_liquido", TypeName = "numeric(14,2)")]
    public decimal ResultadoLiquido { get; set; }

    // ── Fiscal ───────────────────────────────────────────────────────────────

    [Column("notas_autorizadas")]
    public int NotasAutorizadas { get; set; }

    [Column("notas_canceladas")]
    public int NotasCanceladas { get; set; }

    [Column("valor_notas_autorizadas", TypeName = "numeric(14,2)")]
    public decimal ValorNotasAutorizadas { get; set; }

    [Column("notas_entrada")]
    public int NotasEntrada { get; set; }

    [Column("valor_notas_entrada", TypeName = "numeric(14,2)")]
    public decimal ValorNotasEntrada { get; set; }

    /// <summary>Regime apurado no fechamento ("SimplesNacional"/"LucroPresumido").</summary>
    [MaxLength(30)]
    [Column("regime_apurado")]
    public string RegimeApurado { get; set; } = string.Empty;

    /// <summary>Imposto devido no regime apurado (DAS ou soma do Presumido), em R$.</summary>
    [Column("imposto_apurado", TypeName = "numeric(14,2)")]
    public decimal ImpostoApurado { get; set; }

    /// <summary>Alíquota efetiva do regime apurado, em %.</summary>
    [Column("aliquota_efetiva", TypeName = "numeric(7,4)")]
    public decimal AliquotaEfetiva { get; set; }

    /// <summary>
    /// Comparativo completo (Simples x Presumido) e pendências, serializados no
    /// momento do fechamento. É o que garante que reabrir o relatório meses
    /// depois mostre os números daquele dia, e não um recálculo silencioso.
    /// </summary>
    [Column("payload_json", TypeName = "jsonb")]
    public string PayloadJson { get; set; } = "{}";

    /// <summary>Observação do contador registrada junto ao fechamento.</summary>
    [MaxLength(2000)]
    [Column("observacao")]
    public string? Observacao { get; set; }

    /// <summary>Id da conta de contador (catálogo) que fechou — null se o próprio lojista fechou.</summary>
    [Column("fechado_por_contador_id")]
    public Guid? FechadoPorContadorId { get; set; }

    [MaxLength(200)]
    [Column("fechado_por_nome")]
    public string? FechadoPorNome { get; set; }

    [Column("fechado_em")]
    public DateTime FechadoEm { get; set; } = DateTime.UtcNow;
}
