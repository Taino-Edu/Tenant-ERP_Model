// =============================================================================
// User.cs — Entidade de Usuário (PostgreSQL)
// Suporta dois perfis: Admin (Maikon) e Customer (clientes da loja)
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Representa um usuário do sistema.
/// Admin = dono da loja (Maikon), com acesso total ao painel.
/// Customer = cliente que entra via QR Code com login simplificado.
/// </summary>
[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Dados básicos — preenchidos por todos os usuários
    // -------------------------------------------------------------------------

    /// <summary>Nome completo do usuário.</summary>
    [Required, MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// E-mail: obrigatório para Admin e para registro em Campeonatos.
    /// Para o login rápido de Customer, pode ser nulo.
    /// </summary>
    [MaxLength(255)]
    [Column("email")]
    public string? Email { get; set; }

    /// <summary>Senha com hash BCrypt. Nula para clientes de login rápido.</summary>
    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    // -------------------------------------------------------------------------
    // Dados do cliente (login rápido via QR Code)
    // -------------------------------------------------------------------------

    /// <summary>WhatsApp do cliente (formato: 5511999999999).</summary>
    [MaxLength(20)]
    [Column("whatsapp")]
    public string? WhatsApp { get; set; }

    /// <summary>CPF do cliente (armazenado sem formatação: 11 dígitos).</summary>
    [MaxLength(11)]
    [Column("cpf")]
    public string? Cpf { get; set; }

    // -------------------------------------------------------------------------
    // Controle de acesso
    // -------------------------------------------------------------------------

    /// <summary>
    /// Perfil RBAC. Valores válidos: "Admin" | "Customer"
    /// Use a enum UserRole para evitar strings mágicas no código.
    /// </summary>
    [Required, MaxLength(20)]
    [Column("role")]
    public string Role { get; set; } = UserRole.Customer;

    // -------------------------------------------------------------------------
    // "Lembre-se de mim" — Refresh Token
    // -------------------------------------------------------------------------

    /// <summary>Token opaco usado para renovar o JWT sem novo login.</summary>
    [Column("refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>Data de expiração do refresh token.</summary>
    [Column("refresh_token_expiry")]
    public DateTime? RefreshTokenExpiry { get; set; }

    // -------------------------------------------------------------------------
    // Auditoria
    // -------------------------------------------------------------------------

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    // -------------------------------------------------------------------------
    // Navegação (relacionamentos)
    // -------------------------------------------------------------------------

    /// <summary>Comandas abertas ou históricas deste usuário.</summary>
    public ICollection<Comanda> Comandas { get; set; } = new List<Comanda>();

    /// <summary>Participações em campeonatos.</summary>
    public ICollection<ChampionshipParticipant> ChampionshipParticipants { get; set; } = new List<ChampionshipParticipant>();
}

/// <summary>
/// Constantes de perfil para evitar strings mágicas no código.
/// Exemplo de uso: user.Role = UserRole.Admin
/// </summary>
public static class UserRole
{
    public const string Admin    = "Admin";
    public const string Customer = "Customer";
}
