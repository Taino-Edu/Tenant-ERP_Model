// =============================================================================
// IAuditService.cs — Contrato do serviço de auditoria LGPD
// Registra trilha de auditoria de ações sobre dados pessoais.
// =============================================================================

using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Http;

namespace CardGameStore.Services.Interfaces;

public interface IAuditService
{
    /// <summary>
    /// Registra uma ação de auditoria sobre dados pessoais.
    /// </summary>
    /// <param name="action">Ação realizada: "Visualizou", "Editou", "Exportou", "Deletou", "Respondeu" etc.</param>
    /// <param name="entityType">Tipo da entidade: "User", "Comanda", "LgpdRequest" etc.</param>
    /// <param name="entityId">ID da entidade afetada.</param>
    /// <param name="details">JSON com contexto adicional (opcional) — mesclado com o bloco
    /// "context" (user-agent parseado, geo do Cloudflare) que este serviço acrescenta.</param>
    /// <param name="httpContext">Contexto HTTP para extrair IP e usuário autenticado.</param>
    /// <param name="targetUserId">Usuário em nome de quem a ação foi feita — só em fluxos de
    /// impersonation (admin/plataforma operando como outro usuário).</param>
    /// <param name="channel">Origem da ação ("Web", "PDV", "API", "Cron"). Nulo = inferido
    /// automaticamente (há HttpContext → "Web", senão → "System").</param>
    /// <param name="severity">Gravidade do evento — default Info.</param>
    Task LogAsync(
        string         action,
        string         entityType,
        string?        entityId    = null,
        string?        details     = null,
        HttpContext?   httpContext = null,
        string?        targetUserId = null,
        string?        channel      = null,
        AuditSeverity  severity     = AuditSeverity.Info
    );
}
