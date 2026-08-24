using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace CardGameStore.Multitenancy;

/// <summary>Gera credenciais determinísticas e exclusivas por tenant. A senha
/// não é persistida no catálogo: deriva de uma chave secreta do ambiente.</summary>
public sealed class TenantDatabaseCredentials
{
    private readonly string _adminConnectionString;
    private readonly byte[] _key;
    private readonly int _maxPoolSize;
    private readonly int _connectionIdleLifetime;

    public TenantDatabaseCredentials(IConfiguration configuration)
    {
        _adminConnectionString = configuration.GetConnectionString("PostgreSQLAdmin")
            ?? throw new InvalidOperationException("ConnectionStrings:PostgreSQLAdmin não configurada.");
        var keyText = configuration["Database:TenantCredentialKey"];
        if (string.IsNullOrWhiteSpace(keyText) || keyText.Length < 32)
            throw new InvalidOperationException(
                "Database:TenantCredentialKey deve ter pelo menos 32 caracteres.");
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyText));
        _maxPoolSize = Math.Clamp(configuration.GetValue("Database:TenantMaxPoolSize", 5), 1, 20);
        _connectionIdleLifetime = Math.Clamp(
            configuration.GetValue("Database:ConnectionIdleLifetimeSeconds", 60), 10, 300);
    }

    public string RoleFor(Guid tenantId) => $"tenant_r_{tenantId:N}";

    public string PasswordFor(Guid tenantId)
    {
        using var hmac = new HMACSHA256(_key);
        return Convert.ToHexString(hmac.ComputeHash(tenantId.ToByteArray())).ToLowerInvariant();
    }

    public string ConnectionStringFor(Guid tenantId)
    {
        var cs = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Username = RoleFor(tenantId),
            Password = PasswordFor(tenantId),
            MinPoolSize = 0,
            MaxPoolSize = _maxPoolSize,
            ConnectionIdleLifetime = _connectionIdleLifetime,
            ConnectionPruningInterval = Math.Min(10, _connectionIdleLifetime),
            Timeout = 10,
            CommandTimeout = 30,
        };
        return cs.ConnectionString;
    }
}
