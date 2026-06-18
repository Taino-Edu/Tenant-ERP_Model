using System.ComponentModel.DataAnnotations;
using CardGameStore.Models.MongoDB;

namespace CardGameStore.DTOs;

public class VendaAvulsaRequest
{
    [MaxLength(150)]
    public string? ClientName { get; set; }

    /// <summary>
    /// ID do cliente cadastrado. Obrigatório para Crediario, Pontos e Cashback.
    /// </summary>
    public Guid? UserId { get; set; }

    [Required]
    public string PaymentMethod { get; set; } = Models.MongoDB.PaymentMethod.Pix;

    [Range(0, 100)]
    public int DiscountPercent { get; set; } = 0;

    [Required, MinLength(1)]
    public List<VendaAvulsaItemRequest> Items { get; set; } = new();

    public bool IsPaymentMethodValid() => Models.MongoDB.PaymentMethod.IsValid(PaymentMethod);
}

public class VendaAvulsaItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; } = 1;
}

public class VendaAvulsaDto
{
    public string              Id              { get; set; } = string.Empty;
    public string?             ClientName      { get; set; }
    public string              PaymentMethod   { get; set; } = string.Empty;
    public decimal             TotalInReais    { get; set; }
    public int                 TotalInCents    => (int)(TotalInReais * 100);
    public DateTime            SoldAt          { get; set; }
    public string              SoldByAdminName { get; set; } = string.Empty;
    public int                 DiscountPercent { get; set; }
    public decimal             DiscountInReais { get; set; }
    public List<VendaAvulsaItemDto> Items      { get; set; } = new();
}

public class VendaAvulsaItemDto
{
    public string  ProductName      { get; set; } = string.Empty;
    public string? ProductCategory  { get; set; }
    public int     Quantity         { get; set; }
    public decimal UnitPriceInReais { get; set; }
    public decimal SubtotalInReais  { get; set; }
    public int     UnitCostInCents  { get; set; }
}
