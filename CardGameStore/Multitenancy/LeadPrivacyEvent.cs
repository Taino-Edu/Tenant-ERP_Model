using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

[Table("lead_privacy_events")]
public sealed class LeadPrivacyEvent
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("lead_id")] public Guid LeadId { get; set; }
    public Lead Lead { get; set; } = null!;
    [Required, MaxLength(60), Column("event_type")] public string EventType { get; set; } = string.Empty;
    [Required, MaxLength(150), Column("actor_name")] public string ActorName { get; set; } = "Sistema";
    [Column("actor_user_id")] public Guid? ActorUserId { get; set; }
    [Column("details_json", TypeName = "text")] public string DetailsJson { get; set; } = "{}";
    [MaxLength(64), Column("previous_hash")] public string? PreviousHash { get; set; }
    [Required, MaxLength(64), Column("event_hash")] public string EventHash { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class LeadPrivacyAudit
{
    public static async Task<LeadPrivacyEvent> AppendAsync(CatalogDbContext db, Guid leadId,
        string eventType, object details, string actorName = "Sistema", Guid? actorUserId = null,
        CancellationToken ct = default)
    {
        var previousHash = await db.LeadPrivacyEvents.AsNoTracking()
            .Where(e => e.LeadId == leadId).OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Select(e => e.EventHash).FirstOrDefaultAsync(ct);
        var createdAt = DateTime.UtcNow;
        var detailsJson = JsonSerializer.Serialize(details);
        var material = $"{leadId:N}|{eventType}|{actorUserId:N}|{actorName}|{createdAt:O}|{detailsJson}|{previousHash}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
        var entry = new LeadPrivacyEvent
        {
            LeadId = leadId, EventType = eventType, ActorName = actorName,
            ActorUserId = actorUserId, DetailsJson = detailsJson,
            PreviousHash = previousHash, EventHash = hash, CreatedAt = createdAt,
        };
        db.LeadPrivacyEvents.Add(entry);
        return entry;
    }
}
