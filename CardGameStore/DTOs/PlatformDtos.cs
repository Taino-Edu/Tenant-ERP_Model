// =============================================================================
// PlatformDtos.cs — DTOs do painel do dono da plataforma (gestão de tenants).
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public class CreateTenantRequest
{
    [Required, MaxLength(20)]
    public string Slug { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string AdminEmail { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string AdminPassword { get; set; } = string.Empty;
}

public class TenantSummaryDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class UpdateTenantStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}
