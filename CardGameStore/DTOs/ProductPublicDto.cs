// =============================================================================
// ProductPublicDto.cs — Produto sem os campos internos de custo/reposição (M12).
//
// GetAll (anônimo) e GetAllStore (qualquer autenticado, inclusive Customer) não
// podem devolver a entidade Product inteira: CostPriceInCents e MinimumStock são
// dados internos de margem/reposição, não pra cliente nem concorrente verem.
// Endpoints Admin/Operator (GetAllAdmin, GetById interno etc.) continuam com a
// entidade completa.
// =============================================================================

using CardGameStore.Models.PostgreSQL;

namespace CardGameStore.DTOs;

public class ProductPublicDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public int PriceInCents { get; set; }
    public int StockQuantity { get; set; }
    public string? Ncm { get; set; }
    public string? ImageUrl { get; set; }
    public string[] ImageUrls { get; set; } = Array.Empty<string>();
    public string? FullDescription { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public bool ShowOnSite { get; set; }
    public bool ShowOnMarketplace { get; set; }
    public int? DiscountPriceInCents { get; set; }
    public bool IsPreVenda { get; set; }
    public bool HasVariants { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Espelham as propriedades computadas de Product (preço em reais/promoção) —
    // não são dado sensível, é o mesmo PriceInCents/DiscountPriceInCents já
    // acima só em outra unidade. Faltavam aqui desde a criação deste DTO (M12),
    // que só copiou os campos "brutos" e não os computados: o frontend
    // (lib/api.ts `Product`) sempre esperou PriceInReais/DiscountPriceInReais/
    // IsOnPromo prontos do backend, então toda página pública de produto
    // quebrava em fmt(undefined).toFixed() — achado testando localmente.
    // CostPriceInReais/MarginInReais/MarginPercent/IsLowStock continuam de
    // fora de propósito: esses sim revelam custo/margem/estoque mínimo.
    public decimal PriceInReais { get; set; }
    public decimal? DiscountPriceInReais { get; set; }
    public bool IsOnPromo { get; set; }

    public static ProductPublicDto FromEntity(Product p) => new()
    {
        Id                  = p.Id,
        Name                = p.Name,
        Description         = p.Description,
        Category            = p.Category,
        Barcode             = p.Barcode,
        PriceInCents        = p.PriceInCents,
        StockQuantity       = p.StockQuantity,
        Ncm                 = p.Ncm,
        ImageUrl            = p.ImageUrl,
        ImageUrls           = p.ImageUrls,
        FullDescription     = p.FullDescription,
        IsActive            = p.IsActive,
        IsFeatured          = p.IsFeatured,
        ShowOnSite          = p.ShowOnSite,
        ShowOnMarketplace   = p.ShowOnMarketplace,
        DiscountPriceInCents = p.DiscountPriceInCents,
        IsPreVenda          = p.IsPreVenda,
        HasVariants         = p.HasVariants,
        CreatedAt           = p.CreatedAt,
        UpdatedAt           = p.UpdatedAt,
        PriceInReais        = p.PriceInReais,
        DiscountPriceInReais = p.DiscountPriceInReais,
        IsOnPromo           = p.IsOnPromo,
    };
}
