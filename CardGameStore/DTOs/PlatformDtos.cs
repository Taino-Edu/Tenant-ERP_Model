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

public class PlatformOverviewDto
{
    public int ActiveTenants { get; set; }
    public int SuspendedTenants { get; set; }

    /// <summary>
    /// Receita agregada de todos os tenants ativos, em centavos, somando os
    /// fechamentos "Dia" do mês corrente — ou seja, é "mês até ontem": o dia
    /// de hoje só fecha essa noite (FechamentoBackgroundService), então não
    /// entra ainda. Não usar o fechamento "Mes" aqui — esse só é gravado no
    /// dia 1 do mês seguinte, o que faria esse número mostrar o mês passado
    /// no meio do mês corrente.
    /// </summary>
    public long ReceitaMesAtualCents { get; set; }

    public Dictionary<string, int> PaymentStatusCounts { get; set; } = new();
    public Dictionary<string, int> ModuleAdoptionCounts { get; set; } = new();
    public List<TenantActivityDto> Tenants { get; set; } = new();
}

public class TenantActivityDto
{
    public Guid TenantId { get; set; }
    public long ReceitaMesAtualCents { get; set; }
    public DateTime? LastActivityAt { get; set; }
}
