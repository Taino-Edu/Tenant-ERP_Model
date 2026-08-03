using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public sealed class PublicAssistantRequest
{
    [Required]
    [StringLength(500, MinimumLength = 2)]
    public string Message { get; set; } = string.Empty;
}

public sealed class PublicAssistantResponse
{
    public string Reply { get; set; } = string.Empty;
    public string MarketingWhatsappUrl { get; set; } = "https://wa.me/5517997455482";
}
