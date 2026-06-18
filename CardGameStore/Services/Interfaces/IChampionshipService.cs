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
    Task<Championship>              UpdateAsync(Championship championship);
    Task<IEnumerable<Championship>> GetUpcomingAsync();
    Task<IEnumerable<Championship>> GetAllAsync(string? search = null);
    Task<Championship>              UpdateStatusAsync(Guid id, ChampionshipStatus newStatus);
    Task                            DeleteAsync(Guid id);

    Task<ChampionshipParticipant>   RegisterParticipantAsync(Guid championshipId, Guid userId, string? deckName = null);
    Task                            LinkComandaToParticipantAsync(Guid participantId, Guid comandaId);
    Task<IEnumerable<ChampionshipParticipant>> GetParticipantsAsync(Guid championshipId);
    Task<IEnumerable<ChampionshipParticipant>> GetUserParticipationsAsync(Guid userId);
    Task                            SetPlacementAsync(Guid participantId, int placement);
    Task                            RemoveParticipantAsync(Guid participantId);

    Task<ChampionshipPreInscricao>              AddPreInscricaoAsync(Guid championshipId, string nome, string whatsApp);
    Task<IEnumerable<ChampionshipPreInscricao>> GetPreInscricoesAsync(Guid championshipId);
    Task                                        DeletePreInscricaoAsync(Guid preInscricaoId);
    Task                                        SetPodioAsync(Guid championshipId, string podioJson);
}
