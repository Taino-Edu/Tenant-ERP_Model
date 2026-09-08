using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("crediario_email_outbox")]
public sealed class CrediarioEmailOutbox
{
    [Key] public Guid Id { get; set; }
    [MaxLength(254)] public string ToEmail { get; set; } = "";
    [MaxLength(150)] public string ToName { get; set; } = "";
    public decimal Valor { get; set; }
    public DateTime Vencimento { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public int Attempts { get; set; }
    public DateTime? SentAt { get; set; }
}
