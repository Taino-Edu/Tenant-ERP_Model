using System.ComponentModel.DataAnnotations;
// Alias necessário: as classes abaixo têm uma propriedade de instância "PaymentMethod",
// que sombreia o nome do tipo estático — sem o alias, "PaymentMethod.IsValid(...)" não compila.
using PaymentMethods = CardGameStore.Models.PostgreSQL.PaymentMethod;

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
    public string PaymentMethod { get; set; } = PaymentMethods.Pix;

    [Range(0, 100)]
    public int DiscountPercent { get; set; } = 0;

    /// <summary>Se preenchido, sobrepõe DiscountPercent — desconto direto em centavos.</summary>
    [Range(0, int.MaxValue)]
    public int? DiscountInCents { get; set; }

    [Required, MinLength(1)]
    public List<VendaAvulsaItemRequest> Items { get; set; } = new();

    public string? SecondPaymentMethod { get; set; }

    [Range(0, int.MaxValue)]
    public int SecondPaymentAmountInCents { get; set; } = 0;

    /// <summary>Se true, emite a NFC-e desta venda automaticamente. Ver CloseComandaRequest.EmitirNotaFiscal.</summary>
    public bool EmitirNotaFiscal { get; set; } = false;

    public bool IsPaymentMethodValid() =>
        PaymentMethods.IsValid(PaymentMethod) &&
        (SecondPaymentMethod == null || PaymentMethods.IsValid(SecondPaymentMethod));
}

public class VendaAvulsaItemRequest
{
    [Required]
    public Guid  ProductId { get; set; }

    /// <summary>Preenchido quando o produto tem grade (HasVariants=true). Obrigatório nesse caso.</summary>
    public Guid? VariantId { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; } = 1;
}

public class VendaAvulsaDto
{
    public Guid                Id                         { get; set; }
    public string?             ClientName                 { get; set; }
    public string              PaymentMethod              { get; set; } = string.Empty;
    public string?             SecondPaymentMethod        { get; set; }
    public int                 SecondPaymentAmountInCents { get; set; }
    public decimal             TotalInReais               { get; set; }
    public int                 TotalInCents               => (int)(TotalInReais * 100);
    public DateTime            SoldAt                     { get; set; }
    public string              SoldByAdminName            { get; set; } = string.Empty;
    public int                 DiscountPercent            { get; set; }
    public decimal             DiscountInReais            { get; set; }
    public DateTime?           CanceladoEm                { get; set; }
    public List<VendaAvulsaItemDto> Items                 { get; set; } = new();

    /// <summary>Preenchidos só quando o registro pediu emissão de NFC-e (EmitirNotaFiscal=true) —
    /// permite o front abrir o cupom automaticamente quando autoriza, ou avisar o motivo se não.</summary>
    public Guid?   NotaFiscalId             { get; set; }
    public string? NotaFiscalStatus         { get; set; }
    public string? NotaFiscalMotivoRejeicao { get; set; }
}

public class EditarPagamentoVendaAvulsaRequest
{
    [Required]
    public string PaymentMethod { get; set; } = PaymentMethods.Pix;

    public string? SecondPaymentMethod { get; set; }

    [Range(0, int.MaxValue)]
    public int SecondPaymentAmountInCents { get; set; } = 0;

    /// <summary>Nome do cliente (opcional). Null = mantém o atual.</summary>
    public string? ClientName { get; set; }

    /// <summary>True para limpar o nome do cliente.</summary>
    public bool ClearClientName { get; set; } = false;

    /// <summary>Desconto em centavos (opcional). Null = mantém o atual.</summary>
    [Range(0, int.MaxValue)]
    public int? DiscountInCents { get; set; }
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
