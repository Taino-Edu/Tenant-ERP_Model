// =============================================================================
// TestDbFactory.cs — Fábrica única de AppDbContext pros testes, sempre contra
// Postgres real (schema isolado por teste, dentro do mesmo banco).
//
// SQLite foi removido de propósito: ele é tolerante demais em pontos onde o
// Postgres não é (ex: aceitava DateTime com Kind=Unspecified em coluna
// timestamptz, que o Npgsql rejeita), e isso já deixou passar um bug real de
// produção (financeiro/fechamento automático quebrados silenciosamente) que a
// suíte inteira, 100% em SQLite, nunca teria pego.
//
// Setup (uma vez só, container fica de pé indefinidamente):
//   podman run -d --name tenant-erp-test-db -e POSTGRES_USER=tenant_test \
//     -e POSTGRES_PASSWORD=tenant_test_pw -e POSTGRES_DB=tenant_erp_test \
//     -p 5433:5432 docker.io/library/postgres:16-alpine
//
// Rodar os testes (com o container acima já no ar):
//   dotnet test tests/unit/CardGameStore.Tests/CardGameStore.Tests.csproj
//
// TEST_POSTGRES_CONNECTION sobrescreve a connection string default acima,
// caso o Postgres de teste esteja em outro host/porta/credenciais.
// =============================================================================

using System.Data.Common;
using System.Text.RegularExpressions;
using CardGameStore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CardGameStore.Tests;

public static class TestDbFactory
{
    private const string DefaultConnString =
        "Host=127.0.0.1;Port=5433;Database=tenant_erp_test;Username=tenant_test;Password=tenant_test_pw";

    private static readonly string PgConnString =
        Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION") is { Length: > 0 } env
            ? env
            : DefaultConnString;

    /// <summary>Cria um AppDbContext isolado pra um teste — schema próprio,
    /// dropado e recriado vazio, dentro do mesmo banco Postgres de teste.
    /// <paramref name="testName"/> vira o nome do schema (mesma string que os
    /// testes já passam via nameof(MetodoDoTeste), reaproveitada pra isolar em
    /// vez de só documentar).</summary>
    public static AppDbContext Create(string testName = "")
    {
        var schema = "test_" + Sanitize(string.IsNullOrWhiteSpace(testName) ? Guid.NewGuid().ToString("N") : testName);
        // Trunca — identificador de schema no Postgres tem limite de 63 bytes.
        if (schema.Length > 60) schema = schema[..60];

        // DROP/CREATE roda numa conexão descartável, fechada logo em seguida —
        // não pode ser a mesma conexão do DbContext (ver comentário abaixo).
        using (var setup = new NpgsqlConnection(PgConnString))
        {
            setup.Open();
            using var cmd = setup.CreateCommand();
            // DROP antes de CREATE: reexecuções locais da mesma classe de teste
            // (schema com o mesmo nome) não acumulam lixo de uma rodada anterior.
            cmd.CommandText =
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE; " +
                $"CREATE SCHEMA \"{schema}\";";
            cmd.ExecuteNonQuery();
        }

        // Importante: passar a MESMA connection string (PgConnString, sem
        // schema embutido) pra todo teste, não uma com "Search Path" único por
        // teste — isso foi tentado primeiro e criava um pool Npgsql SEPARADO
        // por teste (cada connection string distinta = pool distinto), e cada
        // pool mantém pelo menos 1 conexão física ociosa por um tempo depois de
        // usada; com ~190 testes isso estourava o "max_connections" do próprio
        // servidor Postgres (não é limite do client, do servidor) em segundos.
        // Em vez disso, um único pool compartilhado (mesma connection string
        // sempre) + um DbConnectionInterceptor que roda "SET search_path" a
        // cada Open() lógico — exatamente o mesmo padrão que
        // TenantConnectionInterceptor usa em produção pra isolar tenants sem
        // multiplicar pools.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(PgConnString)
            .AddInterceptors(new TestSchemaInterceptor(schema))
            .Options;
        var db = new AppDbContext(options);
        // NÃO usar Database.EnsureCreated(): o "database já tem tabelas?" dele
        // não é escopado por schema — ele acusa "sim" (e pula toda criação) se
        // QUALQUER schema no banco tiver tabelas, mesmo de outro teste/schema
        // completamente diferente. Resultado real observado: com schemas de
        // testes anteriores ainda no banco, EnsureCreated() silenciosamente não
        // criava nada no schema novo (recém-criado, vazio) e todo INSERT/SELECT
        // subsequente falhava com "relation ... does not exist". CreateTables()
        // gera e roda o script de criação incondicionalmente — certo aqui porque
        // o schema acima acabou de ser dropado+recriado vazio.
        db.GetInfrastructure().GetRequiredService<IRelationalDatabaseCreator>().CreateTables();
        return db;
    }

    private static string Sanitize(string s) =>
        Regex.Replace(s, "[^a-zA-Z0-9_]", "_").ToLowerInvariant();
}

/// <summary>Fixa o search_path pro schema deste teste em toda conexão física
/// alugada do pool compartilhado — mesmo papel do TenantConnectionInterceptor
/// de produção, só que o schema vem de um valor fechado no construtor (closure)
/// em vez de um ITenantContext resolvido via DI.</summary>
internal sealed class TestSchemaInterceptor(string schema) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetSearchPath(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await SetSearchPathAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void SetSearchPath(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET search_path TO \"{schema}\";";
        cmd.ExecuteNonQuery();
    }

    private async Task SetSearchPathAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET search_path TO \"{schema}\";";
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
