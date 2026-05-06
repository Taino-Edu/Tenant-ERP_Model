using System.ComponentModel.DataAnnotations;
using CardGameStore.Models.PostgreSQL;

namespace CardGameStore.DTOs;

public class AnnouncementDto
{
    public Guid             Id        { get; set; }
    public string           Title     { get; set; } = string.Empty;
    public string?          Body      { get; set; }
    public string?          ImageUrl  { get; set; }
    public string?          LinkUrl   { get; set; }
    public string           Type      { get; set; } = string.Empty;
    public bool             IsActive  { get; set; }
    public DateTime?        ExpiresAt { get; set; }
    public DateTime         CreatedAt { get; set; }
}

public class CreateAnnouncementRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Body { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(500)]
    public string? LinkUrl { get; set; }

    public AnnouncementType Type      { get; set; } = AnnouncementType.Aviso;
    public DateTime?        ExpiresAt { get; set; }
}

public class UpdateAnnouncementRequest
{
    [MaxLength(200)]
    public string? Title    { get; set; }

    [MaxLength(2000)]
    public string? Body     { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(500)]
    public string? LinkUrl  { get; set; }

    public bool?             IsActive  { get; set; }
    public DateTime?         ExpiresAt { get; set; }
    public AnnouncementType? Type      { get; set; }
}
