// =============================================================================
// TenantResolutionMiddleware.cs — Resolve o tenant da requisição pelo Host.
//
// Lê HttpContext.Request.Host.Host diretamente — não precisa de
// X-Forwarded-Host porque o nginx já faz `proxy_set_header Host $host;` em
// todas as locations, então o Host original chega intacto até aqui.
//
// Sem RootDomain configurado (Multitenancy:RootDomain), ou host que não é um
// subdomínio dele, ou slug que não bate com nenhum tenant ativo: cai no
// tenant-zero (schema "public") — mantém o acesso atual funcionando (IP puro,
// domínio raiz) enquanto o DNS wildcard não existe e o catálogo está vazio.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CardGameStore.Multitenancy;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly string? _rootDomain;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public TenantResolutionMiddleware(RequestDelegate next, IMemoryCache cache, IConfiguration config)
    {
        _next       = next;
        _cache      = cache;
        _rootDomain = config["Multitenancy:RootDomain"];
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, CatalogDbContext catalog)
    {
        var slug = ExtractSlug(context.Request.Host.Host, _rootDomain);

        if (slug is not null)
        {
            var cacheKey = $"tenant-slug:{slug}";
            var tenant = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                return await catalog.Tenants
                    .Where(t => t.Slug == slug)
                    .Select(t => new { t.Id, t.SchemaName, t.Status, t.EnabledModules })
                    .FirstOrDefaultAsync();
            });

            if (tenant is not null)
            {
                if (tenant.Status != TenantStatus.Active)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { Message = "Esta loja está temporariamente suspensa." });
                    return;
                }

                tenantContext.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);
                await _next(context);
                return;
            }
        }

        // Sem slug reconhecido (ou tenant não encontrado/inativo) — tenant-zero.
        tenantContext.Set(TenantConstants.TenantZeroId, TenantConstants.TenantZeroSchema, new[] { "fiscal" });
        await _next(context);
    }

    /// <summary>
    /// Extrai o primeiro label do host quando ele é exatamente um subdomínio de
    /// nível único do RootDomain configurado (ex: "loja-maikon.2esysten.com.br"
    /// com RootDomain "2esysten.com.br" → "loja-maikon"). Domínio raiz, IP puro,
    /// "localhost" ou subdomínios de múltiplos níveis retornam null (tenant-zero).
    /// </summary>
    internal static string? ExtractSlug(string host, string? rootDomain)
    {
        if (string.IsNullOrWhiteSpace(rootDomain) || string.IsNullOrWhiteSpace(host))
            return null;

        var suffix = "." + rootDomain;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return null;

        var slug = host[..^suffix.Length];
        // Subdomínio de nível único apenas — "a.b.dominio.com" não é um slug válido.
        return slug.Length > 0 && !slug.Contains('.') ? slug.ToLowerInvariant() : null;
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
