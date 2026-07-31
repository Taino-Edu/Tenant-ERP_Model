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
//   docker compose -f tests/docker-compose.yml up -d --wait
//
// Suba SEMPRE por esse compose, não com um `docker run` cru: o servidor precisa
// de max_locks_per_transaction bem acima do default (64). Cada DROP SCHEMA
// CASCADE desta fábrica pega um lock por objeto — ~60 tabelas mais índices — e
// com o default a tabela de locks do cluster estoura sob concorrência, deixando
// schemas pela metade e fazendo testes aleatórios morrerem com
// "42P01: relation does not exist". Ver o comentário em tests/docker-compose.yml.
// A checagem em CheckDatabaseAvailable() avisa se o servidor estiver abaixo do
// mínimo, pra ninguém perder tempo caçando isso de novo.
//
// Rodar os testes (com o container acima já no ar):
//   dotnet test tests/unit/CardGameStore.Tests/CardGameStore.Tests.csproj
//
// TEST_POSTGRES_CONNECTION sobrescreve a connection string default acima,
// caso o Postgres de teste esteja em outro host/porta/credenciais.
// =============================================================================

using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
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
        "Host=127.0.0.1;Port=5433;Database=tenant_erp_test;Username=tenant_test;Password=tenant_test_pw;Timeout=3";

    private static readonly string PgConnString =
        Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION") is { Length: > 0 } env
            ? env
            : DefaultConnString;
    private static readonly Lazy<bool> DatabaseAvailable = new(CheckDatabaseAvailable);

    /// <summary>Connection string do Postgres de teste — exposta pra testes que
    /// precisam montar o próprio DbContext (ex: TenantIsolationTests, que usa o
    /// TenantConnectionInterceptor real de produção em vez do TestSchemaInterceptor).</summary>
    public static string ConnectionString => PgConnString;

    /// <summary>Dropa e recria um schema vazio no banco de teste — mesmo preparo
    /// que Create() faz, exposto pra testes que gerenciam o próprio contexto.</summary>
    public static void ResetSchema(string schema)
    {
        _ = DatabaseAvailable.Value;
        using var setup = new NpgsqlConnection(PgConnString);
        setup.Open();
        using var cmd = setup.CreateCommand();
        cmd.CommandText =
            $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE; " +
            $"CREATE SCHEMA \"{schema}\";";
        cmd.ExecuteNonQuery();
    }

    /// <summary>Cria um AppDbContext isolado pra um teste — schema próprio,
    /// dropado e recriado vazio, dentro do mesmo banco Postgres de teste.
    /// <paramref name="testName"/> vira o nome do schema (mesma string que os
    /// testes já passam via nameof(MetodoDoTeste), reaproveitada pra isolar em
    /// vez de só documentar).</summary>
    public static AppDbContext Create(string testName = "")
    {
        _ = DatabaseAvailable.Value;
        var schema = SchemaNameFor(testName);

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

    /// <summary>
    /// Nome do schema deste teste. Determinístico de propósito: o mesmo teste
    /// reaproveita o mesmo schema entre execuções (ele é dropado e recriado),
    /// então o total de schemas no banco de teste converge pro número de testes
    /// em vez de crescer sem parar — catálogo inchado deixa o DDL da suíte
    /// progressivamente mais lento.
    ///
    /// Nomes longos exigem cuidado: identificador no Postgres tem limite de 63
    /// bytes, e o corte seco em 60 caracteres que existia aqui fazia dois nomes
    /// de teste com o mesmo prefixo virarem O MESMO schema — aí o DROP CASCADE
    /// de um apagava as tabelas do outro. O corte agora carrega um hash do nome
    /// completo, então nomes diferentes continuam diferentes depois de truncados.
    ///
    /// (A causa das falhas intermitentes de "42P01: relation does not exist" que
    /// a suíte teve não era esta, e sim `max_locks_per_transaction` no servidor —
    /// ver tests/docker-compose.yml. Mas a colisão por truncagem era real e
    /// produzia exatamente o mesmo sintoma, então fica corrigida.)
    /// </summary>
    private static string SchemaNameFor(string testName)
    {
        var name = "test_" + Sanitize(
            string.IsNullOrWhiteSpace(testName) ? Guid.NewGuid().ToString("N") : testName);

        const int max = 60;
        if (name.Length <= max) return name;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8].ToLowerInvariant();
        return name[..(max - hash.Length - 1)] + "_" + hash;
    }

    private static string Sanitize(string s) =>
        Regex.Replace(s, "[^a-zA-Z0-9_]", "_").ToLowerInvariant();

    /// <summary>Mínimo de locks por transação exigido do servidor de teste. Cada
    /// DROP SCHEMA CASCADE aqui derruba ~60 tabelas mais índices e sequences, e a
    /// tabela de locks é compartilhada pelo cluster inteiro
    /// (max_locks_per_transaction × max_connections). Com o default 64 do
    /// Postgres, o DROP/CREATE falha no meio sob concorrência e o teste seguinte
    /// morre com "42P01: relation does not exist" — sintoma que aponta pro código
    /// e não pro servidor, e que já custou uma investigação inteira.</summary>
    private const int MinLocksPerTransaction = 512;

    private static void EnsureLockCapacity(NpgsqlConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SHOW max_locks_per_transaction";
        if (!int.TryParse(cmd.ExecuteScalar() as string, out var locks) || locks >= MinLocksPerTransaction)
            return;

        throw new InvalidOperationException(
            $"O PostgreSQL de testes está com max_locks_per_transaction={locks}, abaixo do mínimo " +
            $"de {MinLocksPerTransaction}. Nessa configuração a suíte falha de forma intermitente e " +
            "enganosa: alguns testes morrem com \"42P01: relation does not exist\" porque o " +
            "DROP SCHEMA CASCADE deles esgotou a tabela de locks do cluster. " +
            "Recrie o container pelo compose, que já traz o ajuste: " +
            "'docker compose -f tests/docker-compose.yml up -d --force-recreate --wait'.");
    }

    private static bool CheckDatabaseAvailable()
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            try
            {
                using var connection = new NpgsqlConnection(PgConnString);
                connection.Open();
                // Fora do retry: um servidor mal configurado não fica bom esperando,
                // e a mensagem dele é específica — não pode virar "banco inacessível".
                EnsureLockCapacity(connection);
                return true;
            }
            catch (Exception exception) when (exception is not InvalidOperationException && attempt < 6)
            {
                lastException = exception;
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
            }
            catch (Exception exception) when (exception is not InvalidOperationException)
            {
                lastException = exception;
            }
        }

        throw new InvalidOperationException(
            "O PostgreSQL de testes não está acessível. Execute " +
            "'docker compose -f tests/docker-compose.yml up -d --wait' " +
            "ou configure TEST_POSTGRES_CONNECTION antes de rodar a suíte.",
            lastException);
    }
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
