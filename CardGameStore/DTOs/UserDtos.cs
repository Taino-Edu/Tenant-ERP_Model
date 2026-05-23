// =============================================================================
// UserDtos.cs — DTOs para gerenciamento de usuários e pontos
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

/// <summary>Retorno resumido de um usuário (para listagem admin).</summary>
public class UserSummaryDto
{
    public Guid     Id              { get; set; }
    public string   Name            { get; set; } = string.Empty;
    public string?  Email           { get; set; }
    public string?  Cpf             { get; set; }
    public string?  WhatsApp        { get; set; }
    public string   Role            { get; set; } = string.Empty;
    public int      PointsBalance   { get; set; }
    public DateTime? PointsExpiresAt { get; set; }
    public bool     PointsExpired   { get; set; }
    public int      BalanceInCents  { get; set; }
    public bool     IsActive        { get; set; }
    public DateTime CreatedAt       { get; set; }
}

/// <summary>Perfil completo do cliente logado (retornado em GET /api/user/me).</summary>
public class UserProfileDto
{
    public Guid      Id              { get; set; }
    public string    Name            { get; set; } = string.Empty;
    public string?   Email           { get; set; }
    public string?   Cpf             { get; set; }
    public string?   WhatsApp        { get; set; }
    public string    Role            { get; set; } = string.Empty;
    public int       PointsBalance   { get; set; }
    public DateTime? PointsExpiresAt { get; set; }
    public bool      PointsExpired   { get; set; }
    public int       BalanceInCents  { get; set; }
    public DateTime  CreatedAt       { get; set; }
}

/// <summary>Request para ajustar saldo monetário de um usuário (Admin).</summary>
public class AdjustBalanceRequest
{
    /// <summary>Valor em centavos. Positivo = crédito (recarga), negativo = débito (uso).</summary>
    [Required]
    public int AmountInCents { get; set; }

    [MaxLength(255)]
    public string? Reason { get; set; }
}

/// <summary>Request para adicionar pontos a um usuário (Admin).</summary>
public class AddPointsRequest
{
    [Required]
    [Range(1, 100000, ErrorMessage = "Quantidade de pontos deve ser entre 1 e 100.000.")]
    public int Points { get; set; }

    [MaxLength(255)]
    public string? Reason { get; set; }  // Motivo (opcional, ex: "Campeonato de Pokémon")
}

/// <summary>
/// Request para o titular corrigir seus próprios dados (LGPD — direito de retificação).
/// Todos os campos são opcionais: apenas os campos fornecidos são atualizados.
/// </summary>
public class UpdateMeRequest
{
    [MaxLength(150)]
    public string? Name { get; set; }

    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? WhatsApp { get; set; }
}
