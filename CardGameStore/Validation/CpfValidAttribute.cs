// =============================================================================
// CpfValidAttribute.cs — Validação de CPF com algoritmo de dígito verificador
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.Validation;

/// <summary>
/// Valida o CPF usando o algoritmo oficial de dígitos verificadores (Módulo 11).
/// Rejeita: formato errado, sequências repetidas (000...000, 111...111 etc.)
/// e qualquer número que não passe nos dois dígitos verificadores.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class CpfValidAttribute : ValidationAttribute
{
    public CpfValidAttribute() : base("CPF inválido.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string cpf || string.IsNullOrWhiteSpace(cpf))
            return ValidationResult.Success; // deixa [Required] cuidar do vazio

        var limpo = cpf.Trim().Replace(".", "").Replace("-", "");

        if (!ValidarCpf(limpo))
            return new ValidationResult(ErrorMessage ?? "CPF inválido.");

        return ValidationResult.Success;
    }

    public static bool ValidarCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11)
            return false;

        if (!cpf.All(char.IsAsciiDigit))
            return false;

        if (cpf.Distinct().Count() == 1)
            return false;

        int soma = 0;
        for (int i = 0; i < 9; i++)
            soma += (cpf[i] - '0') * (10 - i);

        int d1 = 11 - (soma % 11);
        if (d1 >= 10) d1 = 0;

        if (cpf[9] - '0' != d1)
            return false;

        soma = 0;
        for (int i = 0; i < 10; i++)
            soma += (cpf[i] - '0') * (11 - i);

        int d2 = 11 - (soma % 11);
        if (d2 >= 10) d2 = 0;

        return cpf[10] - '0' == d2;
    }
}
