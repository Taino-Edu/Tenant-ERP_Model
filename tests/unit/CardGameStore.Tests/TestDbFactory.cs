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
// Suba pelo compose, não com um `docker run` cru: ele já vem com folga de locks
// (cada DROP SCHEMA CASCADE daqui derruba ~134 objetos) e sem fsync, o que
// acelera muito o DDL da suíte. CheckDatabaseAvailable() avisa se o servidor
// estiver abaixo do mínimo, em vez de deixar isso virar erro obscuro mais tarde.
//
// O isolamento entre DUAS EXECUÇÕES SIMULTÂNEAS da suíte não vem daí, e sim do
// RunId por processo em IsolatedSchemaName: sem ele, dois processos usando o
// mesmo banco geravam os mesmos nomes de schema e um dropava o schema que o
// outro estava usando — testes aleatórios morriam com "42P01: relation does not
// exist" e o sintoma apontava pro código, não pra concorrência entre processos.
//
// Rodar os testes (com o container acima já no ar):
//   dotnet test tests/unit/CardGameStore.Tests/CardGameStore.Tests.csproj
//
// TEST_POSTGRES_CONNECTION sobrescreve a connection string default acima,
// caso o Postgres de teste esteja em outro host/porta/credenciais.
// =============================================================================

using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
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
    // Duas execuções locais da suíte podem compartilhar o mesmo PostgreSQL.
    // Sem um prefixo por processo, ambas dropavam schemas com o mesmo nome e
    // provocavam falhas aleatórias uma na outra.
    private static readonly string RunId =
        $"{Environment.ProcessId:x}_{Guid.NewGuid():N}"[..12];
    private static int SchemaSequence;

    static TestDbFactory()
    {
        // Os schemas exclusivos evitam colisão entre processos, mas não devem
        // se acumular no banco local depois de uma execução normal da suíte.
        //
        // Isto sozinho NÃO basta, e o motivo não é óbvio: o runtime dá um prazo
        // de ~2 segundos ao handler de ProcessExit e depois derruba o processo
        // no meio do que estiver fazendo. Dropar ~300 schemas, cada um com uns
        // 134 objetos em CASCADE, não cabe nesse prazo — então toda execução
        // normal deixa um resto para trás, medido em ~300 schemas por rodada.
        // É daí que vêm os 6.700 órfãos que já quebraram a suíte duas vezes.
        //
        // Por isso a varredura de verdade é CleanupOrphanSchemas, no INÍCIO da
        // execução seguinte, onde não há relógio correndo. Este handler continua
        // valendo pelo que consegue adiantar.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanupRunSchemas();
    }

    /// <summary>Connection string do Postgres de teste — exposta pra testes que
    /// precisam montar o próprio DbContext (ex: TenantIsolationTests, que usa o
    /// TenantConnectionInterceptor real de produção em vez do TestSchemaInterceptor).</summary>
    public static string ConnectionString => PgConnString;

    /// <summary>Nome de schema legível e único entre chamadas e processos
    /// concorrentes da suíte.
    ///
    /// A unicidade mora no PREFIXO (RunId + sequência), e é isso que torna a
    /// truncagem segura: identificador no Postgres tem limite de 63 bytes, e o
    /// corte em 60 descarta só a cauda descritiva. Um esquema em que o nome do
    /// teste viesse primeiro colidiria aqui — dois testes com o mesmo prefixo
    /// longo virariam o MESMO schema, e o DROP CASCADE de um apagaria as tabelas
    /// do outro no meio da execução.</summary>
    public static string IsolatedSchemaName(string logicalName)
    {
        var sequence = Interlocked.Increment(ref SchemaSequence);
        var schema = $"test_{RunId}_{sequence:x}_{Sanitize(logicalName)}";
        return schema.Length > 60 ? schema[..60] : schema;
    }

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
        var schema = IsolatedSchemaName(
            string.IsNullOrWhiteSpace(testName) ? Guid.NewGuid().ToString("N") : testName);

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

    /// <summary>Folga de locks exigida do servidor de teste. Cada DROP SCHEMA
    /// CASCADE aqui derruba ~134 objetos (tabelas, índices e sequences do
    /// AppDbContext) e pega um lock por objeto. O parâmetro dimensiona a tabela
    /// de locks compartilhada do cluster (max_locks_per_transaction ×
    /// max_connections), então o default de 64 não limita uma transação sozinha
    /// — mas manter o teto por transação abaixo do que a operação mais pesada da
    /// suíte usa de fato é deixar uma armadilha armada para o dia em que a
    /// concorrência aumentar.</summary>
    private const int MinLocksPerTransaction = 512;

    /// <summary>
    /// AVISO, nunca falha. A suíte roda bem com o default do Postgres — inclusive
    /// no CI, cujo service container não aceita override de configuração — porque
    /// max_locks_per_transaction dimensiona a tabela de locks COMPARTILHADA
    /// (valor × max_connections), e não o teto de uma transação sozinha. Isto aqui
    /// só sinaliza que a folga está menor do que a operação mais pesada da suíte
    /// usa, o que costuma significar container local criado antes do ajuste no
    /// compose (ou por um 'docker run' cru) — e portanto também sem fsync=off,
    /// que é de onde vem a maior parte do ganho de tempo.
    /// </summary>
    private static void EnsureLockCapacity(NpgsqlConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SHOW max_locks_per_transaction";
        if (!int.TryParse(cmd.ExecuteScalar() as string, out var locks) || locks >= MinLocksPerTransaction)
            return;

        Console.Error.WriteLine(
            $"[TestDbFactory] max_locks_per_transaction={locks} (recomendado: {MinLocksPerTransaction}). " +
            "A suíte roda assim mesmo; só está sem a folga e sem o fsync=off do compose. " +
            "Local, recrie com: docker compose -f tests/docker-compose.yml up -d --force-recreate --wait");
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
                EnsureLockCapacity(connection);
                CleanupOrphanSchemas(connection);
                return true;
            }
            catch (Exception exception) when (attempt < 6)
            {
                lastException = exception;
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
            }
            catch (Exception exception)
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

    /// <summary>Dropa schemas de execuções ANTERIORES que morreram sem passar
    /// pelo ProcessExit — rodada cancelada no meio, testhost derrubado, debug
    /// interrompido. Roda uma vez por processo, antes do primeiro Create().
    ///
    /// Sem isto o lixo se acumula em silêncio até o catálogo do Postgres ficar
    /// grande demais e o handshake do Npgsql estourar o Timeout=3 — e o sintoma
    /// não tem nenhuma relação com a causa: a suíte quebra em massa em testes
    /// que ninguém tocou (já aconteceu duas vezes, ~6.700 schemas órfãos).
    ///
    /// A limpeza é de melhor esforço e nunca falha a suíte: um erro aqui
    /// significa banco ocupado, não teste quebrado.</summary>
    private static void CleanupOrphanSchemas(NpgsqlConnection connection)
    {
        try
        {
            var schemas = new List<string>();
            using (var list = connection.CreateCommand())
            {
                // '_' é curinga no LIKE; escapado para não casar 'testX...'.
                list.CommandText = @"SELECT schema_name FROM information_schema.schemata
                                     WHERE schema_name LIKE 'test\_%'";
                using var reader = list.ExecuteReader();
                while (reader.Read()) schemas.Add(reader.GetString(0));
            }

            var removidos = 0;
            foreach (var schema in schemas.Where(DeExecucaoJaMorta))
            {
                using var drop = connection.CreateCommand();
                drop.CommandText =
                    $"DROP SCHEMA IF EXISTS {new NpgsqlCommandBuilder().QuoteIdentifier(schema)} CASCADE;";
                drop.ExecuteNonQuery();
                removidos++;
            }

            if (removidos > 0)
                Console.Error.WriteLine(
                    $"[TestDbFactory] {removidos} schema(s) de execuções anteriores removido(s).");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[TestDbFactory] limpeza de schemas órfãos ignorada: {exception.Message}");
        }
    }

    /// <summary>O schema pertence a uma execução que com certeza já terminou?
    ///
    /// O nome carrega o PID do testhost que o criou (ver RunId), então basta
    /// perguntar ao SO se aquele processo ainda existe. A dúvida sempre resolve
    /// para <c>false</c> — nome fora do padrão, processo vivo, ou PID reciclado
    /// por outro programa — porque o único erro caro aqui seria dropar o schema
    /// de uma segunda suíte rodando ao mesmo tempo, que a mataria com
    /// "relation does not exist". Deixar lixo para trás custa uma passada a mais
    /// na próxima execução; dropar o que está em uso custa uma investigação.</summary>
    internal static bool DeExecucaoJaMorta(string schema)
    {
        // test_{pid:x}_{guid}_{sequencia}_{nome-do-teste}
        var partes = schema.Split('_');
        if (partes.Length < 4
            || !int.TryParse(partes[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var pid)
            || pid <= 0)
            return false;

        if (pid == Environment.ProcessId) return false;

        try
        {
            using var processo = Process.GetProcessById(pid);
            return processo.HasExited;
        }
        catch (ArgumentException)
        {
            // Não existe processo com este id: a execução que criou o schema
            // acabou (normalmente ou não) e o schema ficou para trás.
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CleanupRunSchemas()
    {
        try
        {
            using var connection = new NpgsqlConnection(PgConnString);
            connection.Open();

            using var list = connection.CreateCommand();
            list.CommandText = """
                SELECT schema_name
                FROM information_schema.schemata
                WHERE left(schema_name, length(@prefix)) = @prefix
                """;
            list.Parameters.AddWithValue("prefix", $"test_{RunId}_");

            var schemas = new List<string>();
            using (var reader = list.ExecuteReader())
                while (reader.Read()) schemas.Add(reader.GetString(0));

            foreach (var schema in schemas)
            {
                using var drop = connection.CreateCommand();
                var quotedSchema = new NpgsqlCommandBuilder().QuoteIdentifier(schema);
                drop.CommandText = $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;";
                drop.ExecuteNonQuery();
            }
        }
        catch
        {
            // Limpeza de melhor esforço durante a saída do testhost: nunca
            // esconde o resultado real da suíte se o PostgreSQL já caiu.
        }
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
