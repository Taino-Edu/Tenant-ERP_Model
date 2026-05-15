// =============================================================================
// IUserService.cs — Interface do serviço de usuários e pontos
// =============================================================================

using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IUserService
{
    /// <summary>Lista todos os usuários ativos (Admin).</summary>
    Task<IEnumerable<UserSummaryDto>> GetAllAsync(string? search = null);

    /// <summary>Retorna um usuário pelo ID (Admin).</summary>
    Task<UserSummaryDto?> GetByIdAsync(Guid id);

    /// <summary>Perfil completo do usuário logado.</summary>
    Task<UserProfileDto?> GetProfileAsync(Guid userId);

    /// <summary>Adiciona pontos ao saldo de um usuário. Redefine a validade para +30 dias.</summary>
    Task<UserSummaryDto> AddPointsAsync(Guid userId, AddPointsRequest request, Guid adminId);

    /// <summary>Deduz pontos do saldo do usuário (usado ao resgatar na comanda).</summary>
    Task DeductPointsAsync(Guid userId, int points);

    // ── LGPD — Direitos do titular ────────────────────────────────────────────

    /// <summary>
    /// Permite ao titular corrigir seus próprios dados (nome, e-mail, WhatsApp).
    /// Direito de retificação conforme Art. 18, IV da LGPD.
    /// </summary>
    Task<UserProfileDto> UpdateMeAsync(Guid userId, UpdateMeRequest request);

    /// <summary>
    /// Anonimiza os dados do titular (exclusão lógica).
    /// Substitui dados pessoais por valores neutros em vez de deletar fisicamente
    /// o registro, preservando a integridade referencial das comandas e crediários.
    /// Direito de exclusão conforme Art. 18, VI da LGPD.
    /// </summary>
    Task AnonimizarAsync(Guid userId);
}
