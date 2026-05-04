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
}
