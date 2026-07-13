// =============================================================================
// SupportDtos.cs — DTOs de chamado de suporte entre lojista e dono da
// plataforma (SupportController do lado do lojista, endpoints de suporte em
// PlatformController do lado do dono).
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public class CreateSupportTicketRequest
{
    [Required, MaxLength(150)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;
}

public class CreateSupportMessageRequest
{
    [Required]
    public string Body { get; set; } = string.Empty;
}

public class UpdateSupportTicketStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class SupportTicketMessageDto
{
    public Guid Id { get; set; }
    public string AuthorRole { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SupportTicketDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantSlug { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MessageCount { get; set; }
}

public class SupportTicketDetailDto : SupportTicketDto
{
    public List<SupportTicketMessageDto> Messages { get; set; } = new();
}
