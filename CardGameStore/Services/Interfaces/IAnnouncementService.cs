using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IAnnouncementService
{
    /// <summary>Retorna todos os anúncios visíveis (ativos + dentro do prazo). Público.</summary>
    Task<IEnumerable<AnnouncementDto>> GetVisibleAsync();

    /// <summary>Retorna todos os anúncios (ativos e inativos). Admin only.</summary>
    Task<IEnumerable<AnnouncementDto>> GetAllAsync();

    Task<AnnouncementDto> CreateAsync(CreateAnnouncementRequest request, Guid adminId);
    Task<AnnouncementDto> UpdateAsync(Guid id, UpdateAnnouncementRequest request);
    Task DeleteAsync(Guid id);
}
