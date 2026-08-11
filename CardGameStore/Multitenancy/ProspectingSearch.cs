using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

public enum ProspectingSearchStatus
{
    Completed,
    Partial,
    Failed,
}

[Table("prospecting_searches")]
public class ProspectingSearch
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100), Column("category")]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(100), Column("city")]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(220), Column("cache_key")]
    public string CacheKey { get; set; } = string.Empty;

    [Required, MaxLength(40), Column("source")]
    public string Source { get; set; } = "OpenStreetMap";

    [Column("status")]
    public ProspectingSearchStatus Status { get; set; } = ProspectingSearchStatus.Completed;

    [Column("result_count")]
    public int ResultCount { get; set; }

    [Column("south")]
    public double South { get; set; }

    [Column("west")]
    public double West { get; set; }

    [Column("north")]
    public double North { get; set; }

    [Column("east")]
    public double East { get; set; }

    [Column("osm_area_id")]
    public long? OsmAreaId { get; set; }

    [MaxLength(500), Column("warning")]
    public string? Warning { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("refreshed_at")]
    public DateTime RefreshedAt { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    public ICollection<ProspectCandidate> Candidates { get; set; } = [];
}

public enum ProspectCandidateStatus
{
    New,
    Selected,
    Discarded,
    Lead,
    Customer,
    Stale,
    Suppressed,
}

public enum ProspectEnrichmentStatus
{
    Pending,
    Updated,
    Failed,
}

[Table("prospect_candidates")]
public class ProspectCandidate
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("search_id")]
    public Guid SearchId { get; set; }

    [Required, MaxLength(40), Column("source")]
    public string Source { get; set; } = "OpenStreetMap";

    [Required, MaxLength(100), Column("source_id")]
    public string SourceId { get; set; } = string.Empty;

    [Required, MaxLength(150), Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500), Column("address")]
    public string? Address { get; set; }

    [MaxLength(30), Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(1000), Column("website")]
    public string? Website { get; set; }

    [MaxLength(20), Column("digital_presence")]
    public string DigitalPresence { get; set; } = "SemSite";

    [Column("opportunity_score")]
    public int OpportunityScore { get; set; }

    [MaxLength(60), Column("estimated_revenue_range")]
    public string EstimatedRevenueRange { get; set; } = string.Empty;

    [Column("status")]
    public ProspectCandidateStatus Status { get; set; } = ProspectCandidateStatus.New;

    [Column("lead_id")]
    public Guid? LeadId { get; set; }

    [Column("first_seen_at")]
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    [Column("last_seen_at")]
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    [Column("enrichment_status")]
    public ProspectEnrichmentStatus EnrichmentStatus { get; set; } = ProspectEnrichmentStatus.Pending;

    [Column("last_enriched_at")]
    public DateTime? LastEnrichedAt { get; set; }

    [MaxLength(80), Column("enrichment_source")]
    public string? EnrichmentSource { get; set; }

    [Range(0, 100), Column("enrichment_confidence")]
    public int? EnrichmentConfidence { get; set; }

    [MaxLength(2000), Column("suggested_approach")]
    public string? SuggestedApproach { get; set; }

    public ProspectingSearch Search { get; set; } = null!;
    public ICollection<ProspectObservation> Observations { get; set; } = [];
}

[Table("prospect_observations")]
public class ProspectObservation
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("candidate_id")]
    public Guid CandidateId { get; set; }

    [Required, MaxLength(60), Column("field_name")]
    public string FieldName { get; set; } = string.Empty;

    [MaxLength(2000), Column("previous_value")]
    public string? PreviousValue { get; set; }

    [MaxLength(2000), Column("observed_value")]
    public string? ObservedValue { get; set; }

    [Required, MaxLength(80), Column("source")]
    public string Source { get; set; } = string.Empty;

    [Range(0, 100), Column("confidence")]
    public int Confidence { get; set; }

    [Column("observed_at")]
    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;

    public ProspectCandidate Candidate { get; set; } = null!;
}
