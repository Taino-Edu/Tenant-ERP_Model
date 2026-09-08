using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CardGameStore.Tests.Data;

public sealed class SaleIdempotencyMigrationTests
{
    [Fact]
    public async Task Migration_ConsolidatesLegacyOpenCreditsBeforeUniqueIndex()
    {
        var schema = TestDbFactory.IsolatedSchemaName(nameof(Migration_ConsolidatesLegacyOpenCreditsBeforeUniqueIndex));
        TestDbFactory.ResetSchema(schema);
        var connection = new NpgsqlConnectionStringBuilder(TestDbFactory.ConnectionString)
        {
            SearchPath = schema,
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .Options;

        try
        {
            await using var db = new AppDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260908123525_AddCrediarioEmailOutbox");
            var user = new User { Name = "Legado", PasswordHash = "hash", Role = UserRole.Customer };
            db.Users.Add(user);
            var oldest = new Crediario
            {
                UserId = user.Id, ValorEmCentavos = 1000, ValorPagoEmCentavos = 100,
                DataAbertura = DateTime.UtcNow.AddDays(-5), DataVencimento = DateTime.UtcNow.AddDays(25),
                Status = CrediariosStatus.Aberto, AbertoPorAdminId = Guid.NewGuid(),
                ItensJson = "[{\"ItemName\":\"A\",\"Quantity\":1,\"UnitPriceInReais\":10,\"SubtotalInReais\":10}]",
            };
            var newest = new Crediario
            {
                UserId = user.Id, ValorEmCentavos = 700, ValorPagoEmCentavos = 200,
                DataAbertura = DateTime.UtcNow.AddDays(-2), DataVencimento = DateTime.UtcNow.AddDays(28),
                Status = CrediariosStatus.Aberto, AbertoPorAdminId = Guid.NewGuid(),
                ItensJson = "[{\"ItemName\":\"B\",\"Quantity\":1,\"UnitPriceInReais\":7,\"SubtotalInReais\":7}]",
            };
            db.Crediarios.AddRange(oldest, newest);
            await db.SaveChangesAsync();

            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();

            var merged = await db.Crediarios.SingleAsync(c => c.UserId == user.Id);
            merged.Id.Should().Be(oldest.Id);
            merged.ValorEmCentavos.Should().Be(1700);
            merged.ValorPagoEmCentavos.Should().Be(300);
            merged.ItensJson.Should().Contain("A").And.Contain("B");
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(TestDbFactory.ConnectionString);
            await cleanup.OpenAsync();
            await using var command = cleanup.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await command.ExecuteNonQueryAsync();
        }
    }
}
