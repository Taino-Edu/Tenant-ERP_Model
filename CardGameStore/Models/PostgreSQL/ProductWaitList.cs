using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("product_waitlist")]
public class ProductWaitList
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [ForeignKey("ProductId")]
    public Product? Product { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [Column("name"), MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Column("whatsapp"), MaxLength(20)]
    public string WhatsApp { get; set; } = string.Empty;

    [Column("position")]
    public int Position { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("notified_at")]
    public DateTime? NotifiedAt { get; set; }
}
