// =============================================================================
// CnpjValidAttribute.cs — Validação de CNPJ com dígito verificador (Módulo 11)
//
// Irmão do CpfValidAttribute. Existe porque o gateway de cobrança valida o
// documento do lado dele e recusa a criação do cliente com um 400 genérico:
// sem validar aqui, o lojista salva um CNPJ errado, o formulário diz "salvo", e
// a mensalidade dele só falha dias depois, dentro de um job, num log que ele
// nunca vai ler. Validar na entrada transforma isso em erro de formulário.
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class CnpjValidAttribute : ValidationAttribute
{
    public CnpjValidAttribute() : base("CNPJ inválido.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string cnpj || string.IsNullOrWhiteSpace(cnpj))
            return ValidationResult.Success; // deixa [Required] cuidar do vazio

        return ValidarCnpj(SomenteDigitos(cnpj))
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage ?? "CNPJ inválido.");
    }

    /// <summary>Pesos oficiais do cálculo, aplicados da esquerda pra direita.
    /// O segundo dígito usa a mesma sequência com um 6 na frente.</summary>
    private static readonly int[] PesosPrimeiro = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosSegundo  = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public static bool ValidarCnpj(string cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj) || cnpj.Length != 14) return false;
        if (!cnpj.All(char.IsAsciiDigit)) return false;

        // 00.000.000/0000-00 e afins passam no módulo 11 mas não existem. É o
        // mesmo motivo pelo qual o CpfValidAttribute rejeita sequências iguais.
        if (cnpj.Distinct().Count() == 1) return false;

        return cnpj[12] - '0' == CalcularDigito(cnpj, PesosPrimeiro)
            && cnpj[13] - '0' == CalcularDigito(cnpj, PesosSegundo);
    }

    private static int CalcularDigito(string cnpj, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++)
            soma += (cnpj[i] - '0') * pesos[i];

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    /// <summary>Aceita CPF (11 dígitos) ou CNPJ (14). O gateway aceita os dois —
    /// lojista MEI ou autônomo costuma cobrar no CPF.</summary>
    public static bool ValidarCpfOuCnpj(string? documento)
    {
        var digitos = SomenteDigitos(documento ?? "");

        return digitos.Length switch
        {
            11 => CpfValidAttribute.ValidarCpf(digitos),
            14 => ValidarCnpj(digitos),
            _  => false,
        };
    }

    public static string SomenteDigitos(string valor) =>
        new(valor.Where(char.IsAsciiDigit).ToArray());
}
