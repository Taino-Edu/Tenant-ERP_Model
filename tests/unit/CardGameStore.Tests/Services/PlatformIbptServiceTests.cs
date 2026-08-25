using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CardGameStore.Tests.Services;

public sealed class PlatformIbptServiceTests
{
    [Fact]
    public async Task ListarAsync_AgrupaTabelaRealDoPostgresPorUf()
    {
        var schema = TestDbFactory.IsolatedSchemaName(nameof(ListarAsync_AgrupaTabelaRealDoPostgresPorUf));
        await using var setup = new NpgsqlConnection(TestDbFactory.ConnectionString);
        await setup.OpenAsync();
        await using (var command = setup.CreateCommand())
        {
            command.CommandText = $"""
                CREATE SCHEMA "{schema}";
                CREATE TABLE "{schema}".ibpt_tabela (
                    id uuid PRIMARY KEY,
                    ncm varchar(8) NOT NULL,
                    uf varchar(2) NOT NULL,
                    importado boolean NOT NULL,
                    percentual_federal numeric NOT NULL,
                    percentual_estadual numeric NOT NULL,
                    percentual_municipal numeric NOT NULL,
                    fonte varchar(100),
                    versao varchar(30),
                    chave varchar(50),
                    vigencia_inicio timestamptz,
                    vigencia_fim timestamptz,
                    atualizado_em timestamptz NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            var connection = new NpgsqlConnectionStringBuilder(TestDbFactory.ConnectionString)
            {
                SearchPath = schema,
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseNpgsql(connection)
                .Options;
            await using var catalog = new CatalogDbContext(options);
            var now = DateTime.UtcNow;
            catalog.IbptTabela.AddRange(
                Entry("95044000", false, now.AddMinutes(-2)),
                Entry("95044000", true, now.AddMinutes(-1)),
                Entry("61091000", false, now));
            await catalog.SaveChangesAsync();

            var result = await new PlatformIbptService(
                catalog, NullLogger<PlatformIbptService>.Instance).ListarAsync();

            result.Should().ContainSingle();
            result[0].Uf.Should().Be("SP");
            result[0].Ncms.Should().Be(2);
            result[0].Versao.Should().Be("26.1.L");
            result[0].AtualizadoEm.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        }
        finally
        {
            await using var cleanup = setup.CreateCommand();
            cleanup.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;";
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static IbptTabelaEntry Entry(string ncm, bool importado, DateTime atualizadoEm) => new()
    {
        Ncm = ncm,
        Uf = "SP",
        Importado = importado,
        PercentualFederal = 12.5m,
        PercentualEstadual = 18m,
        PercentualMunicipal = 0m,
        Versao = "26.1.L",
        VigenciaInicio = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
        VigenciaFim = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
        AtualizadoEm = atualizadoEm,
    };
}
