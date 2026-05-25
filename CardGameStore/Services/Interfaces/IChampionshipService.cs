// =============================================================================
// IChampionshipService.cs — Interface do serviço de Campeonatos
// =============================================================================

using CardGameStore.Models.PostgreSQL;

namespace CardGameStore.Services.Interfaces;

/// <summary>Contrato para gestão de torneios e participantes.</summary>
public interface IChampionshipService
{
    Task<Championship>              CreateAsync(Championship championship);
    Task<Championship?>             GetByIdAsync(Guid id);
    Task<IEnumerable<Championship>> GetUpcomingAsync();
    Task<Championship>              UpdateStatusAsync(Guid id, ChampionshipStatus newStatus);

    Task<ChampionshipParticipant>   RegisterParticipantAsync(Guid championshipId, Guid userId, string? deckName = null);
    Task                            LinkComandaToParticipantAsync(Guid participantId, Guid comandaId);
    Task<IEnumerable<ChampionshipParticipant>> GetParticipantsAsync(Guid championshipId);
    Task                            SetPlacementAsync(Guid participantId, int placement);
    Task                            RemoveParticipantAsync(Guid participantId);
}
