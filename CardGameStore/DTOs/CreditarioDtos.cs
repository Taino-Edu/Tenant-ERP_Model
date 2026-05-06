// =============================================================================
// CreditarioDtos.cs — DTOs do módulo de Crediário
// =============================================================================

namespace CardGameStore.DTOs;

/// <summary>Resposta de um crediário (admin e cliente).</summary>
public class CrediariosDto
{
    public Guid      Id             { get; set; }
    public Guid      UserId         { get; set; }
    public string    UserName       { get; set; } = string.Empty;
    public string?   UserEmail      { get; set; }
    public Guid      ComandaId      { get; set; }
    public decimal   ValorEmReais   { get; set; }
    public DateTime  DataAbertura   { get; set; }
    public DateTime  DataVencimento { get; set; }
    public DateTime? DataPagamento  { get; set; }
    public string    Status         { get; set; } = string.Empty;
    public string?   Observacao     { get; set; }

    /// <summary>True se Status == Aberto e DataVencimento < agora.</summary>
    public bool Vencido { get; set; }

    /// <summary>Dias restantes para vencer (negativo se já venceu).</summary>
    public int DiasRestantes { get; set; }
}

/// <summary>Body do endpoint PUT /api/crediarios/{id}/pagar.</summary>
public class MarcarPagoRequest
{
    /// <summary>Observação opcional (ex: "Pago em dinheiro no balcão").</summary>
    public string? Observacao { get; set; }
}
