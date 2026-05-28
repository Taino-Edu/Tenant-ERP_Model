// =============================================================================
// AuthDtos.cs — DTOs de Autenticação
// Separa os dados de entrada/saída da API dos Models internos.
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

// -------------------------------------------------------------------------
// Validação de CPF (dígitos verificadores)
// -------------------------------------------------------------------------

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValidCpfAttribute : ValidationAttribute
{
    public ValidCpfAttribute() : base("CPF inválido.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        var cpf = (value as string)?.Trim() ?? string.Empty;
        if (cpf.Length != 11 || !cpf.All(char.IsDigit) || cpf.Distinct().Count() == 1)
            return new ValidationResult(ErrorMessage);

        static int Digit(string s, int len)
        {
            var sum = s.Take(len).Select((c, i) => (c - '0') * (len + 1 - i)).Sum();
            var rem = (sum * 10) % 11;
            return rem == 10 ? 0 : rem;
        }

        return Digit(cpf, 9)  == (cpf[9]  - '0') &&
               Digit(cpf, 10) == (cpf[10] - '0')
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}

// -------------------------------------------------------------------------
// Requests (entrada)
// -------------------------------------------------------------------------

/// <summary>Login completo: Admin e clientes de Campeonatos.</summary>
public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password
);

/// <summary>
/// Login Rápido via QR Code: apenas para Customers da comanda.
/// Não exige senha — validação por CPF + WhatsApp.
/// </summary>
public record QuickLoginRequest(
    [Required, MaxLength(150)]  string Name,
    [Required, ValidCpf]        string Cpf,       // Apenas dígitos, com verificação de dígito
    [Required, MaxLength(20)]   string WhatsApp,  // Formato: 5511999999999
    [MaxLength(50)]             string? TableIdentifier = null // Mesa do QR Code
);

/// <summary>Renovação de token usando o Refresh Token.</summary>
public record RefreshTokenRequest(
    [Required] string RefreshToken
);

/// <summary>Busca cliente por CPF — primeiro acesso pelo site.</summary>
public record CpfLookupRequest(
    [Required, ValidCpf] string Cpf
);

/// <summary>Ativa a conta de um cliente existente (CPF + email + senha).</summary>
public record SetupAccountRequest(
    [Required, ValidCpf]        string Cpf,
    [Required, EmailAddress]    string Email,
    [Required, MinLength(8)]    string Password
);

/// <summary>Login de cliente pelo site (email + senha).</summary>
public record ClientLoginRequest(
    [Required, EmailAddress]    string Email,
    [Required]                  string Password
);

/// <summary>Resposta da busca por CPF.</summary>
public record CpfLookupResponse(
    string Name,
    bool   HasPassword
);

/// <summary>Solicita envio de email para redefinição de senha.</summary>
public record ForgotPasswordRequest(
    [Required, EmailAddress] string Email
);

/// <summary>Redefine a senha usando o token recebido por email.</summary>
public record ResetPasswordRequest(
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword
);

// -------------------------------------------------------------------------
// Responses (saída)
// -------------------------------------------------------------------------

/// <summary>Resposta interna de autenticação — inclui tokens para uso nos cookies.</summary>
public record AuthResponse(
    string   AccessToken,
    string   RefreshToken,
    DateTime ExpiresAt,
    string   Role,
    string   UserName,
    Guid     UserId,
    /// <summary>
    /// ID da comanda ativa — preenchido apenas no quick-login (cliente via QR Code).
    /// Null no login completo do Admin.
    /// </summary>
    Guid?    ComandaId = null
);

/// <summary>
/// Resposta de auth enviada ao cliente via JSON — sem tokens.
/// Os tokens trafegam exclusivamente como cookies HttpOnly (proteção XSS).
/// </summary>
public record SafeAuthResponse(
    DateTime ExpiresAt,
    string   Role,
    string   UserName,
    Guid     UserId,
    Guid?    ComandaId = null
);
