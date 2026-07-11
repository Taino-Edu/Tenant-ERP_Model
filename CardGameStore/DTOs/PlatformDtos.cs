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
    public string PlanName { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string[] EnabledModules { get; set; } = Array.Empty<string>();
}

public class UpdateTenantStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class UpdateTenantBillingRequest
{
    [Required, MaxLength(63)]
    public string PlanName { get; set; } = string.Empty;

    [Required]
    public string PaymentStatus { get; set; } = string.Empty;

    public string[] EnabledModules { get; set; } = Array.Empty<string>();
}
