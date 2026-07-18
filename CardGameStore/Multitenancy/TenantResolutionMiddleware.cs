// =============================================================================
// TenantResolutionMiddleware.cs — Resolve o tenant da requisição pelo Host.
//
// Lê HttpContext.Request.Host.Host diretamente — não precisa de
// X-Forwarded-Host porque o nginx já faz `proxy_set_header Host $host;` em
// todas as locations, então o Host original chega intacto até aqui.
//
// Sem RootDomain configurado (Multitenancy:RootDomain), ou host que não é um
// subdomínio dele (IP puro, domínio raiz, domínio de terceiro sem CustomDomain):
// cai no tenant-zero (schema "public") — mantém o acesso atual funcionando
// enquanto o DNS wildcard não existe e o catálogo está vazio.
//
// Já um subdomínio BEM-FORMADO do RootDomain que não existe no catálogo
// (loja-inexistente.RootDomain) retorna 404 — não pode servir a loja do
// tenant-zero, ver InvokeAsync.
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
        var host = context.Request.Host.Host;
        var slug = ExtractSlug(host, _rootDomain);

        TenantLookup? tenant = null;

        if (slug is not null)
        {
            tenant = await _cache.GetOrCreateAsync($"tenant-slug:{slug}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                return await catalog.Tenants
                    .Where(t => t.Slug == slug)
                    .Select(t => new TenantLookup(t.Id, t.SchemaName, t.Status, t.EnabledModules))
                    .FirstOrDefaultAsync();
            });

            // Slug bem-formado (subdomínio de nível único do RootDomain) que NÃO
            // bate com nenhum tenant: é uma loja desconhecida (typo de subdomínio,
            // loja removida), não um acesso legítimo por IP/domínio-raiz. Sem este
            // 404, qualquer *.RootDomain inexistente serviria a vitrine e a tela de
            // login do tenant-zero (schema "public"), com cookies válidos pro host
            // — o visitante poderia logar/comprar na "loja" errada sem perceber.
            // O caminho de CustomDomain abaixo só existe quando slug is null, então
            // não há o que preservar aqui.
            if (tenant is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new { Message = "Loja não encontrada." });
                return;
            }
        }

        // Sem subdomínio reconhecido (host não é um slug de <RootDomain>) — tenta
        // domínio próprio do lojista (BYO domain). Sem automação de TLS: o
        // lojista aponta o domínio dele pra cá atrás da própria Cloudflare
        // (modo Flexible), do mesmo jeito que o domínio raiz da plataforma já
        // funciona — ver comentário em Tenant.CustomDomain.
        if (tenant is null && slug is null)
        {
            var hostLower = host.ToLowerInvariant();
            tenant = await _cache.GetOrCreateAsync($"tenant-domain:{hostLower}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                return await catalog.Tenants
                    .Where(t => t.CustomDomain == hostLower)
                    .Select(t => new TenantLookup(t.Id, t.SchemaName, t.Status, t.EnabledModules))
                    .FirstOrDefaultAsync();
            });
        }

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

        // Nada reconhecido (slug e domínio próprio) — tenant-zero.
        tenantContext.Set(TenantConstants.TenantZeroId, TenantConstants.TenantZeroSchema, new[] { "fiscal" });
        await _next(context);
    }

    private sealed record TenantLookup(Guid Id, string SchemaName, TenantStatus Status, string[] EnabledModules);

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
