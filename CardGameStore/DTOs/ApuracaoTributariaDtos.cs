// =============================================================================
// ApuracaoTributariaDtos.cs — Contrato do comparativo Simples Nacional x Lucro
// Presumido exibido no portal do contador.
//
// Tudo aqui é ESTIMATIVA de apuração, não guia recolhida: o sistema conhece a
// receita e a folha informada, mas não conhece substituição tributária, receitas
// de exportação, retenções na fonte nem crédito de ICMS. O contador usa como
// ponto de partida e ajusta — a UI diz isso explicitamente.
// =============================================================================

namespace CardGameStore.DTOs;

/// <summary>Receita bruta de um mês do histórico usado no RBT12.</summary>
public class ReceitaMensalDto
{
    public int     Ano          { get; set; }
    public int     Mes          { get; set; }
    /// <summary>"08/2026" — pronto pro eixo do gráfico.</summary>
    public string  Competencia  { get; set; } = string.Empty;
    public decimal ReceitaBruta { get; set; }
}

/// <summary>Uma linha do detalhamento de um regime (tributo → base → alíquota → valor).</summary>
public class TributoLinhaDto
{
    public string  Tributo    { get; set; } = string.Empty;
    public decimal Base       { get; set; }
    public decimal Aliquota   { get; set; }
    public decimal Valor      { get; set; }
    public string? Observacao { get; set; }
}

public class ApuracaoSimplesDto
{
    /// <summary>Anexo configurado pelo contador.</summary>
    public string  AnexoConfigurado { get; set; } = "I";
    /// <summary>Anexo efetivamente aplicado — muda quando o fator R reclassifica III↔V.</summary>
    public string  AnexoAplicado    { get; set; } = "I";
    /// <summary>Folha 12m ÷ RBT12, em %. Null quando o anexo não usa fator R.</summary>
    public decimal? FatorR          { get; set; }
    public int      Faixa           { get; set; }
    public decimal  AliquotaNominal { get; set; }
    public decimal  ParcelaDeduzir  { get; set; }
    public decimal  AliquotaEfetiva { get; set; }
    /// <summary>DAS estimado do período = receita do período × alíquota efetiva.</summary>
    public decimal  ValorDas        { get; set; }
    /// <summary>RBT12 acima de R$ 4.800.000 desenquadra do Simples.</summary>
    public bool     ExcedeuLimite   { get; set; }
    /// <summary>RBT12 acima de R$ 3.600.000 joga ICMS/ISS pra fora do DAS (sublimite estadual).</summary>
    public bool     ExcedeuSublimite { get; set; }
    public List<TributoLinhaDto> Linhas { get; set; } = new();
}

public class ApuracaoPresumidoDto
{
    public decimal BaseIrpj       { get; set; }
    public decimal Irpj           { get; set; }
    public decimal AdicionalIrpj  { get; set; }
    public decimal BaseCsll       { get; set; }
    public decimal Csll           { get; set; }
    public decimal Pis            { get; set; }
    public decimal Cofins         { get; set; }
    public decimal Icms           { get; set; }
    public decimal Iss            { get; set; }
    public decimal InssPatronal   { get; set; }
    public decimal Total          { get; set; }
    public decimal AliquotaEfetiva { get; set; }
    public List<TributoLinhaDto> Linhas { get; set; } = new();
}

public class ApuracaoTributariaDto
{
    public DateTime PeriodoInicio { get; set; }
    public DateTime PeriodoFim    { get; set; }
    /// <summary>Meses inteiros no período — usados no limite mensal do adicional de IRPJ.</summary>
    public decimal  MesesNoPeriodo { get; set; }

    public decimal ReceitaBrutaPeriodo { get; set; }
    public decimal Rbt12               { get; set; }
    /// <summary>true quando a loja tem menos de 12 meses de histórico no sistema.</summary>
    public bool    Rbt12Parcial        { get; set; }
    public int     MesesComReceita     { get; set; }
    public List<ReceitaMensalDto> HistoricoReceita { get; set; } = new();

    public decimal FolhaPagamento12m     { get; set; }
    public decimal FolhaPagamentoMensal  { get; set; }

    public ApuracaoSimplesDto   Simples   { get; set; } = new();
    public ApuracaoPresumidoDto Presumido { get; set; } = new();

    /// <summary>"SimplesNacional" ou "LucroPresumido" — o de menor carga no período.</summary>
    public string  RegimeMaisEconomico { get; set; } = "SimplesNacional";
    /// <summary>Diferença absoluta entre os dois regimes no período (R$).</summary>
    public decimal Economia            { get; set; }
    /// <summary>Regime realmente configurado na loja hoje.</summary>
    public string  RegimeAtual         { get; set; } = "SimplesNacional";

    /// <summary>Ressalvas que mudam a leitura do número (histórico curto, folha zerada, ICMS não informado…).</summary>
    public List<string> Alertas { get; set; } = new();
}
