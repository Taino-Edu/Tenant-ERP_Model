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

    // Sem [Required]: uma imagem sozinha (sem texto) é uma mensagem válida —
    // ver checagem "pelo menos um dos dois" no controller.
    public string Body { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }
}

public class CreateSupportMessageRequest
{
    // Sem [Required]: idem CreateSupportTicketRequest.
    public string Body { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }
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
    public string? ImageUrl { get; set; }
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
