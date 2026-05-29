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
    public Guid?     ComandaId             { get; set; }
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

    /// <summary>Itens da comanda de origem (null = dívida manual sem comanda).</summary>
    public List<ItemCrediarioDto> ItensComanda { get; set; } = new();
}

/// <summary>Item da comanda vinculada ao crediário (somente leitura).</summary>
public class ItemCrediarioDto
{
    public string  ItemName        { get; set; } = string.Empty;
    public int     Quantity        { get; set; }
    public decimal UnitPriceInReais { get; set; }
    public decimal SubtotalInReais  { get; set; }
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

/// <summary>Body do endpoint POST /api/crediarios (criação manual — dívidas anteriores ao sistema).</summary>
public class CriarCrediarioManualRequest
{
    /// <summary>ID do cliente que tem a dívida.</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Valor da dívida em centavos.</summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "O valor deve ser maior que zero.")]
    public int ValorEmCentavos { get; set; }

    /// <summary>Observação (ex: "Dívida de torneio 12/04/2025").</summary>
    [MaxLength(500)]
    public string? Observacao { get; set; }

    /// <summary>Vencimento customizado. Se null, usa DataAbertura + 30 dias.</summary>
    public DateTime? DataVencimento { get; set; }

    /// <summary>
    /// Lista de itens que compõem a dívida (opcional).
    /// Serializada como JSON no campo ItensJson da entidade.
    /// </summary>
    public List<ItemCrediarioDto>? Itens { get; set; }
}

/// <summary>Body do endpoint PATCH /api/crediarios/{id} (edição de crediário em aberto).</summary>
public class EditarCrediarioRequest
{
    /// <summary>Novo valor total em centavos. Se null, mantém o atual.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "O valor deve ser maior que zero.")]
    public int? ValorEmCentavos { get; set; }

    /// <summary>Nova observação. Se null, mantém a atual.</summary>
    [MaxLength(500)]
    public string? Observacao { get; set; }

    /// <summary>Nova data de vencimento. Se null, mantém a atual.</summary>
    public DateTime? DataVencimento { get; set; }
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
