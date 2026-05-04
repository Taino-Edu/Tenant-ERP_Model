// =============================================================================
// AuthDtos.cs — DTOs de Autenticação
// Separa os dados de entrada/saída da API dos Models internos.
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

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
    [Required, MaxLength(11)]   string Cpf,       // Apenas dígitos
    [Required, MaxLength(20)]   string WhatsApp,  // Formato: 5511999999999
    [MaxLength(50)]             string? TableIdentifier = null // Mesa do QR Code
);

/// <summary>Renovação de token usando o Refresh Token.</summary>
public record RefreshTokenRequest(
    [Required] string RefreshToken
);

// -------------------------------------------------------------------------
// Responses (saída)
// -------------------------------------------------------------------------

/// <summary>Resposta de autenticação bem-sucedida.</summary>
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
