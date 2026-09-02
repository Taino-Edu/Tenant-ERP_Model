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
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private readonly string? _rootDomain;
    private readonly bool _rejectUnknownHosts;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public TenantResolutionMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        IConfiguration config,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next       = next;
        _cache      = cache;
        _logger     = logger;
        _rootDomain = config["Multitenancy:RootDomain"];
        _rejectUnknownHosts = config.GetValue("Multitenancy:RejectUnknownHosts", false);
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, CatalogDbContext catalog)
    {
        // Estes endpoints públicos resolvem o tenant pelo parâmetro `slug` no
        // próprio controller. O SSR chama a API diretamente pela rede Docker,
        // cujo Host é o nome do serviço (ex.: cardgamestore_api), e por isso não
        // deve passar pela resolução/rejeição baseada em domínio. A exceção é
        // deliberadamente exata: nenhuma outra rota pública ou autenticada ganha
        // acesso por um Host desconhecido.
        if (IsSlugResolvedPublicPath(context.Request.Path))
        {
            await SetTenantAndContinue(
                context, tenantContext,
                TenantConstants.TenantZeroId, TenantConstants.TenantZeroSchema, new[] { "fiscal" });
            return;
        }

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
                    .Select(t => new TenantLookup(t.Id, t.SchemaName, t.Status, t.Kind, t.EnabledModules))
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
                    .Select(t => new TenantLookup(t.Id, t.SchemaName, t.Status, t.Kind, t.EnabledModules))
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

            if (tenant.Kind == TenantKind.ExternalIntegrated &&
                !context.Request.Path.StartsWithSegments("/api/integrations"))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new
                {
                    Message = "Este tenant usa um sistema externo e disponibiliza somente APIs de integração."
                });
                return;
            }

            await SetTenantAndContinue(context, tenantContext, tenant.Id, tenant.SchemaName, tenant.EnabledModules);
            return;
        }

        // Em produção, Host arbitrário não pode cair silenciosamente no
        // tenant-zero. Isso evita servir dados públicos do schema "public" por
        // IP direto, domínio de terceiro ou Host forjado. O domínio raiz e www
        // continuam sendo os Hosts legítimos da plataforma.
        if (_rejectUnknownHosts
            && !IsPlatformHost(host, _rootDomain)
            && !context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { Message = "Endereço não reconhecido." });
            return;
        }

        // Nada reconhecido, mas Host de plataforma permitido — tenant-zero.
        await SetTenantAndContinue(
            context, tenantContext,
            TenantConstants.TenantZeroId, TenantConstants.TenantZeroSchema, new[] { "fiscal" });
    }

    /// <summary>
    /// Popula o ITenantContext e segue o pipeline dentro de um escopo de log que
    /// carrega a identidade do tenant.
    ///
    /// O escopo é o ponto: sem ele, NENHUMA linha de log do sistema dizia de qual
    /// loja ela veio — 40 controllers logando "erro ao salvar" sem jeito de saber
    /// o dono do problema quando um lojista específico reclama. Como o escopo
    /// nasce aqui, no único lugar por onde toda requisição passa, todo log emitido
    /// daqui pra frente herda os campos sem precisar tocar em serviço nenhum.
    ///
    /// Só aparece na saída com Logging:Console:IncludeScopes = true
    /// (appsettings.json) — o padrão do .NET é descartar escopos.
    /// </summary>
    private async Task SetTenantAndContinue(
        HttpContext context,
        ITenantContext tenantContext,
        Guid tenantId,
        string schemaName,
        string[] enabledModules)
    {
        tenantContext.Set(tenantId, schemaName, enabledModules);

        // Cópia do tenant no HttpContext, além do ITenantContext: o SignalR cria
        // um escopo de DI PRÓPRIO por invocação de hub e não herda o escopo desta
        // requisição, então o ITenantContext que o hub recebe nasce no default
        // (tenant-zero) e o middleware nunca chega nele. O HttpContext do
        // handshake, ao contrário, continua acessível pela vida da conexão via
        // HubCallerContext.GetHttpContext() — é por ele que o TenantHubFilter
        // reidrata o tenant antes de qualquer método do hub rodar.
        //
        // Sem isso, um admin de loja.dominio entrava no grupo da tenant-zero
        // enquanto os eventos da loja iam pro grupo dela: conectado e surdo, sem
        // erro nenhum. E toda conexão de cliente estourava, porque o
        // OnConnectedAsync dele toca o banco e o TenantConnectionInterceptor tem
        // fail-fast pra escopo sem Set().
        context.Items[TenantHubFilter.HttpContextItemKey] =
            new TenantSnapshot(tenantId, schemaName, enabledModules);

        // Template de mensagem (e não um Dictionary) de propósito: o formatter
        // do console renderiza o escopo chamando ToString() nele, e
        // Dictionary.ToString() devolve "System.Collections.Generic.Dictionary`2
        // [System.String,System.Object]" — o escopo aparecia no log sem nenhum
        // dos valores. Com o template sai legível ("TenantSchema:loja_x ...") e
        // os placeholders nomeados continuam virando propriedades estruturadas
        // se um dia entrar um sink que as consuma.
        using (_logger.BeginScope("TenantSchema:{TenantSchema} TenantId:{TenantId}", schemaName, tenantId))
        {
            await _next(context);
        }
    }

    private sealed record TenantLookup(
        Guid Id, string SchemaName, TenantStatus Status, TenantKind Kind, string[] EnabledModules);

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
        return slug.Length > 0
            && !slug.Contains('.')
            && !slug.Equals("www", StringComparison.OrdinalIgnoreCase)
                ? slug.ToLowerInvariant()
                : null;
    }

    internal static bool IsPlatformHost(string host, string? rootDomain) =>
        !string.IsNullOrWhiteSpace(rootDomain)
        && (host.Equals(rootDomain, StringComparison.OrdinalIgnoreCase)
            || host.Equals($"www.{rootDomain}", StringComparison.OrdinalIgnoreCase));

    internal static bool IsSlugResolvedPublicPath(PathString path) =>
        path.Equals("/api/public/site-icons", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/public/product", StringComparison.OrdinalIgnoreCase);
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
