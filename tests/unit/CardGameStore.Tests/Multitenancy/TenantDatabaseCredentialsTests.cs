using CardGameStore.Multitenancy;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace CardGameStore.Tests.Multitenancy;

public class TenantDatabaseCredentialsTests
{
    [Fact]
    public void ConnectionStringFor_AplicaLimitesDoPoolPorTenant()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgreSQLAdmin"] = "Host=localhost;Database=test;Username=owner;Password=secret",
            ["Database:TenantCredentialKey"] = "test-key-with-at-least-32-characters",
            ["Database:TenantMaxPoolSize"] = "4",
            ["Database:ConnectionIdleLifetimeSeconds"] = "45",
        }).Build();

        var credentials = new TenantDatabaseCredentials(config);
        var connection = new NpgsqlConnectionStringBuilder(credentials.ConnectionStringFor(Guid.NewGuid()));

        connection.MinPoolSize.Should().Be(0);
        connection.MaxPoolSize.Should().Be(4);
        connection.ConnectionIdleLifetime.Should().Be(45);
        connection.ConnectionPruningInterval.Should().Be(10);
        connection.Timeout.Should().Be(10);
        connection.CommandTimeout.Should().Be(30);
    }
}
