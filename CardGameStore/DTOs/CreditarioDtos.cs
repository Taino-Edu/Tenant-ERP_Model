// =============================================================================
// CreditarioDtos.cs — DTOs do módulo de Crediário
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

/// <summary>Resposta de um crediário (admin e cliente).</summary>
public class CrediariosDto
{
    public Guid      Id                    { get; set; }
    public Guid      UserId                { get; set; }
    public string    UserName              { get; set; } = string.Empty;
    public string?   UserEmail             { get; set; }
    public Guid      ComandaId             { get; set; }
    public decimal   ValorEmReais          { get; set; }
    public decimal   ValorPagoEmReais      { get; set; }
    public decimal   SaldoRestanteEmReais  { get; set; }
    public DateTime  DataAbertura          { get; set; }
    public DateTime  DataVencimento        { get; set; }
    public DateTime? DataPagamento         { get; set; }
    public string    Status                { get; set; } = string.Empty;
    public string?   Observacao            { get; set; }

    /// <summary>True se Status == Aberto e DataVencimento &lt; agora.</summary>
    public bool Vencido { get; set; }

    /// <summary>Dias restantes para vencer (negativo se já venceu).</summary>
    public int DiasRestantes { get; set; }

    /// <summary>Histórico de pagamentos parciais registrados.</summary>
    public List<PagamentoCrediarioDto> Pagamentos { get; set; } = new();
}

/// <summary>DTO de um pagamento parcial do crediário.</summary>
public class PagamentoCrediarioDto
{
    public Guid     Id             { get; set; }
    public decimal  ValorEmReais   { get; set; }
    public string   FormaPagamento { get; set; } = string.Empty;
    public string?  Observacao     { get; set; }
    public DateTime CreatedAt      { get; set; }
}

/// <summary>Body do endpoint PUT /api/crediarios/{id}/pagar (quitação total).</summary>
public class MarcarPagoRequest
{
    /// <summary>Observação opcional (ex: "Pago em dinheiro no balcão").</summary>
    public string? Observacao { get; set; }
}

/// <summary>Body do endpoint POST /api/crediarios/{id}/pagamento (pagamento parcial).</summary>
public class RegistrarPagamentoRequest
{
    /// <summary>Valor pago nesta parcela, em centavos.</summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "O valor do pagamento deve ser maior que zero.")]
    public int ValorEmCentavos { get; set; }

    /// <summary>Forma de pagamento usada (Dinheiro, Pix, CartaoCredito, CartaoDebito).</summary>
    [MaxLength(50)]
    public string FormaPagamento { get; set; } = "Dinheiro";

    /// <summary>Observação opcional.</summary>
    [MaxLength(500)]
    public string? Observacao { get; set; }
}
