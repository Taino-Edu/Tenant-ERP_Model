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

    public async Task<Tenant> ProvisionAsync(string slug, string adminEmail, string adminPassword)
    {
        slug = slug.Trim().ToLowerInvariant();

        if (!SlugPattern.IsMatch(slug))
            throw new InvalidOperationException("Slug inválido — use só letras minúsculas, números e hífen (1-20 caracteres).");

        if (ReservedSlugs.Contains(slug))
            throw new InvalidOperationException($"Slug '{slug}' é reservado e não pode ser usado.");

        var slugInUse = await _catalog.Tenants.AnyAsync(t => t.Slug == slug);
        if (slugInUse)
            throw new InvalidOperationException($"Já existe um tenant com o slug '{slug}'.");

        var schemaName = "tenant_" + slug.Replace('-', '_');

        var tenant = new Tenant
        {
            Slug       = slug,
            SchemaName = schemaName,
            Status     = TenantStatus.Active,
        };

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
