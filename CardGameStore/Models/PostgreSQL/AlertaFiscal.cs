// =============================================================================
// AlertaFiscal.cs — Pendência fiscal que alguém precisa resolver (CON-002).
//
// A diferença para as notificações avulsas que existiam antes: um alerta aqui é
// um FATO RECONCILIADO, não um disparo. Cada ciclo recalcula o conjunto de
// pendências reais a partir do estado do banco e casa com o que já está aberto:
//
//   • fato novo            → alerta criado;
//   • fato que continua    → alerta atualizado (idade, severidade, ocorrências);
//   • fato que sumiu       → alerta resolvido automaticamente.
//
// É daí que vem a deduplicação: a chave é derivada do próprio fato, então o
// mesmo problema nunca vira dois alertas — por mais ciclos que passem. E é daí
// que vem a honestidade do painel: um alerta só desaparece sozinho quando a
// condição que o gerou deixou de existir de verdade.
//
// Resolver manualmente é confirmação humana ("eu cuidei disso"), não supressão:
// se o fato continuar verdadeiro no próximo ciclo, o alerta reabre. Uma nota
// rejeitada não deixa de estar rejeitada porque alguém clicou.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

public enum TipoAlertaFiscal
{
    /// <summary>Documento transmitido sem resposta da SEFAZ (RES-001). Imediato.</summary>
    ResultadoIncerto,

    /// <summary>NFC-e emitida offline aguardando retransmissão dentro do prazo legal de 24h.</summary>
    ContingenciaPendente,

    /// <summary>A SEFAZ recusou o documento e a venda segue sem nota válida.</summary>
    NotaRejeitada,

    /// <summary>Vendas de um dia fechado que não geraram documento fiscal nenhum.</summary>
    VendaSemDocumento,

    /// <summary>Buraco na sequência de numeração da série, sem inutilização registrada.</summary>
    LacunaNumeracao,

    /// <summary>O ZIP mensal de XMLs não chegou ao contador neste mês.</summary>
    ExportacaoMensalPendente,
}

public enum SeveridadeAlertaFiscal
{
    /// <summary>Exige ação agora: risco de documento duplicado, perda de prazo legal
    /// ou venda que fica permanentemente sem documento.</summary>
    Critica,

    /// <summary>Exige ação no dia: a venda está sem documento válido, mas ainda há prazo.</summary>
    Alta,

    /// <summary>Exige revisão no fechamento: divergência a esclarecer com o contador.</summary>
    Media,
}

[Table("alertas_fiscais")]
public class AlertaFiscal
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tipo")]
    public TipoAlertaFiscal Tipo { get; set; }

    [Column("severidade")]
    public SeveridadeAlertaFiscal Severidade { get; set; }

    /// <summary>
    /// Identidade do FATO, derivada dele (ex.: <c>ResultadoIncerto:{notaId}</c>,
    /// <c>VendaSemDocumento:2026-08-05</c>). É o que garante que o mesmo problema
    /// não vire dois alertas — única no banco, não por convenção.
    /// </summary>
    [Required, MaxLength(120)]
    [Column("chave")]
    public string Chave { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    [Column("titulo")]
    public string Titulo { get; set; } = string.Empty;

    /// <summary>O que aconteceu e qual é a ação sugerida — o alerta tem que dizer
    /// o que fazer, não só que algo está errado.</summary>
    [Required, MaxLength(1000)]
    [Column("detalhe")]
    public string Detalhe { get; set; } = string.Empty;

    [MaxLength(300)]
    [Column("link")]
    public string? Link { get; set; }

    /// <summary>Nota de origem, quando o alerta é sobre um documento específico.</summary>
    [Column("nota_fiscal_id")]
    public Guid? NotaFiscalId { get; set; }

    /// <summary>Quando o FATO começou (a venda, a contingência, a transmissão sem
    /// resposta) — não quando o alerta foi criado. É esta data que define a idade
    /// e, em vários tipos, a severidade.</summary>
    [Column("ocorrido_em")]
    public DateTime OcorridoEm { get; set; }

    [Column("detectado_em")]
    public DateTime DetectadoEm { get; set; } = DateTime.UtcNow;

    [Column("atualizado_em")]
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>Quantas vezes o fato foi reconfirmado desde a primeira detecção.</summary>
    [Column("ocorrencias")]
    public int Ocorrencias { get; set; } = 1;

    // ── Responsável ───────────────────────────────────────────────────────────

    [Column("responsavel_user_id")]
    public Guid? ResponsavelUserId { get; set; }

    [Column("responsavel_definido_em")]
    public DateTime? ResponsavelDefinidoEm { get; set; }

    // ── Resolução ─────────────────────────────────────────────────────────────

    [Column("resolvido_em")]
    public DateTime? ResolvidoEm { get; set; }

    [Column("resolvido_por_user_id")]
    public Guid? ResolvidoPorUserId { get; set; }

    [MaxLength(500)]
    [Column("resolucao_observacao")]
    public string? ResolucaoObservacao { get; set; }

    /// <summary>True quando quem resolveu foi o próprio sistema, porque a condição
    /// deixou de existir — distinto de alguém ter confirmado que cuidou do caso.</summary>
    [Column("resolvido_automaticamente")]
    public bool ResolvidoAutomaticamente { get; set; }

    /// <summary>Última vez que o fato voltou a ser detectado depois de alguém ter
    /// dado o alerta por resolvido. Reabertura é sinal de que a confirmação foi
    /// otimista — o problema continua lá.</summary>
    [Column("reaberto_em")]
    public DateTime? ReabertoEm { get; set; }

    [Column("reaberturas")]
    public int Reaberturas { get; set; }

    [NotMapped]
    public bool EstaAberto => ResolvidoEm is null;
}
