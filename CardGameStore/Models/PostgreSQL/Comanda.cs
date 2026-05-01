// =============================================================================
// Comanda.cs — Entidade de Comanda (PostgreSQL)
// Agregado central do sistema: representa o "pedido em aberto" de um cliente.
// Qualquer alteração dispara evento SignalR para o painel do Maikon.
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

    // -------------------------------------------------------------------------
    // Campeonato (opcional) — comanda pode estar vinculada a um torneio
    // -------------------------------------------------------------------------

    [Column("championship_id")]
    public Guid? ChampionshipId { get; set; }

    [ForeignKey(nameof(ChampionshipId))]
    public Championship? Championship { get; set; }

    // -------------------------------------------------------------------------
    // Totalizadores (calculados e sincronizados a cada item adicionado)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Total em centavos. Recalculado sempre que um ComandaItem é inserido/removido.
    /// Armazenado aqui para performance nas queries do dashboard.
    /// </summary>
    [Column("total_in_cents")]
    public int TotalInCents { get; set; }

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
