using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

public enum ProspectingCampaignStatus
{
    Active,
    Paused,
}

[Table("prospecting_campaigns")]
public class ProspectingCampaign
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(120), Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100), Column("category")]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(100), Column("city")]
    public string City { get; set; } = string.Empty;

    [Column("status")]
    public ProspectingCampaignStatus Status { get; set; } = ProspectingCampaignStatus.Active;

    [Range(6, 720), Column("interval_hours")]
    public int IntervalHours { get; set; } = 168;

    [Range(1, 1000), Column("max_candidates_per_run")]
    public int MaxCandidatesPerRun { get; set; } = 200;

    [Range(1, 24), Column("daily_run_budget")]
    public int DailyRunBudget { get; set; } = 1;

    [Range(1, 5), Column("max_retry_attempts")]
    public int MaxRetryAttempts { get; set; } = 3;

    [Column("next_run_at")]
    public DateTime NextRunAt { get; set; } = DateTime.UtcNow;

    [Column("last_run_at")]
    public DateTime? LastRunAt { get; set; }

    [MaxLength(500), Column("last_error")]
    public string? LastError { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProspectingCampaignRun> Runs { get; set; } = [];
}

public enum ProspectingCampaignRunStatus
{
    Queued,
    Running,
    Completed,
    Failed,
}

[Table("prospecting_campaign_runs")]
public class ProspectingCampaignRun
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("campaign_id")]
    public Guid CampaignId { get; set; }

    [Column("search_id")]
    public Guid? SearchId { get; set; }

    [Column("status")]
    public ProspectingCampaignRunStatus Status { get; set; } = ProspectingCampaignRunStatus.Queued;

    [Column("discovered_count")]
    public int DiscoveredCount { get; set; }

    [Column("new_count")]
    public int NewCount { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [MaxLength(500), Column("error")]
    public string? Error { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("attempt_count")]
    public int AttemptCount { get; set; }

    [Column("next_attempt_at")]
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    public ProspectingCampaign Campaign { get; set; } = null!;
}

public enum ProspectSuppressionKeyType
{
    SourceId,
    Phone,
    Domain,
}

[Table("prospect_suppressions")]
public class ProspectSuppression
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("key_type")]
    public ProspectSuppressionKeyType KeyType { get; set; }

    [Required, MaxLength(500), Column("normalized_value")]
    public string NormalizedValue { get; set; } = string.Empty;

    [Required, MaxLength(300), Column("reason")]
    public string Reason { get; set; } = "Solicitação de não prospecção";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
