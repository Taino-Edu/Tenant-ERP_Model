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

/// <summary>Analytics de uso detalhado de um tenant específico — quais telas
/// do admin foram acessadas e por quanto tempo, num período. Contraponto ao
/// sinal barato de LastActivityAt do overview.</summary>
public class TenantUsageDto
{
    public double TotalHoras { get; set; }
    public int UsuariosAtivos { get; set; }
    public List<TenantUsagePathDto> TopPaths { get; set; } = new();
}

public class TenantUsagePathDto
{
    public string Path { get; set; } = string.Empty;
    public double Horas { get; set; }
    public int Visitas { get; set; }
}

/// <summary>Funcionário/admin de um tenant, visto pelo dono da plataforma —
/// nunca inclui hash de senha nem tokens.</summary>
public class TenantStaffDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? PerfilNome { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Cliente de um tenant, visto pelo dono da plataforma.</summary>
public class TenantCustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? WhatsApp { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Registro de auditoria agregado no feed cross-tenant — mesmos campos
/// de <see cref="AuditLogDto"/> mais o slug do tenant de origem.</summary>
public class PlatformAuditLogDto
{
    public string   Id            { get; set; } = string.Empty;
    public string   TenantSlug    { get; set; } = string.Empty;
    public string?  ActorUserId   { get; set; }
    public string?  ActorUserName { get; set; }
    public string   Action        { get; set; } = string.Empty;
    public string   EntityType    { get; set; } = string.Empty;
    public string?  EntityId      { get; set; }
    public string?  Details       { get; set; }
    public string?  TargetUserId  { get; set; }
    public string?  Channel       { get; set; }
    public string   Severity      { get; set; } = string.Empty;
    public string?  TraceId       { get; set; }
    public DateTime CreatedAt     { get; set; }
}
