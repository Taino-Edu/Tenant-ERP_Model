// =============================================================================
// IAuditService.cs — Contrato do serviço de auditoria LGPD
// Registra trilha de auditoria de ações sobre dados pessoais.
// =============================================================================

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
    /// <param name="details">JSON com contexto adicional (opcional).</param>
    /// <param name="httpContext">Contexto HTTP para extrair IP e usuário autenticado.</param>
    Task LogAsync(
        string         action,
        string         entityType,
        string?        entityId    = null,
        string?        details     = null,
        HttpContext?   httpContext  = null
    );
}
