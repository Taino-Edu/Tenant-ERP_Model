// =============================================================================
// AuditSaveChangesInterceptor.cs — Diff automático de auditoria
//
// Olha o ChangeTracker de AppDbContext logo ANTES de cada SaveChanges e, pra
// um conjunto pequeno de entidades sensíveis, grava sozinho um AuditLog com o
// que mudou (Modified) ou um snapshot da linha (Deleted) — sem precisar de
// uma chamada manual em cada controller/service.
//
// Por que não reaproveita IAuditService.LogAsync: esse método chama
// _db.SaveChangesAsync() sozinho, e chamá-lo de dentro de SavingChangesAsync
// causaria reentrância (um SaveChanges disparando outro no meio do primeiro).
// Em vez disso, os AuditLog novos são adicionados direto no mesmo
// ChangeTracker do contexto que já está salvando — viram parte do mesmo
// SaveChanges, no mesmo schema do tenant (o TenantConnectionInterceptor já
// isolou a conexão antes disso rodar).
// =============================================================================

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CardGameStore.Services.Implementations;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<Type> SensitiveTypes = new()
    {
        typeof(Product),
        typeof(VendaAvulsa),
        typeof(User),
    };

    /// <summary>Propriedades cujo valor nunca pode ir em texto puro pro log —
    /// aparecem como "[REDACTED]" no diff/snapshot, só denunciando que mudou.</summary>
    private static readonly HashSet<string> RedactedProperties = new()
    {
        nameof(User.PasswordHash),
        nameof(User.RefreshToken),
        nameof(User.PasswordResetToken),
    };

    /// <summary>Propriedades que mudam sozinhas a cada login (sessão) e não
    /// devem, por si só, disparar um log — o AuthController já loga
    /// "LoginSucesso" manualmente. Se algo mais relevante também mudou no
    /// mesmo Modified, o log sai normalmente (com essas ainda redigidas se
    /// também estiverem em RedactedProperties).</summary>
    private static readonly HashSet<string> NoiseProperties = new()
    {
        nameof(User.LastLoginAt),
        nameof(User.RefreshToken),
        nameof(User.RefreshTokenExpiry),
        "UpdatedAt",
    };

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditSaveChangesInterceptor> _logger;
    private readonly string _ipSalt;

    public AuditSaveChangesInterceptor(
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditSaveChangesInterceptor> logger,
        IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger              = logger;
        _ipSalt              = configuration["Security:IpHashSalt"] ?? "tenant-erp-ip-salt-dev";
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CaptureChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        CaptureChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void CaptureChanges(DbContext? context)
    {
        if (context is null) return;

        try
        {
            // Materializa antes de adicionar novas entidades ao ChangeTracker —
            // adicionar durante a enumeração quebraria o LINQ sobre Entries().
            var entries = context.ChangeTracker.Entries()
                .Where(e => SensitiveTypes.Contains(e.Entity.GetType())
                         && (e.State == EntityState.Modified || e.State == EntityState.Deleted))
                .ToList();

            if (entries.Count == 0) return;

            var ctx           = _httpContextAccessor.HttpContext;
            var actorUserId   = ctx?.User.FindFirst("sub")?.Value ?? ctx?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var actorUserName = ctx?.User.FindFirst(ClaimTypes.Name)?.Value ?? ctx?.User.FindFirst("name")?.Value;
            var ipHash        = HashIp(ctx?.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            var traceId       = ctx?.TraceIdentifier;
            var channel       = ctx is null ? "System" : "Web";

            foreach (var entry in entries)
            {
                var details = entry.State == EntityState.Deleted
                    ? BuildDeletedSnapshot(entry)
                    : BuildModifiedDiff(entry);

                // Modified sem nenhuma propriedade de fato alterada (ex: só um
                // relacionamento navegado tocado) não gera log — nada mudou.
                if (details is null) continue;

                context.Set<AuditLog>().Add(new AuditLog
                {
                    ActorUserId   = actorUserId,
                    ActorUserName = actorUserName,
                    Action        = entry.State == EntityState.Deleted ? "ExclusaoAutomatica" : "AlteracaoAutomatica",
                    EntityType    = entry.Entity.GetType().Name,
                    EntityId      = GetEntityId(entry),
                    Details       = details,
                    IpHash        = ipHash,
                    Channel       = channel,
                    Severity      = entry.State == EntityState.Deleted ? AuditSeverity.Warning : AuditSeverity.Info,
                    TraceId       = traceId,
                    CreatedAt     = DateTime.UtcNow,
                });
            }
        }
        catch (Exception ex)
        {
            // Mesma regra do AuditService: uma falha aqui nunca pode abortar o
            // SaveChanges de verdade do usuário.
            _logger.LogError(ex, "Falha ao capturar diff automático de auditoria.");
        }
    }

    private static string? BuildModifiedDiff(EntityEntry entry)
    {
        var modified = entry.Properties.Where(p => p.IsModified).ToList();

        // Se só propriedades de "ruído" de sessão mudaram (ex: login tocando
        // LastLoginAt/RefreshToken), não vale um log automático — o
        // AuthController já grava "LoginSucesso" manualmente pra isso.
        if (modified.All(p => NoiseProperties.Contains(p.Metadata.Name))) return null;

        var changes = modified
            .Select(p => new
            {
                campo = p.Metadata.Name,
                de    = RedactedProperties.Contains(p.Metadata.Name) ? "[REDACTED]" : p.OriginalValue,
                para  = RedactedProperties.Contains(p.Metadata.Name) ? "[REDACTED]" : p.CurrentValue,
            })
            .ToList();

        if (changes.Count == 0) return null;

        return JsonSerializer.Serialize(new { alteracoes = changes });
    }

    private static string BuildDeletedSnapshot(EntityEntry entry)
    {
        var snapshot = entry.Properties.ToDictionary(
            p => p.Metadata.Name,
            p => RedactedProperties.Contains(p.Metadata.Name) ? (object?)"[REDACTED]" : p.OriginalValue);
        return JsonSerializer.Serialize(new { snapshotExcluido = snapshot });
    }

    private static string? GetEntityId(EntityEntry entry) =>
        entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue?.ToString()
        ?? entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id")?.OriginalValue?.ToString();

    private string HashIp(string ip)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(_ipSalt + ip));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
