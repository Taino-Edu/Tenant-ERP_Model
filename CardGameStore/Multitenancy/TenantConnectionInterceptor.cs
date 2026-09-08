// =============================================================================
// TenantConnectionInterceptor.cs — Isolamento multi-tenant via search_path.
//
// Roda "SET search_path" assim que cada conexão física do AppDbContext abre,
// direcionando todas as queries da requisição pro schema do tenant resolvido
// (ITenantContext, scoped). Uma instância desta classe é criada por escopo —
// ver o registro em Program.cs, que resolve ITenantContext do próprio
// IServiceProvider scoped ao configurar as DbContextOptions.
//
// Invariante (ver AppDbContext/CatalogDbContext): nunca abrir NpgsqlConnection
// cru fora destes dois contextos, e nunca habilitar Multiplexing=true no
// Npgsql nem PgBouncer em modo transaction — ambos reusam a conexão física
// entre tenants sem passar por aqui, quebrando o isolamento silenciosamente.
// =============================================================================

using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CardGameStore.Multitenancy;

public class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TenantConnectionInterceptor> _logger;
    private readonly TenantDatabaseCredentials? _credentials;

    /// <param name="tenantContext">Tenant do escopo, lido a cada abertura de conexão.</param>
    /// <param name="logger">Diagnóstico de isolamento e de realinhamento de credencial.</param>
    /// <param name="credentials">
    /// Quando informado, a credencial da conexão é realinhada ao tenant no momento
    /// da abertura (ver AlignCredentials). Fica nulo no
    /// <see cref="TenantDatabaseAdmin"/>, que abre conexão com a credencial
    /// administrativa de propósito e não pode ser rebaixada pro papel do tenant.
    /// </param>
    public TenantConnectionInterceptor(ITenantContext tenantContext, ILogger<TenantConnectionInterceptor> logger,
        TenantDatabaseCredentials? credentials = null)
    {
        _tenantContext = tenantContext;
        _logger        = logger;
        _credentials   = credentials;
    }

    public override InterceptionResult ConnectionOpening(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        // Valida antes de o provider tentar alcançar o PostgreSQL. Além de impedir
        // que um identificador inválido chegue ao SQL, isso preserva o fail-fast
        // mesmo quando o servidor está indisponível ou o pool está sob pressão.
        ValidateSchemaName();
        AlignCredentials(connection);
        return base.ConnectionOpening(connection, eventData, result);
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        ValidateSchemaName();
        AlignCredentials(connection);
        return base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Garante que a conexão abra com o papel PostgreSQL do tenant ATUAL, e não
    /// com o que valia quando as DbContextOptions foram montadas.
    ///
    /// POR QUE É NECESSÁRIO
    /// A connection string é resolvida uma vez, quando o AppDbContext é criado no
    /// escopo (Program.cs, `ConnectionStringFor(ITenantContext.TenantId)`). Isso
    /// assume que o tenant já está definido nesse instante — verdade num request
    /// HTTP, onde o middleware roda antes de qualquer controller.
    ///
    /// No SignalR não é. O hub é CONSTRUÍDO antes de o TenantHubFilter rodar, e
    /// construir o hub já resolve IComandaService e, com ele, o AppDbContext —
    /// ainda na tenant-zero. O filtro então corrige o ITenantContext, e o
    /// resultado era o pior meio-termo possível: `SET search_path` no schema
    /// certo, com a credencial do tenant-zero, que não tem USAGE no schema do
    /// tenant. `current_schema()` voltava vazio e TODA conexão de hub em loja
    /// real morria — o painel "LIVE" nunca atualizava fora da tenant-zero, que é
    /// justamente onde se desenvolve.
    ///
    /// Alinhar aqui conserta a classe inteira do problema, não só o hub: qualquer
    /// escopo que chame Set() depois de o DbContext existir passa a abrir com a
    /// credencial certa. O schema já era resolvido na abertura; agora a
    /// credencial também.
    ///
    /// Trocar a connection string com a conexão FECHADA é operação suportada, e o
    /// pool do Npgsql é por string — a conexão sai e volta pro pool do papel certo.
    /// </summary>
    private void AlignCredentials(DbConnection connection)
    {
        if (_credentials is null || connection.State != ConnectionState.Closed) return;

        var esperada = _credentials.ConnectionStringFor(_tenantContext.TenantId);

        // Compara o usuário, não a string inteira: o Npgsql normaliza a string e
        // uma comparação textual acusaria diferença a cada abertura.
        var atual = SafeUsername(connection.ConnectionString);
        var alvo  = SafeUsername(esperada);
        if (string.Equals(atual, alvo, StringComparison.Ordinal)) return;

        _logger.LogDebug(
            "Realinhando credencial da conexão de '{Atual}' para '{Alvo}' (tenant {TenantId}).",
            atual, alvo, _tenantContext.TenantId);
        connection.ConnectionString = esperada;
    }

    private static string? SafeUsername(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        try { return new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Username; }
        catch (ArgumentException) { return null; }
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await SetSearchPathAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        // EF Core abre conexão de forma síncrona em alguns caminhos (ex: Database.OpenConnection()).
        // Versão síncrona dedicada — nunca bloquear sobre a Task async aqui, isso
        // esgota o thread pool sob carga (sync-over-async).
        SetSearchPath(connection);
        base.ConnectionOpened(connection, eventData);
    }

    private void SetSearchPath(DbConnection connection)
    {
        var schema = ValidateSchemaName();

        using var setCmd = connection.CreateCommand();
        // Sem fallback pro "public" — ver comentário em SetSearchPathAsync.
        setCmd.CommandText = $"SET search_path TO \"{schema}\";";
        setCmd.ExecuteNonQuery();

        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT current_schema();";
        // "as string" (não cast direto): se o schema não existir, current_schema()
        // retorna NULL SQL (DBNull) — o cast direto estourava InvalidCastException
        // antes da mensagem de diagnóstico do LogAndVerify (bug pego pelos
        // TenantIsolationTests).
        var current = checkCmd.ExecuteScalar() as string;

        LogAndVerify(schema, current);
    }

    private async Task SetSearchPathAsync(DbConnection connection, CancellationToken ct)
    {
        var schema = ValidateSchemaName();

        await using var setCmd = connection.CreateCommand();
        // SEM fallback pro "public" no search_path: "public" aqui é o schema de
        // dados de verdade do tenant-zero (não um schema "compartilhado" de
        // extensões/funções). Um fallback faria qualquer tabela ausente no
        // schema do tenant (ex: logo após CREATE SCHEMA, antes da migration
        // rodar) resolver silenciosamente pra tabela de public via busca de
        // nome do Postgres — inclusive a própria "__EFMigrationsHistory", o que
        // faz o EF achar "já migrado" e nunca criar nada no schema novo (bug
        // real encontrado ao provisionar o primeiro tenant nesta sessão).
        setCmd.CommandText = $"SET search_path TO \"{schema}\";";
        await setCmd.ExecuteNonQueryAsync(ct);

        // Rede de segurança barata: confirma que o search_path realmente apontou
        // pro schema esperado. Pega cedo qualquer regressão silenciosa (ex:
        // multiplexing/PgBouncer transaction mode reaproveitando a conexão física
        // entre tenants sem passar por este interceptor).
        await using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT current_schema();";
        // "as string" em vez de cast direto — ver comentário na versão síncrona.
        var current = await checkCmd.ExecuteScalarAsync(ct) as string;

        LogAndVerify(schema, current);
    }

    private string ValidateSchemaName()
    {
        // C3: todo caminho legítimo do código (TenantResolutionMiddleware, background
        // services via ForEachActiveTenantAsync, scopes manuais em controllers/services,
        // boot do Program.cs) chama ITenantContext.Set(...) explicitamente antes de tocar
        // no AppDbContext — inclusive pra tenant-zero. Um scope que abre conexão sem nunca
        // ter chamado Set() é sempre bug (esqueceram de propagar o tenant), nunca uso
        // intencional do default — cai silenciosamente no schema "public" sem avisar
        // ninguém, causa raiz de bugs reais já encontrados (C1/C2 na auditoria). Falha
        // rápido em vez de gravar dado no tenant errado.
        if (!_tenantContext.IsExplicitlySet)
            throw new InvalidOperationException(
                "ITenantContext.Set(...) nunca foi chamado neste escopo antes de abrir uma conexão — " +
                "provável bug de propagação de tenant (CreateScope() sem Set() antes de resolver o AppDbContext).");

        var schema = TenantSchemaName.Validate(_tenantContext.SchemaName);

        // O nome do schema só vem do catálogo de tenants ou da constante de
        // tenant-zero — nunca de input livre de usuário. Ainda assim validamos o
        // formato antes de interpolar no SQL, porque search_path não aceita
        // parâmetro bind (é uma configuração de sessão, não uma query parametrizável).
        if (!TenantSchemaName.IsValid(schema))
            throw new InvalidOperationException($"Nome de schema de tenant inválido: '{schema}'.");

        return schema;
    }

    private void LogAndVerify(string schema, string? current)
    {
        _logger.LogDebug("Conexão isolada no schema '{Schema}' (current_schema() = '{Current}').", schema, current);

        if (!string.Equals(current, schema, StringComparison.Ordinal))
        {
            _logger.LogError(
                "Isolamento de tenant comprometido: search_path pedido foi '{Expected}' mas current_schema() " +
                "retornou '{Actual}'. Verifique se o schema existe e se não há multiplexing/PgBouncer " +
                "transaction mode na conexão.", schema, current);
            throw new InvalidOperationException(
                $"Falha ao isolar conexão no schema '{schema}' (current_schema() = '{current}').");
        }
    }

}
