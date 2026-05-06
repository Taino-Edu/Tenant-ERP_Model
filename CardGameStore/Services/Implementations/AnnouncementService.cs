using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class AnnouncementService : IAnnouncementService
{
    private readonly AppDbContext              _db;
    private readonly ILogger<AnnouncementService> _logger;

    public AnnouncementService(AppDbContext db, ILogger<AnnouncementService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<IEnumerable<AnnouncementDto>> GetVisibleAsync()
    {
        var now = DateTime.UtcNow;
        var list = await _db.Announcements
            .Where(a => a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return list.Select(Map);
    }

    public async Task<IEnumerable<AnnouncementDto>> GetAllAsync()
    {
        var list = await _db.Announcements
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return list.Select(Map);
    }

    public async Task<AnnouncementDto> CreateAsync(CreateAnnouncementRequest request, Guid adminId)
    {
        var ann = new Announcement
        {
            Title              = request.Title.Trim(),
            Body               = request.Body?.Trim(),
            ImageUrl           = request.ImageUrl?.Trim(),
            LinkUrl            = request.LinkUrl?.Trim(),
            Type               = request.Type,
            ExpiresAt          = request.ExpiresAt,
            CreatedByAdminId   = adminId,
        };
        _db.Announcements.Add(ann);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Anúncio '{Title}' criado por admin {AdminId}", ann.Title, adminId);
        return Map(ann);
    }

    public async Task<AnnouncementDto> UpdateAsync(Guid id, UpdateAnnouncementRequest request)
    {
        var ann = await _db.Announcements.FindAsync(id)
            ?? throw new InvalidOperationException("Anúncio não encontrado.");

        if (request.Title    != null) ann.Title    = request.Title.Trim();
        if (request.Body     != null) ann.Body     = request.Body.Trim();
        if (request.ImageUrl != null) ann.ImageUrl = request.ImageUrl.Trim();
        if (request.LinkUrl  != null) ann.LinkUrl  = request.LinkUrl.Trim();
        if (request.IsActive  .HasValue) ann.IsActive  = request.IsActive.Value;
        if (request.ExpiresAt .HasValue) ann.ExpiresAt = request.ExpiresAt;
        if (request.Type      .HasValue) ann.Type      = request.Type.Value;
        ann.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Map(ann);
    }

    public async Task DeleteAsync(Guid id)
    {
        var ann = await _db.Announcements.FindAsync(id)
            ?? throw new InvalidOperationException("Anúncio não encontrado.");
        _db.Announcements.Remove(ann);
        await _db.SaveChangesAsync();
    }

    private static AnnouncementDto Map(Announcement a) => new()
    {
        Id        = a.Id,
        Title     = a.Title,
        Body      = a.Body,
        ImageUrl  = a.ImageUrl,
        LinkUrl   = a.LinkUrl,
        Type      = a.Type.ToString(),
        IsActive  = a.IsActive,
        ExpiresAt = a.ExpiresAt,
        CreatedAt = a.CreatedAt,
    };
}
