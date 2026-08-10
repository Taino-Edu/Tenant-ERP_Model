// =============================================================================
// AiChatDtos.cs — DTOs do assistente IA
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public class AiChatRequest
{
    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;
}

public class AiChatResponse
{
    public string      Reply   { get; set; } = string.Empty;
    public bool        Success { get; set; } = true;
    public string?     Error   { get; set; }
    public AiAction?   Action  { get; set; }
}

public class AiAction
{
    public string  Type  { get; set; } = string.Empty; // "navigate" | "openWizard"
    public string? Route { get; set; }
}

/// <summary>Um evento SSE do chat em streaming — ou um pedaço de texto (Delta),
/// ou o evento final (Done=true) com a action já extraída/limpa dos marcadores.</summary>
public class AiStreamEvent
{
    public string?   Delta  { get; set; }
    public bool      Done   { get; set; }
    public AiAction? Action { get; set; }
}
