// =============================================================================
// TenantIsolationTests.cs — Testes de integração do coração do multi-tenant:
// o isolamento por schema via TenantConnectionInterceptor REAL de produção
// (não o TestSchemaInterceptor do TestDbFactory), contra Postgres real.
//
// Cobre: dado gravado no schema do tenant A é invisível pro tenant B; troca de
// tenant no mesmo escopo redireciona conexões novas; nome de schema inválido é
// rejeitado antes de tocar o SQL; e a rede de segurança do current_schema()
// dispara quando o schema não existe.
// =============================================================================

using Xunit;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Multitenancy;

public class TenantIsolationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>AppDbContext ligado ao TenantConnectionInterceptor REAL, lendo o
    /// schema do ITenantContext — o mesmo caminho de código de produção.</summary>
    private static AppDbContext CreateDbFor(ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestDbFactory.ConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(
                tenant, NullLogger<TenantConnectionInterceptor>.Instance))
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Dropa/recria o schema e cria as tabelas do AppDbContext dentro
    /// dele, já passando pelo interceptor real (CreateTables gera CREATE TABLE
    /// sem qualificador de schema — as tabelas caem no search_path corrente,
    /// exatamente como no provisionamento de tenant de produção).</summary>
    private static ITenantContext ProvisionSchema(string schema)
    {
        TestDbFactory.ResetSchema(schema);

        var tenant = new TenantContext();
        tenant.Set(Guid.NewGuid(), schema, ["fiscal"]);

        using var db = CreateDbFor(tenant);
        db.GetInfrastructure().GetRequiredService<IRelationalDatabaseCreator>().CreateTables();
        return tenant;
    }

    private static Product NovoProduto(string nome) => new()
    {
        Id            = Guid.NewGuid(),
        Name          = nome,
        Category      = "Geral",
        PriceInCents  = 1000,
        StockQuantity = 5,
        MinimumStock  = 1,
        IsActive      = true,
    };

    // ── Isolamento entre tenants ──────────────────────────────────────────────

    [Fact]
    public async Task DadoGravadoNoTenantA_NaoApareceNoTenantB()
    {
        var tenantA = ProvisionSchema("mt_iso_tenant_a");
        var tenantB = ProvisionSchema("mt_iso_tenant_b");

        await using (var dbA = CreateDbFor(tenantA))
        {
            dbA.Products.Add(NovoProduto("Produto Exclusivo do Tenant A"));
            await dbA.SaveChangesAsync();
        }

        await using (var dbB = CreateDbFor(tenantB))
        {
            var produtosDeB = await dbB.Products.ToListAsync();
            produtosDeB.Should().BeEmpty(
                "o schema do tenant B foi recém-criado e nada foi gravado nele — " +
                "ver o produto do tenant A aqui seria vazamento de isolamento");
        }

        await using (var dbA2 = CreateDbFor(tenantA))
        {
            var produtosDeA = await dbA2.Products.ToListAsync();
            produtosDeA.Should().ContainSingle(p => p.Name == "Produto Exclusivo do Tenant A",
                "o dado precisa continuar visível pro próprio tenant que gravou");
        }
    }

    [Fact]
    public async Task TrocarTenantNoMesmoEscopo_RedirecionaConexoesNovas()
    {
        // Em produção o ITenantContext é scoped e o interceptor lê dele a cada
        // conexão aberta — se o contexto mudar (ex: loop de migrations por tenant
        // no boot), conexões novas têm que cair no schema novo.
        var tenantA = ProvisionSchema("mt_swap_tenant_a");
        ProvisionSchema("mt_swap_tenant_b");

        await using (var dbA = CreateDbFor(tenantA))
        {
            dbA.Products.Add(NovoProduto("Só no A"));
            await dbA.SaveChangesAsync();
        }

        // Mesmo ITenantContext, agora apontando pro schema B
        tenantA.Set(Guid.NewGuid(), "mt_swap_tenant_b", ["fiscal"]);

        await using var dbDepoisDaTroca = CreateDbFor(tenantA);
        var produtos = await dbDepoisDaTroca.Products.ToListAsync();
        produtos.Should().BeEmpty("depois do Set() o interceptor deve isolar no schema novo (B), que está vazio");
    }

    // ── Validações defensivas do interceptor ──────────────────────────────────

    [Fact]
    public void EscopoQueNuncaChamouSet_FalhaRapidoAoAbrirConexao()
    {
        // C3: um ITenantContext "cru" (nunca teve Set() chamado) não pode abrir conexão
        // silenciosamente no default (tenant-zero/public) — todo caminho legítimo do
        // código (middleware, background services, scopes manuais) sempre chama Set()
        // explicitamente, mesmo pra tenant-zero. Abrir conexão sem isso é bug de
        // propagação de tenant, não uso intencional do default.
        var tenantNuncaSetado = new TenantContext();

        using var db = CreateDbFor(tenantNuncaSetado);
        var abrirConexao = () => db.Database.OpenConnection();

        abrirConexao.Should().Throw<InvalidOperationException>()
            .WithMessage("*Set(*", "o interceptor deve barrar antes de sequer tentar o SET search_path");
    }

    [Theory]
    [InlineData("tenant; DROP SCHEMA public")] // injeção via separador SQL
    [InlineData("tenant-com-hifen")]           // hífen não passa na allowlist
    [InlineData("1comeca_com_digito")]
    [InlineData("")]
    public void SchemaComNomeInvalido_EhRejeitadoAntesDoSql(string schemaInvalido)
    {
        var tenant = new TenantContext();
        tenant.Set(Guid.NewGuid(), schemaInvalido, ["fiscal"]);

        using var db = CreateDbFor(tenant);
        var abrirConexao = () => db.Database.OpenConnection();

        abrirConexao.Should().Throw<InvalidOperationException>()
            .WithMessage("*inválido*", "a allowlist de nome de schema deve barrar antes de interpolar no SET search_path");
    }

    [Fact]
    public void SchemaInexistente_DisparaRedeDeSegurancaDoCurrentSchema()
    {
        // "SET search_path TO schema_que_nao_existe" NÃO é erro no Postgres — a
        // proteção real é o SELECT current_schema() do interceptor detectar que o
        // isolamento não aconteceu e abortar em vez de deixar queries correrem
        // soltas (potencialmente resolvendo tabelas de outro schema).
        var tenant = new TenantContext();
        tenant.Set(Guid.NewGuid(), "mt_schema_fantasma_que_nao_existe", ["fiscal"]);

        using var db = CreateDbFor(tenant);
        var abrirConexao = () => db.Database.OpenConnection();

        abrirConexao.Should().Throw<InvalidOperationException>()
            .WithMessage("*Falha ao isolar*");
    }
}
