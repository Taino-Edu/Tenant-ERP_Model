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
    public DateTime          OpenedAt        { get; set; }
    public List<ComandaItemDto> Items        { get; set; } = new();
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
// Venda Avulsa — venda direta no balcão sem QR Code/login de cliente
// -------------------------------------------------------------------------

/// <summary>
/// Requisição de venda avulsa feita pelo Admin diretamente no balcão.
/// Não requer login do cliente — cria e fecha a comanda atomicamente.
/// </summary>
public class VendaAvulsaRequest
{
    /// <summary>Nome do cliente (para registro). Opcional.</summary>
    [MaxLength(150)]
    public string? ClientName { get; set; }

    /// <summary>Lista de itens a vender.</summary>
    [Required, MinLength(1)]
    public List<VendaAvulsaItemRequest> Items { get; set; } = new();
}

/// <summary>Item de uma venda avulsa.</summary>
public class VendaAvulsaItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; } = 1;
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
