using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("push_subscriptions")]
public class PushSubscription
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User   { get; set; } = null!;

    [MaxLength(600)] public string Endpoint { get; set; } = string.Empty;
    [MaxLength(300)] public string P256dh   { get; set; } = string.Empty;
    [MaxLength(150)] public string Auth     { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
