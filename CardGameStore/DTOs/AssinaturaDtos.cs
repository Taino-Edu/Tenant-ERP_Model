// =============================================================================
// AssinaturaDtos.cs — O que a LOJA vê da própria assinatura.
//
// Espelho reduzido do PlatformBillingDtos: lá é a plataforma olhando todas as
// lojas; aqui é uma loja olhando só a si mesma. Nada de custo, margem ou
// comparação com outros tenants passa por estes objetos.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using CardGameStore.Validation;

namespace CardGameStore.DTOs;

public class AssinaturaDto
{
    public string Plano { get; set; } = "";

    public decimal Mensalidade { get; set; }

    /// <summary>"Ativa" ou "Suspensa" — o mesmo estado que decide se a loja abre.</summary>
    public string Situacao { get; set; } = "";

    /// <summary>"Pago", "Atrasado" ou "Isento".</summary>
    public string StatusPagamento { get; set; } = "";

    public string? Cnpj { get; set; }

    public string? EmailDeFaturamento { get; set; }

    /// <summary>False enquanto faltar CNPJ ou e-mail. É o que o frontend usa pra
    /// mostrar o aviso — sem esses dois a cobrança não chega a ser emitida e a
    /// loja descobre isso do jeito ruim, vencendo sem nunca ter recebido boleto.</summary>
    public bool DadosCompletos { get; set; }

    public List<FaturaDto> Faturas { get; set; } = new();
}

public class FaturaDto
{
    public Guid Id { get; set; }

    /// <summary>"Mensalidade" ou "Implantacao".</summary>
    public string Tipo { get; set; } = "";

    public decimal Valor { get; set; }

    public DateTime Competencia { get; set; }

    public DateTime Vencimento { get; set; }

    public DateTime? PagoEm { get; set; }

    public bool Vencida { get; set; }

    /// <summary>Link de pagamento do gateway. Null quando a cobrança ainda não
    /// foi emitida (dados de faturamento incompletos, ou o job ainda não rodou).</summary>
    public string? LinkDePagamento { get; set; }
}

public class AtualizarFaturamentoRequest
{
    /// <summary>CPF ou CNPJ de quem paga. Validado aqui porque o gateway recusa
    /// documento inválido com um erro genérico, dias depois, dentro de um job.</summary>
    [Required(ErrorMessage = "Informe o CPF ou CNPJ de faturamento.")]
    [CnpjOuCpfValid]
    public string Documento { get; set; } = "";

    [Required(ErrorMessage = "Informe o e-mail de faturamento.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [MaxLength(200)]
    public string Email { get; set; } = "";
}

/// <summary>Aceita CPF ou CNPJ — MEI e autônomo costumam faturar no CPF.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class CnpjOuCpfValidAttribute : ValidationAttribute
{
    public CnpjOuCpfValidAttribute() : base("CPF ou CNPJ inválido.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string documento || string.IsNullOrWhiteSpace(documento))
            return ValidationResult.Success;

        return CnpjValidAttribute.ValidarCpfOuCnpj(documento)
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage ?? "CPF ou CNPJ inválido.");
    }
}
