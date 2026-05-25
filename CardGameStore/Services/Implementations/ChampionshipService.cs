// =============================================================================
// ChampionshipService.cs — Implementação de Campeonatos
// =============================================================================
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class ChampionshipService : IChampionshipService
{
    private readonly AppDbContext _db;
    public ChampionshipService(AppDbContext db) { _db = db; }

    public async Task<Championship> CreateAsync(Championship championship)
    {
        _db.Championships.Add(championship);
        await _db.SaveChangesAsync();
        return championship;
    }

    public async Task<Championship?> GetByIdAsync(Guid id) =>
        await _db.Championships.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IEnumerable<Championship>> GetUpcomingAsync() =>
        await _db.Championships
            .Include(c => c.Participants)
            .Where(c => c.Status == ChampionshipStatus.Planejado || c.Status == ChampionshipStatus.Inscricoes)
            .OrderBy(c => c.StartDate).ToListAsync();

    public async Task<IEnumerable<Championship>> GetAllAsync(string? search = null)
    {
        var query = _db.Championships.Include(c => c.Participants).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.ToLower().Contains(search.ToLower()) ||
                                     c.Game.ToLower().Contains(search.ToLower()));
        return await query.OrderByDescending(c => c.StartDate).ToListAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var ch = await _db.Championships.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException("Campeonato não encontrado.");

        if (ch.Status != ChampionshipStatus.Finalizado && ch.Status != ChampionshipStatus.Cancelado)
            throw new InvalidOperationException("Só é possível excluir campeonatos Finalizados ou Cancelados.");

        // Remove participantes antes de remover o campeonato
        _db.ChampionshipParticipants.RemoveRange(ch.Participants);
        _db.Championships.Remove(ch);
        await _db.SaveChangesAsync();
    }

    public async Task<Championship> UpdateStatusAsync(Guid id, ChampionshipStatus newStatus)
    {
        var ch = await _db.Championships.FindAsync(id) ?? throw new InvalidOperationException("Campeonato não encontrado.");
        ch.Status = newStatus;
        await _db.SaveChangesAsync();
        return ch;
    }

    public async Task<ChampionshipParticipant> RegisterParticipantAsync(Guid championshipId, Guid userId, string? deckName = null)
    {
        // Verifica se o usuário já está inscrito para evitar duplicata
        var jaInscrito = await _db.ChampionshipParticipants
            .AnyAsync(p => p.ChampionshipId == championshipId && p.UserId == userId);
        if (jaInscrito)
            throw new InvalidOperationException("Usuário já está inscrito neste campeonato.");

        // Gera número sequencial de jogador (último + 1)
        var ultimoNumero = await _db.ChampionshipParticipants
            .Where(p => p.ChampionshipId == championshipId)
            .Select(p => (int?)p.PlayerNumber)
            .MaxAsync();

        var participant = new ChampionshipParticipant
        {
            ChampionshipId = championshipId,
            UserId         = userId,
            DeckName       = deckName,
            PlayerNumber   = (ultimoNumero ?? 0) + 1
        };
        _db.ChampionshipParticipants.Add(participant);
        await _db.SaveChangesAsync();
        return participant;
    }

    public async Task LinkComandaToParticipantAsync(Guid participantId, Guid comandaId)
    {
        var p = await _db.ChampionshipParticipants.FindAsync(participantId);
        if (p != null) { p.ComandaId = comandaId; await _db.SaveChangesAsync(); }
    }

    public async Task<IEnumerable<ChampionshipParticipant>> GetParticipantsAsync(Guid championshipId) =>
        await _db.ChampionshipParticipants
            .Include(p => p.User)
            .Where(p => p.ChampionshipId == championshipId)
            .OrderBy(p => p.PlayerNumber).ToListAsync();

    public async Task SetPlacementAsync(Guid participantId, int placement)
    {
        var p = await _db.ChampionshipParticipants.FindAsync(participantId);
        if (p != null) { p.Placement = placement; await _db.SaveChangesAsync(); }
    }

    public async Task RemoveParticipantAsync(Guid participantId)
    {
        var p = await _db.ChampionshipParticipants.FindAsync(participantId)
            ?? throw new InvalidOperationException("Participante não encontrado.");
        _db.ChampionshipParticipants.Remove(p);
        await _db.SaveChangesAsync();
    }
}
