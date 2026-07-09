// =============================================================================
// ComandaDtos.cs — DTOs de Comanda e Itens
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

// -------------------------------------------------------------------------
// Request: Adicionar item à comanda (usado pelo Hub e pelo Controller REST)
// -------------------------------------------------------------------------

/// <summary>Dados necessários para adicionar um item a uma comanda.</summary>
public class AddItemToComandaRequest
{
    /// <summary>ID do produto no estoque.</summary>
    public Guid? ProductId { get; set; }

    /// <summary>Variante escolhida (tamanho/cor). Obrigatório quando produto tem HasVariants=true.</summary>
    public Guid? VariantId { get; set; }

    /// <summary>Nome do item (preenchido automaticamente pelo serviço).</summary>
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Preço unitário em centavos (preenchido pelo serviço a partir do estoque).</summary>
    public int UnitPriceInCents { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; } = 1;
}

// -------------------------------------------------------------------------
// Response: Dados da comanda para o dashboard e para o cliente
// -------------------------------------------------------------------------

/// <summary>Snapshot da comanda para exibição no dashboard e confirmações.</summary>
public class ComandaDto
{
    public Guid              Id              { get; set; }
    public string            UserName        { get; set; } = string.Empty;
    public Guid              UserId          { get; set; }
    public string?           TableIdentifier { get; set; }
    public string            Status          { get; set; } = string.Empty;
    public decimal           TotalInReais    { get; set; }
    public int               PointsApplied   { get; set; }
    public int               DiscountInCents { get; set; }
    public DateTime          OpenedAt                    { get; set; }
    public DateTime?         ClosedAt                    { get; set; }
    public string?           PaymentMethod               { get; set; }
    public string?           SecondPaymentMethod         { get; set; }
    public int               SecondPaymentAmountInCents  { get; set; }
    public List<ComandaItemDto> Items                    { get; set; } = new();

    /// <summary>Saldo de pontos do cliente — exibido na modal de fechamento.</summary>
    public int  UserPointsBalance  { get; set; }
    /// <summary>Saldo de cashback do cliente em centavos — exibido na modal de fechamento.</summary>
    public int  UserBalanceInCents { get; set; }
    public string? ProfileImageUrl { get; set; }

  /// <summary>Preenchidos só quando o fechamento pediu emissão de NFC-e (EmitirNotaFiscal=true) —
  /// permite o front abrir o cupom automaticamente quando autoriza, ou avisar o motivo se não.</summary>
  public Guid?   NotaFiscalId             { get; set; }
  public string? NotaFiscalStatus         { get; set; }
  public string? NotaFiscalMotivoRejeicao { get; set; }
}

/// <summary>Request para aplicar pontos a uma comanda.</summary>
public class ApplyPointsRequest
{
    [Range(1, 100000)]
    public int Points { get; set; }
}

/// <summary>Body do endpoint PATCH /api/comanda/{id}/items/{itemId} (editar quantidade).</summary>
public class UpdateItemRequest
{
    [Range(0, 100, ErrorMessage = "Quantidade deve ser entre 0 e 100.")]
    public int Quantity { get; set; }
}

/// <summary>Body do endpoint POST /api/comanda/admin-open (admin abre comanda por um cliente).</summary>
public class AdminOpenComandaRequest
{
    [Required]
    public Guid UserId { get; set; }

    [MaxLength(50)]
    public string? TableIdentifier { get; set; }
}

/// <summary>Body do endpoint PUT /api/comanda/{id}/close.</summary>
public class CloseComandaRequest
{
    /// <summary>
    /// Forma de pagamento principal. Valores aceitos:
    /// Dinheiro | Pix | CartaoCredito | CartaoDebito | Crediario | Pontos | Cashback
    /// </summary>
    [Required]
    public string PaymentMethod { get; set; } = "Dinheiro";

    /// <summary>Observação opcional (ex: "troco de R$50,00").</summary>
    public string? Observacao { get; set; }

    /// <summary>Segundo método de pagamento para split (Cashback, Pontos, Dinheiro, Pix, etc.).</summary>
    public string? SecondPaymentMethod { get; set; }

    /// <summary>Valor pago pelo segundo método em centavos. Zero = sem split.</summary>
    public int SecondPaymentAmountInCents { get; set; } = 0;

    /// <summary>
    /// Quando PaymentMethod == Crediario, este campo indica se a dívida deve ser
    /// adicionada a um crediário aberto existente (fornece o Id do crediário)
    /// ou se deve criar uma nova conta (null = nova conta com prazo próprio de 30 dias).
    /// </summary>
    public Guid? CrediarioExistenteId { get; set; }

    /// <summary>Desconto administrativo em centavos aplicado no fechamento (opcional).</summary>
    [Range(0, int.MaxValue)]
    public int DiscountInCents { get; set; } = 0;

    /// <summary>
    /// Se true, emite a NFC-e desta venda automaticamente. O admin decide isso explicitamente
    /// no momento do fechamento (Maikon não quer nota emitida sem antes perguntar) — o valor
    /// vem pré-marcado no front conforme a forma de pagamento estar em FiscalConfig.FormasPagamentoAutoEmissao,
    /// mas é sempre sobrescrevível.
    /// </summary>
    public bool EmitirNotaFiscal { get; set; } = false;
}

public class ComandaItemDto
{
    public Guid    Id                  { get; set; }
    public Guid?   ProductId           { get; set; }
    public string  ItemNameSnapshot    { get; set; } = string.Empty;
    public int     Quantity            { get; set; }
    public int     UnitPriceInCents    { get; set; }
    public decimal UnitPriceInReais    { get; set; }
    public decimal SubtotalInReais     { get; set; }
    public DateTime AddedAt            { get; set; }
}

// -------------------------------------------------------------------------
// Edição de comanda fechada (Admin only)
// -------------------------------------------------------------------------

/// <summary>Request para editar uma comanda já fechada.</summary>
public class EditarComandaRequest
{
    public string? PaymentMethod               { get; set; }
    public string? SecondPaymentMethod         { get; set; }
    public int?    SecondPaymentAmountInCents  { get; set; }
    public Guid?   NovoClienteId              { get; set; }
    public int?    DescontoEmCentavos          { get; set; }
    public string? Notes                       { get; set; }
    public List<EditarItemRequest>? Itens      { get; set; }
}

public class EditarItemRequest
{
    /// <summary>Id do item existente. Null = novo item.</summary>
    public Guid?  ComandaItemId    { get; set; }

    /// <summary>True = remover o item (devolve estoque).</summary>
    public bool   Remover          { get; set; } = false;

    public Guid?  ProductId        { get; set; }
    public string ItemName         { get; set; } = string.Empty;
    public int    UnitPriceInCents { get; set; }
    public int    Quantity         { get; set; } = 1;
}

// -------------------------------------------------------------------------
// Resultado paginado genérico
// -------------------------------------------------------------------------

/// <summary>Wrapper genérico para resultados paginados da API.</summary>
public class PagedResult<T>
{
    public List<T> Items        { get; set; } = new();
    public int     TotalCount   { get; set; }
    public int     Page         { get; set; }
    public int     PageSize     { get; set; }
    public int     TotalPages   => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool    HasNext      => Page < TotalPages;
    public bool    HasPrev      => Page > 1;
    public string? ErrorMessage { get; set; }
}

