// =============================================================================
// Comanda.cs — Entidade de Comanda (PostgreSQL)
// Agregado central do sistema: representa o "pedido em aberto" de um cliente.
// Qualquer alteração dispara evento SignalR para o painel do admin.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Comanda de um cliente na loja.
/// Ciclo de vida: Aberta → EmAndamento → Fechada | Cancelada
/// </summary>
[Table("comandas")]
public class Comanda
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Relacionamento com o usuário
    // -------------------------------------------------------------------------

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    // -------------------------------------------------------------------------
    // Contexto de abertura (mesa / QR Code)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Identificador da mesa ou espaço onde o cliente está.
    /// Gerado pelo QR Code fixado na mesa (ex: "Mesa-03").
    /// </summary>
    [MaxLength(50)]
    [Column("table_identifier")]
    public string? TableIdentifier { get; set; }

    // -------------------------------------------------------------------------
    // Status e ciclo de vida
    // -------------------------------------------------------------------------

    /// <summary>Status atual da comanda.</summary>
    [Required]
    [Column("status")]
    public ComandaStatus Status { get; set; } = ComandaStatus.Aberta;

    [Column("opened_at")]
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Preenchido quando o Admin fecha ou cancela a comanda.</summary>
    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    /// <summary>Forma de pagamento usada no fechamento (Dinheiro, Pix, CartaoCredito, CartaoDebito, Crediario).</summary>
    [MaxLength(30)]
    [Column("payment_method")]
    public string? PaymentMethod { get; set; }

    /// <summary>Segundo método de pagamento (quando há split: ex. Cashback + Dinheiro).</summary>
    [MaxLength(30)]
    [Column("second_payment_method")]
    public string? SecondPaymentMethod { get; set; }

    /// <summary>Valor pago pelo segundo método, em centavos. Zero quando não há split.</summary>
    [Column("second_payment_amount_in_cents")]
    public int SecondPaymentAmountInCents { get; set; }

    [Column("cash_received_in_cents")]
    public int? CashReceivedInCents { get; set; }

    [Column("change_in_cents")]
    public int ChangeInCents { get; set; }

    [Column("cash_rounding_discount_in_cents")]
    public int CashRoundingDiscountInCents { get; set; }

    // -------------------------------------------------------------------------
    // Totalizadores (calculados e sincronizados a cada item adicionado)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Total em centavos. Recalculado sempre que um ComandaItem é inserido/removido.
    /// Armazenado aqui para performance nas queries do dashboard.
    /// </summary>
    [Column("total_in_cents")]
    public int TotalInCents { get; set; }

    /// <summary>Pontos usados pelo cliente para abater o total desta comanda.</summary>
    [Column("points_applied")]
    public int PointsApplied { get; set; } = 0;

    /// <summary>Desconto administrativo em centavos (loja), separado dos pontos de fidelidade.</summary>
    [Column("discount_in_cents")]
    public int DiscountInCents { get; set; } = 0;

    // Efeitos financeiros congelados no fechamento para permitir estorno fiscal exato.
    [Column("fiscal_effects_captured_at")]
    public DateTime? FiscalEffectsCapturedAt { get; set; }

    [Column("points_debited_at_sale")]
    public int PointsDebitedAtSale { get; set; }

    [Column("cashback_debited_at_sale")]
    public int CashbackDebitedAtSale { get; set; }

    [Column("points_awarded_at_sale")]
    public int PointsAwardedAtSale { get; set; }

    [Column("crediario_id_at_sale")]
    public Guid? CrediarioIdAtSale { get; set; }

    [Column("crediario_amount_at_sale")]
    public int CrediarioAmountAtSale { get; set; }

    /// <summary>Observações do Admin (ex: "cliente solicitou desconto").</summary>
    [MaxLength(500)]
    [Column("notes")]
    public string? Notes { get; set; }

    // -------------------------------------------------------------------------
    // Propriedade calculada
    // -------------------------------------------------------------------------

    [NotMapped]
    public decimal TotalInReais => TotalInCents / 100m;

    // -------------------------------------------------------------------------
    // Navegação
    // -------------------------------------------------------------------------

    public ICollection<ComandaItem> Items { get; set; } = new List<ComandaItem>();

    // ── Decisão fiscal no fechamento (CON-003) ────────────────────────────────
    // Quem fechou a venda escolhe emitir NFC-e ou não. Sem registrar a escolha,
    // a conciliação enxerga "venda sem documento" e não sabe distinguir decisão
    // deliberada de falha do sistema — e o contador recebe uma lista sem
    // contexto, que é justamente o que a seção 36.5 do plano diz ser inviável.
    // Nulo = venda anterior a este registro (não é "não escolheu", é "não sabemos").

    /// <summary>True = pediu emissão no fechamento. False = optou por NÃO emitir.</summary>
    [Column("fiscal_emissao_escolhida")]
    public bool? FiscalEmissaoEscolhida { get; set; }

    /// <summary>Operador que tomou a decisão.</summary>
    [Column("fiscal_decisao_por_user_id")]
    public Guid? FiscalDecisaoPorUserId { get; set; }

    [Column("fiscal_decisao_em")]
    public DateTime? FiscalDecisaoEm { get; set; }
}

// -------------------------------------------------------------------------
// Enum de status da comanda
// -------------------------------------------------------------------------

/// <summary>
/// Estados possíveis de uma comanda.
/// Armazenado como string no banco (HasConversion) para legibilidade nas queries.
/// </summary>
public enum ComandaStatus
{
    /// <summary>Recém-criada via QR Code, aguardando primeiro item.</summary>
    Aberta,

    /// <summary>Já possui itens adicionados.</summary>
    EmAndamento,

    /// <summary>Pagamento confirmado pelo Admin.</summary>
    Fechada,

    /// <summary>Cancelada pelo Admin (sem cobrança).</summary>
    Cancelada
}
