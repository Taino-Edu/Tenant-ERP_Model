// =============================================================================
// EventoDtos.cs — DTOs do módulo de gestão de eventos (cadastro + entradas).
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public class EventoDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public DateTime DataEvento { get; set; }
    public int PrecoEntradaInCents { get; set; }
    public int? CapacidadeMaxima { get; set; }
    public string Status { get; set; } = string.Empty;
    public int EntradasVendidas { get; set; }
    public int EntradasCheckIn { get; set; }
    public long FaturamentoInCents { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateEventoRequest
{
    [Required, MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Descricao { get; set; }

    [Required]
    public DateTime DataEvento { get; set; }

    [Range(0, int.MaxValue)]
    public int PrecoEntradaInCents { get; set; }

    [Range(1, int.MaxValue)]
    public int? CapacidadeMaxima { get; set; }
}

public class UpdateEventoRequest
{
    [Required, MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Descricao { get; set; }

    [Required]
    public DateTime DataEvento { get; set; }

    [Range(0, int.MaxValue)]
    public int PrecoEntradaInCents { get; set; }

    [Range(1, int.MaxValue)]
    public int? CapacidadeMaxima { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;
}

public class EventoEntradaDto
{
    public Guid Id { get; set; }
    public string NomeCliente { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string FormaPagamento { get; set; } = string.Empty;
    public int ValorPagoInCents { get; set; }
    public DateTime? CheckInEm { get; set; }
    public DateTime? CanceladaEm { get; set; }
    public string VendidaPorAdminNome { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateEntradaRequest
{
    [Required, MaxLength(150)]
    public string NomeCliente { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    [Required]
    public string FormaPagamento { get; set; } = string.Empty;

    /// <summary>Valor efetivamente cobrado — null usa o preço padrão do evento
    /// (permite meia-entrada/cortesia informando um valor diferente).</summary>
    [Range(0, int.MaxValue)]
    public int? ValorPagoInCents { get; set; }
}
