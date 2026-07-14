// =============================================================================
// AuditService.cs — Implementação do serviço de auditoria LGPD
//
// PRIVACIDADE: O IP é transformado em hash SHA-256 antes de qualquer persistência.
// Nunca salvamos o endereço IP em texto puro.
// =============================================================================

using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using UAParser;

namespace CardGameStore.Services.Implementations;

public class AuditService : IAuditService
{
    private readonly AppDbContext              _db;
    private readonly IHttpContextAccessor      _httpContextAccessor;
    private readonly ILogger<AuditService>     _logger;
    private readonly string                    _ipSalt;

    public AuditService(
        AppDbContext          db,
        IHttpContextAccessor  httpContextAccessor,
        ILogger<AuditService> logger,
        IConfiguration        configuration)
    {
        _db                  = db;
        _httpContextAccessor = httpContextAccessor;
        _logger              = logger;
        // Salt impede ataque de dicionário sobre o hash de IP (espaço IPv4 é finito)
        _ipSalt              = configuration["Security:IpHashSalt"] ?? "tenant-erp-ip-salt-dev";
    }

    /// <inheritdoc/>
    public async Task LogAsync(
        string        action,
        string        entityType,
        string?       entityId     = null,
        string?       details      = null,
        HttpContext?  httpContext  = null,
        string?       targetUserId = null,
        string?       channel      = null,
        AuditSeverity severity     = AuditSeverity.Info)
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

            // IP já resolvido pelo middleware UseForwardedHeaders (Program.cs)
            // RemoteIpAddress reflete o IP real do cliente após o proxy processar X-Forwarded-For
            var ip = ctx?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var log = new AuditLog
            {
                ActorUserId   = actorUserId,
                ActorUserName = actorUserName,
                Action        = action,
                EntityType    = entityType,
                EntityId      = entityId,
                Details       = BuildDetails(details, ctx),
                IpHash        = HashIp(ip),
                TargetUserId  = targetUserId,
                Channel       = channel ?? InferChannel(ctx),
                Severity      = severity,
                TraceId       = ctx?.TraceIdentifier,
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

    /// <summary>Sem HttpContext (job em background, worker de fila) não tem como
    /// ser "Web" — nesse caso é o próprio sistema agindo, não uma requisição.</summary>
    internal static string InferChannel(HttpContext? ctx) => ctx is null ? "System" : "Web";

    /// <summary>
    /// Mescla o "details" que o chamador passou (JSON de negócio, específico da
    /// ação) com um bloco "context" técnico capturado automaticamente aqui —
    /// user-agent parseado e geolocalização vinda dos headers do Cloudflare.
    /// Se "details" não vier num objeto JSON válido, preserva o texto original
    /// dentro de "message" em vez de descartar.
    /// </summary>
    private static string BuildDetails(string? details, HttpContext? ctx)
    {
        var uaString = ctx?.Request.Headers.UserAgent.ToString();
        ClientInfo? client = string.IsNullOrWhiteSpace(uaString) ? null : Parser.GetDefault().Parse(uaString);

        // Cloudflare injeta esses headers na borda — ausentes se a origem for
        // acessada direto (sem passar pelo CDN), ex: chamadas internas/testes.
        var country = ctx?.Request.Headers["CF-IPCountry"].ToString();
        var city    = ctx?.Request.Headers["CF-IPCity"].ToString();

        var context = new
        {
            userAgent = new
            {
                raw            = uaString,
                os             = client?.OS.Family,
                osVersion      = client is null ? null : $"{client.OS.Major}.{client.OS.Minor}".Trim('.'),
                browser        = client?.UA.Family,
                browserVersion = client is null ? null : $"{client.UA.Major}.{client.UA.Minor}".Trim('.'),
                device         = client?.Device.Family,
                // Heurística: UAParser não expõe um IsMobile direto — "Other" é
                // o valor default de Device.Family em desktop; qualquer família
                // reconhecida diferente disso geralmente é celular/tablet (não
                // é 100% preciso, mas suficiente pro contexto de auditoria).
                isMobile       = client is not null && client.Device.Family != "Other",
            },
            geo = new
            {
                country = string.IsNullOrWhiteSpace(country) ? null : country,
                city    = string.IsNullOrWhiteSpace(city)    ? null : city,
            },
        };

        var contextNode = JsonSerializer.SerializeToNode(context)!;

        JsonObject root;
        if (!string.IsNullOrWhiteSpace(details))
        {
            try
            {
                root = JsonNode.Parse(details) as JsonObject ?? new JsonObject { ["message"] = details };
            }
            catch (JsonException)
            {
                root = new JsonObject { ["message"] = details };
            }
        }
        else
        {
            root = new JsonObject();
        }

        root["context"] = contextNode;
        return root.ToJsonString();
    }

    /// <summary>
    /// Gera um HMAC-SHA-256 do endereço IP usando o salt como chave.
    /// Isso torna o valor pseudoanônimo — não é possível recuperar o IP original.
    /// HMAC (em vez de SHA256(salt + ip)) evita ataques de length-extension sobre a concatenação.
    /// </summary>
    private string HashIp(string ip)
    {
        var bytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_ipSalt), Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
