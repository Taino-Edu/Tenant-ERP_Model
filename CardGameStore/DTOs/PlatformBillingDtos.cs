using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

/// <summary>Uma cobrança da plataforma contra uma loja, já com o nome da loja
/// resolvido (a tela lista por competência, não por tenant — sem o nome aqui
/// ela teria que fazer N buscas).</summary>
public class TenantChargeDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Nome de exibição da loja, com fallback pro slug.</summary>
    public string TenantNome { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;

    /// <summary>"Implantacao" ou "Mensalidade".</summary>
    public string Tipo { get; set; } = string.Empty;

    public decimal Valor { get; set; }
    public DateTime Competencia { get; set; }
    public DateTime Vencimento { get; set; }
    public DateTime? PagoEm { get; set; }
    public string? Observacao { get; set; }

    /// <summary>Em aberto e já passou do vencimento. Calculado no servidor pra a
    /// tela não precisar reimplementar a regra (e divergir dela).</summary>
    public bool Vencida { get; set; }
}

/// <summary>Painel financeiro de um mês de competência.</summary>
public class BillingResumoDto
{
    public DateTime Competencia { get; set; }

    /// <summary>Receita recorrente contratada: soma da mensalidade das lojas
    /// ativas. É quanto DEVERIA entrar por mês daqui pra frente — não depende
    /// de cobrança gerada nem de pagamento.</summary>
    public decimal MrrContratado { get; set; }

    /// <summary>Lojas ativas que pagam alguma coisa (mensalidade maior que zero).</summary>
    public int LojasPagantes { get; set; }

    /// <summary>Lojas ativas sem mensalidade — cortesia, piloto, ou plano fora
    /// da tabela que entrou com valor zero e ninguém preencheu depois.</summary>
    public int LojasSemCobranca { get; set; }

    /// <summary>Total emitido nesta competência (mensalidades + implantações).</summary>
    public decimal Faturado { get; set; }

    /// <summary>Parte do faturado que já foi paga.</summary>
    public decimal Recebido { get; set; }

    /// <summary>Parte do faturado ainda em aberto.</summary>
    public decimal EmAberto { get; set; }

    /// <summary>Inadimplência ACUMULADA: tudo em aberto e vencido, de qualquer
    /// competência, não só desta. É o número que importa de verdade — dívida
    /// velha não some porque o mês virou.</summary>
    public decimal VencidoAcumulado { get; set; }

    public int QtdCobrancas { get; set; }
    public int QtdVencidas { get; set; }
}

public class GerarMensalidadesResultDto
{
    public DateTime Competencia { get; set; }

    /// <summary>Quantas cobranças foram criadas nesta execução.</summary>
    public int Criadas { get; set; }

    /// <summary>Quantas já existiam e foram puladas (execução repetida).</summary>
    public int JaExistiam { get; set; }

    /// <summary>Lojas ativas ignoradas por ainda não terem entrado em cobrança
    /// (15 dias de acesso grátis) ou por não terem mensalidade definida.</summary>
    public int ForaDeCobranca { get; set; }

    public decimal TotalGerado { get; set; }
}

public class DefinirPagamentoRequest
{
    /// <summary>Data do pagamento. Null reabre a cobrança (desfaz a baixa).</summary>
    public DateTime? PagoEm { get; set; }
}

public class GerarMensalidadesRequest
{
    /// <summary>Qualquer data dentro do mês de competência — o serviço normaliza
    /// pro dia 1.</summary>
    [Required]
    public DateTime Competencia { get; set; }
}

// ── Lançamentos manuais ──────────────────────────────────────────────────────
// O gerador de mensalidades cobre o caso repetitivo; a vida real tem o resto:
// implantação negociada, mês de cortesia, ajuste de valor combinado por
// telefone, cobrança emitida com o valor errado. Sem estes três verbos, a saída
// era mexer no banco na mão — que não deixa rastro e não valida nada.

public class CriarCobrancaRequest
{
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>"Implantacao" ou "Mensalidade".</summary>
    [Required]
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Valor em reais. Zero é aceito de propósito: cortesia registrada
    /// vale mais que cobrança ausente — ela aparece no histórico do cliente.</summary>
    [Range(0, 9_999_999)]
    public decimal Valor { get; set; }

    /// <summary>Qualquer data dentro do mês; o serviço normaliza pro dia 1.</summary>
    [Required]
    public DateTime Competencia { get; set; }

    [Required]
    public DateTime Vencimento { get; set; }

    [MaxLength(500)]
    public string? Observacao { get; set; }
}

/// <summary>Alteração de uma cobrança em aberto.
///
/// Tipo e competência ficam de fora de propósito: os dois compõem o índice
/// único que impede cobrar o mesmo mês duas vezes, e permitir editá-los
/// transformaria um ajuste de valor numa colisão de chave a ser descoberta no
/// meio do fluxo. Cobrança emitida no mês errado se exclui e se refaz.</summary>
public class AtualizarCobrancaRequest
{
    [Range(0, 9_999_999)]
    public decimal Valor { get; set; }

    [Required]
    public DateTime Vencimento { get; set; }

    [MaxLength(500)]
    public string? Observacao { get; set; }
}
