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
    /// <summary>ID do produto no estoque (nullable se for carta TCG).</summary>
    public Guid? ProductId { get; set; }

    /// <summary>ID da carta no cache MongoDB (nullable se for produto físico).</summary>
    public string? CardCacheId { get; set; }

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
    public DateTime          OpenedAt        { get; set; }
    public DateTime?         ClosedAt        { get; set; }
    public string?           PaymentMethod   { get; set; }
    public List<ComandaItemDto> Items        { get; set; } = new();
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
    /// Forma de pagamento. Valores aceitos:
    /// Dinheiro | Pix | CartaoCredito | CartaoDebito | Crediario
    /// </summary>
    [Required]
    public string PaymentMethod { get; set; } = "Dinheiro";

    /// <summary>Observação opcional (ex: "troco de R$50,00").</summary>
    public string? Observacao { get; set; }
}

public class ComandaItemDto
{
    public Guid    Id                  { get; set; }
    public string  ItemNameSnapshot    { get; set; } = string.Empty;
    public int     Quantity            { get; set; }
    public decimal UnitPriceInReais    { get; set; }
    public decimal SubtotalInReais     { get; set; }
    public DateTime AddedAt            { get; set; }
}

// -------------------------------------------------------------------------
// Resultado paginado genérico
// -------------------------------------------------------------------------

/// <summary>Wrapper genérico para resultados paginados da API.</summary>
public class PagedResult<T>
{
    public List<T> Items      { get; set; } = new();
    public int     TotalCount { get; set; }
    public int     Page       { get; set; }
    public int     PageSize   { get; set; }
    public int     TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool    HasNext    => Page < TotalPages;
    public bool    HasPrev    => Page > 1;
}

// -------------------------------------------------------------------------
// DTOs da API TCG externa (mapeiam a resposta do apitcg.com)
// Adapte os campos conforme a documentação real da API
// -------------------------------------------------------------------------

/// <summary>Resposta de uma carta individual da API TCG.</summary>
public class TcgApiCardResponse
{
    public string        Id       { get; set; } = string.Empty;
    public string        Name     { get; set; } = string.Empty;
    public string        Game     { get; set; } = string.Empty;
    public string?       SetName  { get; set; }
    public string?       SetCode  { get; set; }
    public string?       Number   { get; set; }
    public string?       Rarity   { get; set; }
    public string?       Type     { get; set; }
    public List<string>? Subtypes { get; set; }
    public TcgCardImages? Images  { get; set; }
    public TcgCardPricesApi? Prices { get; set; }
}

public class TcgCardImages
{
    public string? Small { get; set; }
    public string? Large { get; set; }
}

public class TcgCardPricesApi
{
    public decimal? Low       { get; set; }
    public decimal? Mid       { get; set; }
    public decimal? High      { get; set; }
    public decimal? Market    { get; set; }
    public decimal? DirectLow { get; set; }
}

/// <summary>Resposta de busca paginada da API TCG.</summary>
public class TcgApiSearchResponse
{
    public List<TcgApiCardResponse> Cards      { get; set; } = new();
    public int                      TotalCount { get; set; }
    public int                      Page       { get; set; }
    public int                      PageSize   { get; set; }
}

/// <summary>Set/Expansão de um jogo TCG.</summary>
public class TcgSetDto
{
    public string  Code        { get; set; } = string.Empty;
    public string  Name        { get; set; } = string.Empty;
    public string  Game        { get; set; } = string.Empty;
    public string? Series      { get; set; }
    public string? LogoUrl     { get; set; }
    public int     TotalCards  { get; set; }
    public DateTime? ReleaseDate { get; set; }
}
