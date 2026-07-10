// =============================================================================
// ITenantContext.cs — Tenant resolvido para a requisição atual (scoped).
// Valor padrão é o tenant-zero (schema "public") — se por algum motivo o
// TenantResolutionMiddleware não rodar antes de algo pedir o AppDbContext
// (ex: um hosted service fora do pipeline HTTP), o comportamento cai pro
// mesmo schema usado hoje, em vez de falhar ou vazar pra um schema errado.
// =============================================================================

namespace CardGameStore.Multitenancy;

public static class TenantConstants
{
    /// <summary>Tenant fixo usado enquanto não há resolução por subdomínio (sem DNS
    /// wildcard ainda, ou acesso direto por IP/domínio raiz) — aponta pro schema
    /// "public", preservando o comportamento single-tenant atual.</summary>
    public static readonly Guid TenantZeroId = Guid.Empty;
    public const string TenantZeroSchema = "public";

    /// <summary>Nome da claim JWT que carrega o Id do tenant (não o schema — não
    /// invalida tokens se o tenant for renomeado).</summary>
    public const string TenantIdClaimType = "tenant_id";
}

public interface ITenantContext
{
    Guid TenantId { get; }
    string SchemaName { get; }
    void Set(Guid tenantId, string schemaName);
}

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; } = TenantConstants.TenantZeroId;
    public string SchemaName { get; private set; } = TenantConstants.TenantZeroSchema;

    public void Set(Guid tenantId, string schemaName)
    {
        TenantId = tenantId;
        SchemaName = schemaName;
    }
}
