// =============================================================================
// PlatformBillingEmissaoTests.cs — Emissão das cobranças da plataforma no gateway.
//
// O que custa dinheiro aqui é emitir a mesma cobrança duas vezes: é boleto/Pix
// real em dobro na mão do lojista. Com o botão "Emitir no Asaas agora" a rodada
// ganhou um segundo chamador além do job de 12 em 12 horas, e é isso que o
// primeiro teste trava.
//
// InMemory, como PlatformBillingServiceTests. Dois DbContexts com o mesmo nome
// de banco enxergam os mesmos dados, que é o que precisa para simular duas
// rodadas concorrentes.
// =============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class PlatformBillingEmissaoTests
{
    [Fact]
    public async Task DuasRodadasAoMesmoTempo_EmitemCadaCobrancaUmaVezSo()
    {
        var banco = Guid.NewGuid().ToString();
        await SemearAsync(banco, cobrancas: 3);
        var gateway = new GatewayDeTeste(demora: TimeSpan.FromMilliseconds(150));

        await using var db1 = Contexto(banco);
        await using var db2 = Contexto(banco);
        var resultados = await Task.WhenAll(
            Servico(db1, gateway).EmitirCobrancasPendentesAsync(),
            Servico(db2, gateway).EmitirCobrancasPendentesAsync());

        gateway.EmissoesPorCobranca.Should().HaveCount(3);
        gateway.EmissoesPorCobranca.Values.Should().OnlyContain(vezes => vezes == 1,
            "a segunda rodada (o botão, ou o job) não pode reemitir o que a primeira acabou de mandar");
        resultados.Sum(r => r.Emitidas).Should().Be(3);
    }

    [Fact]
    public async Task RodadaRepetidaDepoisDeTerminar_NaoEmiteDeNovo()
    {
        var banco = Guid.NewGuid().ToString();
        await SemearAsync(banco, cobrancas: 2);
        var gateway = new GatewayDeTeste(demora: TimeSpan.Zero);
        await using var db = Contexto(banco);
        var servico = Servico(db, gateway);

        var primeira = await servico.EmitirCobrancasPendentesAsync();
        var segunda  = await servico.EmitirCobrancasPendentesAsync();

        primeira.Emitidas.Should().Be(2);
        segunda.Emitidas.Should().Be(0);
        segunda.JaEmitidas.Should().Be(2);
        gateway.EmissoesPorCobranca.Values.Should().OnlyContain(vezes => vezes == 1);
    }

    [Fact]
    public async Task CobrancaEmitida_ApareceComLinkDaFaturaNoHistoricoDaLoja()
    {
        var banco = Guid.NewGuid().ToString();
        var tenantId = await SemearAsync(banco, cobrancas: 1);
        await using var db = Contexto(banco);
        var servico = Servico(db, new GatewayDeTeste(demora: TimeSpan.Zero));

        var antes = (await servico.ListarPorTenantAsync(tenantId)).Single();
        antes.EmitidaNoGateway.Should().BeFalse();
        antes.LinkPagamento.Should().BeNull();

        await servico.EmitirCobrancasPendentesAsync();

        var depois = (await servico.ListarPorTenantAsync(tenantId)).Single();
        depois.EmitidaNoGateway.Should().BeTrue();
        depois.LinkPagamento.Should().StartWith("https://sandbox.asaas.test/i/");
    }

    [Fact]
    public async Task SemGatewayConfigurado_NaoEmiteEDizPorQue()
    {
        var banco = Guid.NewGuid().ToString();
        await SemearAsync(banco, cobrancas: 1);
        await using var db = Contexto(banco);

        var resultado = await Servico(db, gateway: null).EmitirCobrancasPendentesAsync();

        resultado.Emitidas.Should().Be(0);
        resultado.Pendencias.Should().ContainSingle()
            .Which.Should().Contain("Nenhum gateway", "o botão precisa explicar por que não saiu nada");
    }

    // ── Apoio ────────────────────────────────────────────────────────────────

    private static CatalogDbContext Contexto(string banco) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase(banco).Options);

    private static PlatformBillingService Servico(CatalogDbContext db, IPlatformPaymentGateway? gateway) =>
        new(db, NullLogger<PlatformBillingService>.Instance, gateway: gateway);

    private static async Task<Guid> SemearAsync(string banco, int cobrancas)
    {
        await using var db = Contexto(banco);
        var tenant = new Tenant
        {
            Slug = "loja-teste", SchemaName = "tenant_loja_teste", Status = TenantStatus.Active,
            MonthlyPrice = 269m, BillingCnpj = "12345678000199", BillingCustomerId = "cus_teste",
            BillingStartsOn = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
        };
        db.Tenants.Add(tenant);
        for (var mes = 1; mes <= cobrancas; mes++)
        {
            db.TenantCharges.Add(new TenantCharge
            {
                TenantId       = tenant.Id,
                Kind           = TenantChargeKind.Mensalidade,
                Amount         = 269m,
                ReferenceMonth = new DateTime(2026, mes, 1, 0, 0, 0, DateTimeKind.Utc),
                DueDate        = new DateTime(2026, mes, 10, 0, 0, 0, DateTimeKind.Utc),
            });
        }
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private sealed class GatewayDeTeste(TimeSpan demora) : IPlatformPaymentGateway
    {
        public ConcurrentDictionary<Guid, int> EmissoesPorCobranca { get; } = new();

        public string Name => "teste";
        public bool IsConfigured => true;

        public Task<string> GarantirClienteAsync(Tenant tenant, CancellationToken ct = default) =>
            Task.FromResult("cus_teste");

        public async Task<CobrancaGatewayResult> EmitirCobrancaAsync(TenantCharge charge, Tenant tenant, CancellationToken ct = default)
        {
            // A demora abre a janela entre ler as pendentes e gravar o id externo —
            // exatamente onde duas rodadas simultâneas se atropelavam.
            await Task.Delay(demora, ct);
            EmissoesPorCobranca.AddOrUpdate(charge.Id, 1, (_, vezes) => vezes + 1);
            return new CobrancaGatewayResult($"pay_{charge.Id:N}", $"https://sandbox.asaas.test/i/{charge.Id:N}");
        }

        public bool ValidarAutenticacao(string? tokenRecebido) => true;

        public GatewayWebhookNotification? InterpretarWebhook(JsonElement payload) => null;
    }
}
