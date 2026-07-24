// =============================================================================
// Evento.cs — Gestão de eventos da loja (dia de torneio, festa, etc.) com
// cobrança de entrada. MVP do módulo "eventos": cadastro do evento + venda/
// check-in de entradas. Sem integração fiscal por enquanto — entrada de
// evento não passa pelo motor de NFC-e (decisão a revisar se algum estado
// exigir nota pra esse tipo de cobrança).
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

public enum EventoStatus
{
    Planejado,
    EmAndamento,
    Concluido,
    Cancelado,
}

[Table("eventos")]
public class Evento
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150)]
    [Column("nome")]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(2000)]
    [Column("descricao")]
    public string? Descricao { get; set; }

    [Column("data_evento")]
    public DateTime DataEvento { get; set; }

    /// <summary>Preço padrão da entrada — cada venda pode registrar um valor
    /// diferente (ex: meia-entrada, cortesia), isso aqui é só o sugerido.</summary>
    [Column("preco_entrada_in_cents")]
    public int PrecoEntradaInCents { get; set; }

    /// <summary>Null = sem limite de capacidade.</summary>
    [Column("capacidade_maxima")]
    public int? CapacidadeMaxima { get; set; }

    [Column("status")]
    public EventoStatus Status { get; set; } = EventoStatus.Planejado;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Uma entrada vendida (e opcionalmente confirmada na portaria) pra um evento.</summary>
[Table("evento_entradas")]
public class EventoEntrada
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("evento_id")]
    public Guid EventoId { get; set; }

    [ForeignKey(nameof(EventoId))]
    public Evento? Evento { get; set; }

    [Required, MaxLength(150)]
    [Column("nome_cliente")]
    public string NomeCliente { get; set; } = string.Empty;

    /// <summary>Cliente identificado no momento da venda (nullable — venda anônima não tem UserId).</summary>
    [Column("user_id")]
    public Guid? UserId { get; set; }

    /// <summary>Pix | Dinheiro | CartaoCredito | CartaoDebito | Pontos | Cashback — ver PaymentMethod.</summary>
    [Required, MaxLength(20)]
    [Column("forma_pagamento")]
    public string FormaPagamento { get; set; } = PaymentMethod.Dinheiro;

    [Column("valor_pago_in_cents")]
    public int ValorPagoInCents { get; set; }

    /// <summary>Preenchido quando a pessoa é confirmada na portaria do evento.</summary>
    [Column("check_in_em")]
    public DateTime? CheckInEm { get; set; }

    /// <summary>Preenchido se a entrada foi cancelada/estornada — não conta mais
    /// pra capacidade nem pro faturamento, mas o registro fica pra histórico.</summary>
    [Column("cancelada_em")]
    public DateTime? CanceladaEm { get; set; }

    [Column("vendida_por_admin_id")]
    public Guid VendidaPorAdminId { get; set; }

    [Column("vendida_por_admin_nome")]
    public string VendidaPorAdminNome { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
