using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("notifications")]
public class Notification
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required, MaxLength(120)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [MaxLength(300)]
    [Column("link")]
    public string? Link { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    [NotMapped]
    public bool IsRead => ReadAt.HasValue;
}
