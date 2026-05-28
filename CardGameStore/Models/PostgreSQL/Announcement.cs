// =============================================================================
// Announcement.cs — Anúncios e banners gerenciados pelo Admin
//
// Tipos:
//   Banner  → imagem larga exibida no topo da landing page (1200×400px recomendado)
//   Aviso   → card de texto com título e descrição (sem imagem obrigatória)
//   Destaque→ produto/evento específico em evidência
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CardGameStore.Models.PostgreSQL;

[Table("announcements")]
public class Announcement
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    [Column("body")]
    public string? Body { get; set; }

    /// <summary>URL da imagem (CDN ou caminho relativo). Obrigatório para tipo Banner.</summary>
    [MaxLength(500)]
    [Column("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>URL de destino ao clicar no banner/aviso (opcional).</summary>
    [MaxLength(500)]
    [Column("link_url")]
    public string? LinkUrl { get; set; }

    [Column("type")]
    public AnnouncementType Type { get; set; } = AnnouncementType.Aviso;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Quando null, o anúncio não expira automaticamente.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_by_admin_id")]
    public Guid CreatedByAdminId { get; set; }

    /// <summary>True se estiver ativo e dentro do prazo de validade.</summary>
    [NotMapped]
    public bool IsVisible => IsActive && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnnouncementType
{
    Banner,   // imagem larga no topo da landing
    Aviso,    // card de texto (novidade, aviso de fechamento, etc.)
    Destaque, // produto ou evento em evidência
}
