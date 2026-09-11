// =============================================================================
// TenantProvisioningService.cs — Cria um tenant novo de ponta a ponta:
// valida o slug, registra no catálogo, cria o schema Postgres, roda as
// migrations do AppDbContext nele e cadastra o admin inicial da loja.
// =============================================================================

using System.Text.RegularExpressions;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

public class TenantProvisioningService : ITenantProvisioningService
{
    private static readonly Regex SlugPattern = new(@"^[a-z0-9-]{1,20}$", RegexOptions.Compiled);
    private static readonly string[] ReservedSlugs = ["public", "www", "api", "admin"];

    /// <summary>Catálogo de módulos pagos reconhecidos — mesma lista que o frontend usa
    /// pra montar os checkboxes de criação/edição de tenant (ver lib/api.ts TENANT_MODULES).
    /// Módulo desconhecido na criação é rejeitado em vez de gravado silenciosamente (typo
    /// no request viraria um módulo fantasma, sem RequireModule nenhum lendo aquele nome).</summary>
    public static readonly string[] KnownModules = ["fiscal", "estoque", "pontos", "contador", "ia", "eventos", "restaurante"];

    /// <summary>Tabela de preços vigente (decidida em 2026-07-27, ver BACKLOG e a
    /// const PLANOS de frontend/app/institucional/page.tsx, que é o que o cliente
    /// vê). Serve só como PONTO DE PARTIDA do billing de um tenant novo — o valor
    /// real vive em Tenant.MonthlyPrice e é editável, porque desconto negociado
    /// caso a caso é regra nesse estágio, não exceção.
    ///
    /// A cópia no frontend (frontend/lib/planos.ts) continua existindo de
    /// propósito: a página de vendas não pode depender da API estar de pé pra
    /// mostrar preço — API fora do ar viraria plano sem valor na hora da venda.
    /// O que era risco real ("mudar num lugar e esquecer do outro") deixou de
    /// existir por outro caminho: TabelaPrecosSincronizadaTests compara os dois
    /// arquivos e quebra o CI se divergirem.</summary>
    internal static readonly Dictionary<string, decimal> TabelaPrecos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lagoa"] = 129m,
        ["Rio"]   = 269m,
        ["Mar"]   = 487m,
    };

    /// <summary>Módulos e limite de usuários de cada plano de tabela — o que o
    /// teste grátis criado pelo site liga (TenantSignupService). Mesmo acordo da
    /// tabela de preços: a fonte que o cliente vê é frontend/lib/planos.ts, e
    /// TabelaPrecosSincronizadaTests quebra o CI se os dois divergirem. No painel
    /// da plataforma os módulos continuam escolhidos à mão, loja a loja.</summary>
    internal static readonly Dictionary<string, (string[] Modulos, int? MaxUsuarios)> RecursosDosPlanos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lagoa"] = (new[] { "fiscal", "estoque", "restaurante" }, 2),
        ["Rio"]   = (new[] { "fiscal", "estoque", "restaurante", "pontos", "contador", "eventos" }, 6),
        // Restaurante fica fora do Mar de propósito — ver o comentário em planos.ts.
        ["Mar"]   = (new[] { "fiscal", "estoque", "pontos", "contador", "ia", "eventos" }, null),
    };

    /// <summary>Preço de tabela do plano, ou 0 se o nome não está na tabela
    /// (PlanName é texto livre: cortesia, piloto, plano legado ou typo). Zero é
    /// deliberado — chutar um valor infla o MRR com número que parece certo e que
    /// ninguém vai conferir depois.</summary>
    internal static decimal PrecoMensalDoPlano(string planName) =>
        TabelaPrecos.TryGetValue(planName.Trim(), out var preco) ? preco : 0m;

    internal static void ApplyCommercialTerms(Tenant tenant)
    {
        tenant.MonthlyPrice = PrecoMensalDoPlano(tenant.PlanName);
        // Implantação não nasce mais calculada (decisão de 2026-09-11). Até ali
        // eram 2 mensalidades em todo plano de tabela, o que não fecha com a loja
        // criada pelo próprio lojista no site: ninguém implantou nada, e a taxa
        // apareceria em /admin/assinatura como dívida surpresa no fim do teste.
        //
        // Quando houver implantação de verdade (migração assistida, treinamento),
        // o valor é definido loja a loja no painel da plataforma
        // (PATCH tenants/{id}/billing) ou lançado como cobrança avulsa no
        // financeiro. A conversão do CRM e o registro de indicação continuam
        // gerando a cobrança de implantação sozinhos sempre que SetupFee > 0.
        tenant.SetupFee = 0;
        tenant.BillingStartsOn = tenant.CreatedAt.AddDays(15);
    }

    // Provisionamento (criar schema + rodar migrations + admin inicial) não
    // tinha nenhuma trava de concorrência: dois cadastros de tenant no mesmo
    // instante podiam interferir um no outro. Ação rara/admin-only, então um
    // semáforo em memória (só serializa dentro do MESMO processo) já resolve
    // — essa app roda como instância única (docker-compose mono-nó, sem
    // múltiplas réplicas). Se um dia isso mudar, aí sim precisa de lock
    // distribuído de verdade (ex: advisory lock do Postgres).
    private static readonly SemaphoreSlim _provisionLock = new(1, 1);

    private readonly CatalogDbContext     _catalog;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TenantDatabaseAdmin  _databaseAdmin;
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(
        CatalogDbContext catalog,
        IServiceScopeFactory scopeFactory,
        TenantDatabaseAdmin databaseAdmin,
        ILogger<TenantProvisioningService> logger)
    {
        _catalog      = catalog;
        _scopeFactory = scopeFactory;
        _databaseAdmin = databaseAdmin;
        _logger       = logger;
    }

    public async Task<Tenant> ProvisionAsync(
        string slug, string? adminEmail, string? adminPassword, string[]? enabledModules = null,
        string? planName = null, int? maxUsers = null, TenantKind kind = TenantKind.Native,
        bool isPubliclyListed = false, TenantOwnerProfile? owner = null)
    {
        await _provisionLock.WaitAsync();
        try
        {
            return await ProvisionLockedAsync(slug, adminEmail, adminPassword, enabledModules, planName, maxUsers, kind, isPubliclyListed, owner);
        }
        finally
        {
            _provisionLock.Release();
        }
    }

    private async Task<Tenant> ProvisionLockedAsync(
        string slug, string? adminEmail, string? adminPassword, string[]? enabledModules,
        string? planName, int? maxUsers, TenantKind kind, bool isPubliclyListed, TenantOwnerProfile? owner)
    {
        slug = slug.Trim().ToLowerInvariant();

        if (!SlugPattern.IsMatch(slug))
            throw new InvalidOperationException("Slug inválido — use só letras minúsculas, números e hífen (1-20 caracteres).");

        if (ReservedSlugs.Contains(slug))
            throw new InvalidOperationException($"Slug '{slug}' é reservado e não pode ser usado.");

        if (!Enum.IsDefined(kind))
            throw new InvalidOperationException("Tipo de tenant inválido.");

        if (kind == TenantKind.Native)
        {
            if (string.IsNullOrWhiteSpace(adminEmail))
                throw new InvalidOperationException("Informe o e-mail do admin da loja.");
            // Quem cria pelo site chega com a senha já em hash (validada no DTO com
            // regra mais forte); só o painel da plataforma manda a senha em texto.
            if (owner is not null)
            {
                if (string.IsNullOrWhiteSpace(owner.PasswordHash))
                    throw new InvalidOperationException("O dono da loja precisa chegar com a senha definida.");
            }
            else if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 6)
                throw new InvalidOperationException("A senha inicial deve ter pelo menos 6 caracteres.");
        }

        var slugInUse = await _catalog.Tenants.AnyAsync(t => t.Slug == slug);
        if (slugInUse)
            throw new InvalidOperationException($"Já existe um tenant com o slug '{slug}'.");

        // Defesa em profundidade: o [Range(1,10000)] do DTO já barra isso no único
        // caller real (PlatformController), mas o service não deveria confiar só
        // nisso — qualquer chamador futuro também precisa respeitar o limite.
        if (maxUsers is < 1 or > 10000)
            throw new InvalidOperationException("Limite de usuários deve estar entre 1 e 10000.");

        string[]? modulosValidos = null;
        if (enabledModules is { Length: > 0 })
        {
            var desconhecidos = enabledModules.Where(m => !KnownModules.Contains(m, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (desconhecidos.Length > 0)
                throw new InvalidOperationException($"Módulo(s) desconhecido(s): {string.Join(", ", desconhecidos)}.");

            modulosValidos = enabledModules.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        // Tenants externos mantêm vendas/estoque/financeiro no ERP de origem, mas
        // possuem schema físico para configuração, certificado e documentos do
        // motor fiscal hospedado. O prefixo distinto deixa essa residência híbrida
        // explícita em operações de banco e backup.
        var schemaName = (kind == TenantKind.Native ? "tenant_" : "external_") + slug.Replace('-', '_');
        TenantSchemaName.Validate(schemaName);

        var tenant = new Tenant
        {
            Slug       = slug,
            SchemaName = schemaName,
            Status     = TenantStatus.Active,
            Kind       = kind,
            IsPubliclyListed = isPubliclyListed,
        };
        // Só sobrescreve o default (["fiscal"]) se o chamador passou módulos —
        // preserva o comportamento de antes desse parâmetro existir.
        if (modulosValidos is not null)
            tenant.EnabledModules = modulosValidos;
        if (!string.IsNullOrWhiteSpace(planName))
            tenant.PlanName = planName.Trim();
        if (maxUsers.HasValue)
            tenant.MaxUsers = maxUsers.Value;
        if (owner is not null)
        {
            // O nome digitado no site já identifica a loja na vitrine, no painel
            // da plataforma e no cliente do gateway; quem cadastrou recebe a
            // cobrança até informar outro e-mail em /admin/assinatura.
            tenant.DisplayName  = owner.StoreName;
            tenant.BillingEmail = adminEmail!.Trim().ToLowerInvariant();
        }

        // Billing: preenche a partir da tabela vigente e das regras comerciais
        // (mensalidade do plano, implantação zero, 15 dias grátis).
        // Fica editável depois no painel — a tabela é o ponto de partida, não uma
        // amarra: cliente que fechar por valor negociado tem o campo ajustado.
        //
        // Plano fora da tabela (nome livre, cortesia, piloto) entra com preço 0 em
        // vez de chutar um valor: MRR errado pra cima é pior que MRR incompleto,
        // porque parece certo e ninguém vai conferir.
        ApplyCommercialTerms(tenant);

        _catalog.Tenants.Add(tenant);
        await _catalog.SaveChangesAsync();

        try
        {
            // O schema físico precisa existir ANTES de qualquer conexão do
            // AppDbContext tentar apontar search_path pra ele (ver
            // TenantConnectionInterceptor.ValidateSchemaName). schemaName só
            // contém [a-z0-9_] (validado acima via SlugPattern + prefixo fixo),
            // então a interpolação abaixo é segura — identificadores (nome de
            // schema) não podem ser parametrizados via ExecuteSqlAsync de qualquer forma.
            await _databaseAdmin.CreateAndMigrateTenantAsync(
                tenant.Id, schemaName, tenant.EnabledModules);

            using var scope = _scopeFactory.CreateScope();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.Set(tenant.Id, schemaName, tenant.EnabledModules);

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (kind == TenantKind.Native)
            {
                db.Users.Add(new User
                {
                    Id           = owner?.UserId ?? Guid.NewGuid(),
                    Name         = owner?.Name ?? adminEmail!,
                    Email        = adminEmail!.Trim().ToLowerInvariant(),
                    PasswordHash = owner?.PasswordHash ?? BCrypt.Net.BCrypt.HashPassword(adminPassword!),
                    Role         = UserRole.Admin,
                });

                if (owner is not null)
                {
                    // Sem isto a loja nova se apresentaria como "Octus" (o default da
                    // SiteConfig) no título da vitrine e nos e-mails até o dono achar a
                    // tela de personalização. Busca antes de criar porque a linha
                    // singleton não vem de migration: nasce na primeira gravação.
                    var site = await db.SiteConfigs.FindAsync(SiteConfig.SingletonId);
                    if (site is null)
                    {
                        site = new SiteConfig();
                        db.SiteConfigs.Add(site);
                    }
                    site.SiteName = owner.StoreName;
                }

                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao provisionar tenant '{Slug}' — removendo entrada órfã do catálogo.", slug);
            _catalog.Tenants.Remove(tenant);
            await _catalog.SaveChangesAsync();
            throw;
        }

        _logger.LogInformation(
            "Tenant '{Slug}' provisionado (schema '{Schema}', tipo {Kind}).", slug, schemaName, kind);
        return tenant;
    }
}
