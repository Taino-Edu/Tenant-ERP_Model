// =============================================================================
// LgpdDtos.cs — Objetos de transferência para endpoints LGPD
// =============================================================================

using System.ComponentModel.DataAnnotations;
using CardGameStore.Validation;

namespace CardGameStore.DTOs;

// ── Entrada (solicitante) ─────────────────────────────────────────────────────

/// <summary>Payload para abertura de uma nova solicitação LGPD.</summary>
public class LgpdRequestCreate
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(200)]
    public string RequesterName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [MaxLength(255)]
    public string RequesterEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "O CPF é obrigatório.")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "CPF deve conter exatamente 11 dígitos numéricos.")]
    [CpfValid]
    public string RequesterCpf { get; set; } = string.Empty;

    /// <summary>
    /// Tipo da solicitação conforme Art. 18 LGPD.
    /// Valores aceitos: Acesso | Retificacao | Exclusao | Portabilidade | Oposicao
    /// </summary>
    [Required(ErrorMessage = "O tipo de solicitação é obrigatório.")]
    public string RequestType { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }
}

// ── Saída (para o solicitante) ────────────────────────────────────────────────

/// <summary>Retorno ao solicitante após abertura da solicitação.</summary>
public class LgpdRequestReceived
{
    public string   Protocol  { get; set; } = string.Empty;
    public DateTime Deadline  { get; set; }
    public string   Message   { get; set; } = string.Empty;
}

/// <summary>Dados da solicitação retornados ao consultar pelo protocolo.</summary>
public class LgpdRequestResponse
{
    public string    Id            { get; set; } = string.Empty;
    public string    RequestType   { get; set; } = string.Empty;
    public string    Status        { get; set; } = string.Empty;
    public string?   AdminResponse { get; set; }
    public DateTime  CreatedAt     { get; set; }
    public DateTime  Deadline      { get; set; }
    public DateTime? RespondedAt   { get; set; }
}

// ── Admin ─────────────────────────────────────────────────────────────────────

/// <summary>Payload para resposta do admin a uma solicitação LGPD.</summary>
public class LgpdAdminResponse
{
    /// <summary>Novo status: "EmAnalise" | "Concluido" | "Negado"</summary>
    [Required]
    public string Status { get; set; } = string.Empty;

    [Required(ErrorMessage = "A resposta é obrigatória.")]
    [MaxLength(4000)]
    public string AdminResponse { get; set; } = string.Empty;
}

/// <summary>Resumo de uma solicitação LGPD para listagem no painel admin.</summary>
public class LgpdRequestAdminDto
{
    public string    Id             { get; set; } = string.Empty;
    public string    RequesterName  { get; set; } = string.Empty;
    public string    RequesterEmail { get; set; } = string.Empty;
    public string    RequesterCpf   { get; set; } = string.Empty;
    public string    RequestType    { get; set; } = string.Empty;
    public string?   Description    { get; set; }
    public string    Status         { get; set; } = string.Empty;
    public string?   AdminResponse  { get; set; }
    public DateTime  CreatedAt      { get; set; }
    public DateTime  Deadline       { get; set; }
    public DateTime? RespondedAt    { get; set; }
    public bool      IsOverdue      { get; set; }
    public bool      IsUrgent       { get; set; }
    public bool      TemAnexo       { get; set; }
    public string?   AnexoNome      { get; set; }
}

// ── Audit Log ─────────────────────────────────────────────────────────────────

/// <summary>Entrada de audit log para listagem paginada no painel admin.</summary>
public class AuditLogDto
{
    public string   Id            { get; set; } = string.Empty;
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

/// <summary>Resposta paginada de audit logs.</summary>
public class AuditLogPagedResponse
{
    public IEnumerable<AuditLogDto> Items       { get; set; } = [];
    public int                      TotalCount  { get; set; }
    public int                      Page        { get; set; }
    public int                      PageSize    { get; set; }
    public int                      TotalPages  { get; set; }
}

// ── Cookie Consent ────────────────────────────────────────────────────────────

/// <summary>Payload para registro de consentimento de cookies.</summary>
public class CookieConsentCreate
{
    public bool Accepted { get; set; }
}
