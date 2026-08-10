using CardGameStore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CardGameStore.Multitenancy;

/// <summary>
/// Único caminho autorizado a usar a credencial administrativa do PostgreSQL.
/// Requests normais usam ConnectionStrings:PostgreSQL (papel sem DDL e sem
/// acesso a outros schemas); migrations/provisionamento usam PostgreSQLAdmin.
/// </summary>
public sealed class TenantDatabaseAdmin
{
    private readonly string _connectionString;
    private readonly string _catalogRole;
    private readonly TenantDatabaseCredentials _credentials;

    public TenantDatabaseAdmin(IConfiguration configuration, TenantDatabaseCredentials credentials)
    {
        _connectionString = configuration.GetConnectionString("PostgreSQLAdmin")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:PostgreSQLAdmin é obrigatória quando PostgreSQL está habilitado.");
        _catalogRole = ValidateRole(configuration["Database:CatalogRole"] ?? "cardgame_catalog");
        _credentials = credentials;
    }

    public async Task MigrateCatalogAsync(CancellationToken ct = default)
    {
        await using var db = CreateCatalogDb();
        await db.Database.MigrateAsync(ct);
        await GrantTablesAsync(
            TenantConstants.TenantZeroSchema, _catalogRole,
            db.Model.GetEntityTypes().Select(t => t.GetTableName()).Where(n => n is not null)!,
            ct);
    }

    public async Task<List<Tenant>> ListTenantsAsync(CancellationToken ct = default)
    {
        await using var db = CreateCatalogDb();
        return await db.Tenants.AsNoTracking().ToListAsync(ct);
    }

    public async Task CreateAndMigrateTenantAsync(
        Guid tenantId, string schemaName, string[] enabledModules, CancellationToken ct = default)
    {
        schemaName = TenantSchemaName.Validate(schemaName);
        await ExecuteAdminSqlAsync($"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"", ct);
        await MigrateTenantAsync(tenantId, schemaName, enabledModules, ct);
    }

    public async Task MigrateTenantAsync(
        Guid tenantId, string schemaName, string[] enabledModules, CancellationToken ct = default)
    {
        schemaName = TenantSchemaName.Validate(schemaName);
        var tenant = new TenantContext();
        tenant.Set(tenantId, schemaName, enabledModules);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                _connectionString,
                npgsql => npgsql
                    .EnableRetryOnFailure(maxRetryCount: 5)
                    .MigrationsHistoryTable("__EFMigrationsHistory", schemaName))
            .AddInterceptors(new TenantConnectionInterceptor(
                tenant, NullLogger<TenantConnectionInterceptor>.Instance))
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync(ct);
        var role = _credentials.RoleFor(tenantId);
        var password = _credentials.PasswordFor(tenantId);
        await EnsureTenantRoleAsync(role, password, ct);
        await GrantTablesAsync(
            schemaName, role,
            db.Model.GetEntityTypes().Select(t => t.GetTableName()).Where(n => n is not null)!,
            ct);
    }

    public Task DropTenantSchemaAsync(string schemaName, CancellationToken ct = default)
    {
        schemaName = TenantSchemaName.Validate(schemaName);
        return ExecuteAdminSqlAsync($"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE", ct);
    }

    private CatalogDbContext CreateCatalogDb()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5))
            .Options;
        return new CatalogDbContext(options);
    }

    private async Task EnsureTenantRoleAsync(string role, string password, CancellationToken ct)
    {
        role = ValidateRole(role);
        if (password.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new InvalidOperationException("Senha derivada contém caractere inesperado.");
        await ExecuteAdminSqlAsync(
            $"DO $do$ BEGIN " +
            $"IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{role}') THEN " +
            $"CREATE ROLE \"{role}\" LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT; " +
            $"ELSE ALTER ROLE \"{role}\" PASSWORD '{password}'; END IF; END $do$;",
            ct);
    }

    private async Task GrantTablesAsync(
        string schemaName, string roleName, IEnumerable<string?> tableNames, CancellationToken ct)
    {
        schemaName = TenantSchemaName.Validate(schemaName);
        roleName = ValidateRole(roleName);
        var tables = tableNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .Select(n => $"\"{schemaName}\".\"{n!.Replace("\"", "\"\"")}\"")
            .ToArray();
        if (tables.Length == 0) return;

        await ExecuteAdminSqlAsync(
            $"GRANT USAGE ON SCHEMA \"{schemaName}\" TO \"{roleName}\"; " +
            $"GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE {string.Join(", ", tables)} TO \"{roleName}\";",
            ct);
    }

    private async Task ExecuteAdminSqlAsync(string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string ValidateRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role)
            || role.Length > 63
            || !char.IsAsciiLetter(role[0])
            || role.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new InvalidOperationException($"Nome do papel PostgreSQL de runtime inválido: '{role}'.");
        return role;
    }
}
