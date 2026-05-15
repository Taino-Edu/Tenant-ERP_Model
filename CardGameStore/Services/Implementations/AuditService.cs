// =============================================================================
// AuditService.cs — Implementação do serviço de auditoria LGPD
//
// PRIVACIDADE: O IP é transformado em hash SHA-256 antes de qualquer persistência.
// Nunca salvamos o endereço IP em texto puro.
// =============================================================================

using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CardGameStore.Services.Implementations;

public class AuditService : IAuditService
{
    private readonly AppDbContext              _db;
    private readonly IHttpContextAccessor      _httpContextAccessor;
    private readonly ILogger<AuditService>     _logger;

    public AuditService(
        AppDbContext          db,
        IHttpContextAccessor  httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _db                  = db;
        _httpContextAccessor = httpContextAccessor;
        _logger              = logger;
    }

    /// <inheritdoc/>
    public async Task LogAsync(
        string       action,
        string       entityType,
        string?      entityId   = null,
        string?      details    = null,
        HttpContext?  httpContext = null)
    {
        try
        {
            // Resolve o contexto: preferência para o passado explicitamente,
            // fallback para o IHttpContextAccessor (injeção por DI)
            var ctx = httpContext ?? _httpContextAccessor.HttpContext;

            // Extrai o ator a partir dos claims JWT
            var actorUserId   = ctx?.User.FindFirst("sub")?.Value
                             ?? ctx?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var actorUserName = ctx?.User.FindFirst(ClaimTypes.Name)?.Value
                             ?? ctx?.User.FindFirst("name")?.Value;

            // Obtém IP real (pode vir via header X-Forwarded-For em proxy/nginx)
            var ip = ctx?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (ctx?.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) == true
                && !string.IsNullOrWhiteSpace(forwarded))
            {
                ip = forwarded.ToString().Split(',')[0].Trim();
            }

            var log = new AuditLog
            {
                ActorUserId   = actorUserId,
                ActorUserName = actorUserName,
                Action        = action,
                EntityType    = entityType,
                EntityId      = entityId,
                Details       = details,
                IpHash        = HashIp(ip),
                CreatedAt     = DateTime.UtcNow,
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Falha no audit log nunca deve derrubar o fluxo principal
            _logger.LogError(ex,
                "Falha ao registrar audit log: action={Action} entityType={EntityType} entityId={EntityId}",
                action, entityType, entityId);
        }
    }

    // ── Interno ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Gera um hash SHA-256 do endereço IP.
    /// Isso torna o valor pseudoanônimo — não é possível recuperar o IP original.
    /// </summary>
    private static string HashIp(string ip)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
