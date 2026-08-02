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
    public static readonly string[] KnownModules = ["fiscal", "estoque", "restaurante", "pontos", "contador", "ia", "eventos"];

    /// <summary>Tabela de preços vigente (decidida em 2026-07-27, ver BACKLOG e a
    /// const PLANOS de frontend/app/institucional/page.tsx, que é o que o cliente
    /// vê). Serve só como PONTO DE PARTIDA do billing de um tenant novo — o valor
    /// real vive em Tenant.MonthlyPrice e é editável, porque desconto negociado
    /// caso a caso é regra nesse estágio, não exceção.
    ///
    /// Duplicação com o frontend é consciente: são públicos diferentes (página de
    /// venda vs. provisionamento) e unificar exigiria um endpoint de tabela de
    /// preços que nada consome ainda. Ao mudar preço, mudar nos dois — está
    /// anotado no BACKLOG.</summary>
    private static readonly Dictionary<string, decimal> TabelaPrecos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Essencial"] = 120m,
        ["Completo"]  = 269m,
        ["Avançado"]  = 487m,
    };

    /// <summary>Preço de tabela do plano, ou 0 se o nome não está na tabela
    /// (PlanName é texto livre: cortesia, piloto, plano legado ou typo). Zero é
    /// deliberado — chutar um valor infla o MRR com número que parece certo e que
    /// ninguém vai conferir depois.</summary>
    private static decimal PrecoMensalDoPlano(string planName) =>
        TabelaPrecos.TryGetValue(planName.Trim(), out var preco) ? preco : 0m;

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
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(
        CatalogDbContext catalog,
        IServiceScopeFactory scopeFactory,
        ILogger<TenantProvisioningService> logger)
    {
        _catalog      = catalog;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    public async Task<Tenant> ProvisionAsync(
        string slug, string adminEmail, string adminPassword, string[]? enabledModules = null,
        string? planName = null, int? maxUsers = null)
    {
        await _provisionLock.WaitAsync();
        try
        {
            return await ProvisionLockedAsync(slug, adminEmail, adminPassword, enabledModules, planName, maxUsers);
        }
        finally
        {
            _provisionLock.Release();
        }
    }

    private async Task<Tenant> ProvisionLockedAsync(
        string slug, string adminEmail, string adminPassword, string[]? enabledModules,
        string? planName, int? maxUsers)
    {
        slug = slug.Trim().ToLowerInvariant();

        if (!SlugPattern.IsMatch(slug))
            throw new InvalidOperationException("Slug inválido — use só letras minúsculas, números e hífen (1-20 caracteres).");

        if (ReservedSlugs.Contains(slug))
            throw new InvalidOperationException($"Slug '{slug}' é reservado e não pode ser usado.");

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

        var schemaName = "tenant_" + slug.Replace('-', '_');
        TenantSchemaName.Validate(schemaName);

        var tenant = new Tenant
        {
            Slug       = slug,
            SchemaName = schemaName,
            Status     = TenantStatus.Active,
        };
        // Só sobrescreve o default (["fiscal"]) se o chamador passou módulos —
        // preserva o comportamento de antes desse parâmetro existir.
        if (modulosValidos is not null)
            tenant.EnabledModules = modulosValidos;
        if (!string.IsNullOrWhiteSpace(planName))
            tenant.PlanName = planName.Trim();
        if (maxUsers.HasValue)
            tenant.MaxUsers = maxUsers.Value;

        // Billing: preenche a partir da tabela vigente e das regras comerciais
        // (implantação = 2 mensalidades, primeiro mês de acesso sem mensalidade).
        // Fica editável depois no painel — a tabela é o ponto de partida, não uma
        // amarra: cliente que fechar por valor negociado tem o campo ajustado.
        //
        // Plano fora da tabela (nome livre, cortesia, piloto) entra com preço 0 em
        // vez de chutar um valor: MRR errado pra cima é pior que MRR incompleto,
        // porque parece certo e ninguém vai conferir.
        tenant.MonthlyPrice     = PrecoMensalDoPlano(tenant.PlanName);
        tenant.SetupFee         = tenant.MonthlyPrice * 2;
        tenant.BillingStartsOn  = tenant.CreatedAt.AddMonths(1);

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
#pragma warning disable EF1002
            await _catalog.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"");
#pragma warning restore EF1002

            using var scope = _scopeFactory.CreateScope();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.Set(tenant.Id, schemaName, tenant.EnabledModules);

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();

            db.Users.Add(new User
            {
                Name         = adminEmail,
                Email        = adminEmail.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role         = UserRole.Admin,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao provisionar tenant '{Slug}' — removendo entrada órfã do catálogo.", slug);
            _catalog.Tenants.Remove(tenant);
            await _catalog.SaveChangesAsync();
            throw;
        }

        _logger.LogInformation("Tenant '{Slug}' provisionado (schema '{Schema}').", slug, schemaName);
        return tenant;
    }
}
