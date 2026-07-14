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

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CardGameStore.Multitenancy;

public class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TenantConnectionInterceptor> _logger;

    public TenantConnectionInterceptor(ITenantContext tenantContext, ILogger<TenantConnectionInterceptor> logger)
    {
        _tenantContext = tenantContext;
        _logger        = logger;
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
        var schema = _tenantContext.SchemaName;

        // O nome do schema só vem do catálogo de tenants ou da constante de
        // tenant-zero — nunca de input livre de usuário. Ainda assim validamos o
        // formato antes de interpolar no SQL, porque search_path não aceita
        // parâmetro bind (é uma configuração de sessão, não uma query parametrizável).
        if (!IsValidSchemaName(schema))
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

    private static bool IsValidSchemaName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= 63
        && !char.IsDigit(name[0])
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}
