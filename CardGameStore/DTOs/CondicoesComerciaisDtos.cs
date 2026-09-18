using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

/// <summary>O que foi negociado com uma loja e o que isso vai cobrar nos
/// próximos meses.</summary>
public class CondicoesComerciaisDto
{
    public Guid TenantId { get; set; }

    public decimal Mensalidade { get; set; }

    /// <summary>Vencimento da primeira mensalidade (fim do teste).</summary>
    public DateTime? InicioCobranca { get; set; }

    /// <summary>Dia de vencimento negociado. Null = o dia de InicioCobranca.</summary>
    public int? DiaVencimento { get; set; }

    public ImplantacaoCondicoesDto Implantacao { get; set; } = new();

    public List<DescontoDto> Descontos { get; set; } = new();

    /// <summary>Doze competências a partir da atual.</summary>
    public List<PreviaCompetenciaDto> Previa { get; set; } = new();
}

public class ImplantacaoCondicoesDto
{
    public decimal Valor { get; set; }
    public int Parcelas { get; set; }
    public DateTime? PrimeiroVencimento { get; set; }

    /// <summary>Soma e quantidade das parcelas já pagas — o limite do que uma
    /// renegociação pode reduzir.</summary>
    public decimal ValorPago { get; set; }
    public int ParcelasPagas { get; set; }

    /// <summary>A loja tem implantação lançada à mão (ou anterior ao
    /// parcelamento). Enquanto ela existir, nenhuma parcela é gerada pelas
    /// condições.</summary>
    public bool LancadaManualmente { get; set; }
}

public class DescontoDto
{
    public Guid Id { get; set; }
    public string Descricao { get; set; } = string.Empty;

    /// <summary>"Percentual" ou "ValorFixo".</summary>
    public string Tipo { get; set; } = string.Empty;

    public decimal Valor { get; set; }
    public DateTime CompetenciaInicial { get; set; }
    public DateTime? CompetenciaFinal { get; set; }

    /// <summary>"Vigente", "Futuro" ou "Encerrado", em relação à competência atual.</summary>
    public string Situacao { get; set; } = string.Empty;

    public string? CriadoPor { get; set; }
    public DateTime CriadoEm { get; set; }
}

public class PreviaCompetenciaDto
{
    public DateTime Competencia { get; set; }
    public List<PreviaItemDto> Itens { get; set; } = new();
    public decimal Total { get; set; }
}

public class PreviaItemDto
{
    /// <summary>"Mensalidade" ou "Implantacao".</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>"Mensalidade" ou "Implantação 2/3".</summary>
    public string Descricao { get; set; } = string.Empty;

    public decimal? ValorBruto { get; set; }
    public decimal Desconto { get; set; }
    public string? DescricaoDesconto { get; set; }
    public decimal Valor { get; set; }
    public DateTime Vencimento { get; set; }

    /// <summary>"Prevista" (ainda não gerada), "SemCobranca" (desconto
    /// integral), "EmAberto", "Emitida" ou "Paga".</summary>
    public string Situacao { get; set; } = string.Empty;

    /// <summary>Cobrança já existente editada à mão ou lançada manualmente — não
    /// acompanha as condições.</summary>
    public bool Manual { get; set; }
}

public class AtualizarCondicoesRequest
{
    [Range(0, 1_000_000)]
    public decimal Mensalidade { get; set; }

    public DateTime? InicioCobranca { get; set; }

    [Range(1, 31)]
    public int? DiaVencimento { get; set; }

    [Range(0, 1_000_000)]
    public decimal ImplantacaoValor { get; set; }

    [Range(1, 12)]
    public int ImplantacaoParcelas { get; set; } = 1;

    public DateTime? ImplantacaoPrimeiroVencimento { get; set; }
}

public class SalvarDescontoRequest
{
    [Required, MaxLength(60)]
    public string Descricao { get; set; } = string.Empty;

    /// <summary>"Percentual" ou "ValorFixo".</summary>
    [Required]
    public string Tipo { get; set; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal Valor { get; set; }

    /// <summary>Qualquer data dentro do mês; o serviço normaliza pro dia 1.</summary>
    [Required]
    public DateTime CompetenciaInicial { get; set; }

    /// <summary>Última competência em que vale, inclusive. Null = sem fim.</summary>
    public DateTime? CompetenciaFinal { get; set; }
}

/// <summary>Resposta de toda alteração nas condições: como elas ficaram e o que
/// aconteceu com as cobranças por causa disso.</summary>
public class AlteracaoCondicoesResultDto
{
    public CondicoesComerciaisDto Condicoes { get; set; } = new();
    public SincronizacaoCobrancasResultDto Cobrancas { get; set; } = new();
}
