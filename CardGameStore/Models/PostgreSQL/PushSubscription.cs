using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("push_subscriptions")]
public class PushSubscription
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Column("endpoint")] [MaxLength(600)] public string Endpoint { get; set; } = string.Empty;
    [Column("p256dh")]   [MaxLength(300)] public string P256dh   { get; set; } = string.Empty;
    [Column("auth")]     [MaxLength(150)] public string Auth     { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
