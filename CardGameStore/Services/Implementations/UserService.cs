// =============================================================================
// UserService.cs — Implementação do serviço de usuários e pontos
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class UserService : IUserService
{
    private readonly AppDbContext          _db;
    private readonly ILogger<UserService>  _logger;

    public UserService(AppDbContext db, ILogger<UserService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<IEnumerable<UserSummaryDto>> GetAllAsync(string? search = null)
    {
        var query = _db.Users
            .Where(u => u.IsActive && u.Role == UserRole.Customer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(u =>
                u.Name.ToLower().Contains(search) ||
                (u.Cpf != null && u.Cpf.Contains(search)) ||
                (u.WhatsApp != null && u.WhatsApp.Contains(search)));
        }

        var users = await query
            .OrderBy(u => u.Name)
            .ToListAsync();

        return users.Select(MapToSummary);
    }

    public async Task<UserSummaryDto?> GetByIdAsync(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        return user == null ? null : MapToSummary(user);
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return null;

        return new UserProfileDto
        {
            Id              = user.Id,
            Name            = user.Name,
            Email           = user.Email,
            Cpf             = user.Cpf,
            WhatsApp        = user.WhatsApp,
            Role            = user.Role,
            PointsBalance   = GetEffectivePoints(user),
            PointsExpiresAt = user.PointsExpiresAt,
            PointsExpired   = IsExpired(user),
            CreatedAt       = user.CreatedAt,
        };
    }

    public async Task<UserSummaryDto> AddPointsAsync(Guid userId, AddPointsRequest request, Guid adminId)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        // Expira pontos antigos se já vencidos antes de somar
        if (IsExpired(user))
            user.PointsBalance = 0;

        user.PointsBalance   += request.Points;
        user.PointsExpiresAt  = DateTime.UtcNow.AddDays(30);
        user.UpdatedAt        = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} adicionou {Points} pontos ao usuário {UserId} ({Name}). Motivo: {Reason}",
            adminId, request.Points, userId, user.Name, request.Reason ?? "não informado");

        return MapToSummary(user);
    }

    public async Task DeductPointsAsync(Guid userId, int points)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        if (IsExpired(user))
            throw new InvalidOperationException("Os pontos deste usuário estão expirados.");

        if (user.PointsBalance < points)
            throw new InvalidOperationException(
                $"Saldo insuficiente. Disponível: {user.PointsBalance} pontos, solicitado: {points}.");

        user.PointsBalance -= points;
        user.UpdatedAt      = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsExpired(User user) =>
        user.PointsExpiresAt.HasValue && user.PointsExpiresAt.Value < DateTime.UtcNow;

    private static int GetEffectivePoints(User user) =>
        IsExpired(user) ? 0 : user.PointsBalance;

    private static UserSummaryDto MapToSummary(User user) => new()
    {
        Id              = user.Id,
        Name            = user.Name,
        Email           = user.Email,
        Cpf             = user.Cpf,
        WhatsApp        = user.WhatsApp,
        Role            = user.Role,
        PointsBalance   = IsExpired(user) ? 0 : user.PointsBalance,
        PointsExpiresAt = user.PointsExpiresAt,
        PointsExpired   = IsExpired(user),
        IsActive        = user.IsActive,
        CreatedAt       = user.CreatedAt,
    };
}
