// =============================================================================
// PlatformBillingReativacaoEAvisosTests.cs — O lojista que paga volta na hora,
// e o que está para ser suspenso fica sabendo antes.
//
// Protege: loja paga fora do ar esperando o job de 12 horas; cache do
// middleware mantendo "suspensa" depois da reativação; o mesmo aviso de
// suspensão chegando a cada rodada; aviso saindo pelo SMTP da loja em vez do
// da plataforma.
// =============================================================================

using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CardGameStore.Tests.Services;

public class PlatformBillingReativacaoEAvisosTests
{
    private static readonly DateTime Hoje = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

    private static CatalogDbContext CreateDb(string? nome = null) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase(nome ?? Guid.NewGuid().ToString()).Options);

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Billing:DiasDeCarenciaAposVencimento"] = "7",
            ["SmtpSettings:AppUrl"] = "https://3esysten.com.br",
            ["Multitenancy:RootDomain"] = "3esysten.com.br",
        })
        .Build();

    private static Tenant LojaSuspensaPorAtraso() => new()
    {
        Slug = "loja-devendo", SchemaName = "tenant_loja_devendo", DisplayName = "Loja Devendo",
        Status = TenantStatus.Suspended, PaymentStatus = TenantPaymentStatus.Atrasado,
        MonthlyPrice = 129m, BillingEmail = "dono@loja.test", BillingCnpj = "11222333000181",
        BillingStartsOn = Hoje.AddMonths(-3),
    };

    // ── Reativação imediata ──────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_PagamentoDaUnicaDivida_ReativaNaHoraELimpaOCache()
    {
        using var db = CreateDb();
        var tenant = LojaSuspensaPorAtraso();
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(new TenantCharge
        {
            TenantId = tenant.Id, Kind = TenantChargeKind.Mensalidade, Amount = 129m,
            ReferenceMonth = Hoje.AddMonths(-1), DueDate = Hoje.AddDays(-20),
            Gateway = "asaas", ExternalChargeId = "pay_1",
        });
        await db.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("tenant-slug:loja-devendo", "status antigo");
        var notifier = new Mock<IPlatformBillingNotifier>();
        var servico = new PlatformBillingService(db, NullLogger<PlatformBillingService>.Instance,
            config: Config(), notifier: notifier.Object, cache: cache);

        await servico.RegistrarPagamentoExternoAsync("asaas", "pay_1", paga: true, pagoEm: Hoje);

        var loja = await db.Tenants.AsNoTracking().SingleAsync();
        loja.Status.Should().Be(TenantStatus.Active, "quem pagou não pode esperar a rodada de 12 horas");
        loja.PaymentStatus.Should().Be(TenantPaymentStatus.Pago);
        cache.TryGetValue("tenant-slug:loja-devendo", out _).Should().BeFalse(
            "senão o middleware continua respondendo 'suspensa' por 30 segundos");
        notifier.Verify(n => n.NotificarLojaReativada(tenant.Id), Times.Once);
    }

    [Fact]
    public async Task Webhook_AindaComOutraDividaVencida_ContinuaSuspensa()
    {
        using var db = CreateDb();
        var tenant = LojaSuspensaPorAtraso();
        db.Tenants.Add(tenant);
        db.TenantCharges.AddRange(
            new TenantCharge
            {
                TenantId = tenant.Id, Kind = TenantChargeKind.Mensalidade, Amount = 129m,
                ReferenceMonth = Hoje.AddMonths(-2), DueDate = Hoje.AddDays(-50),
            },
            new TenantCharge
            {
                TenantId = tenant.Id, Kind = TenantChargeKind.Mensalidade, Amount = 129m,
                ReferenceMonth = Hoje.AddMonths(-1), DueDate = Hoje.AddDays(-20),
                Gateway = "asaas", ExternalChargeId = "pay_2",
            });
        await db.SaveChangesAsync();

        var notifier = new Mock<IPlatformBillingNotifier>();
        var servico = new PlatformBillingService(db, NullLogger<PlatformBillingService>.Instance,
            config: Config(), notifier: notifier.Object);

        await servico.RegistrarPagamentoExternoAsync("asaas", "pay_2", paga: true, pagoEm: Hoje);

        (await db.Tenants.AsNoTracking().SingleAsync()).Status.Should().Be(TenantStatus.Suspended);
        notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BaixaManual_TambemReativaNaHora()
    {
        using var db = CreateDb();
        var tenant = LojaSuspensaPorAtraso();
        db.Tenants.Add(tenant);
        var cobranca = new TenantCharge
        {
            TenantId = tenant.Id, Kind = TenantChargeKind.Mensalidade, Amount = 129m,
            ReferenceMonth = Hoje.AddMonths(-1), DueDate = Hoje.AddDays(-20),
        };
        db.TenantCharges.Add(cobranca);
        await db.SaveChangesAsync();

        var servico = new PlatformBillingService(db, NullLogger<PlatformBillingService>.Instance, config: Config());
        await servico.DefinirPagamentoAsync(cobranca.Id, Hoje);

        (await db.Tenants.AsNoTracking().SingleAsync()).Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task SuspensaAMaoPorOutroMotivo_PagamentoNaoReabre()
    {
        using var db = CreateDb();
        var tenant = LojaSuspensaPorAtraso();
        tenant.PaymentStatus = TenantPaymentStatus.Pago; // suspensão manual: fim de contrato
        db.Tenants.Add(tenant);
        var cobranca = new TenantCharge
        {
            TenantId = tenant.Id, Kind = TenantChargeKind.Mensalidade, Amount = 129m,
            ReferenceMonth = Hoje.AddMonths(-1), DueDate = Hoje.AddDays(-20),
        };
        db.TenantCharges.Add(cobranca);
        await db.SaveChangesAsync();

        await new PlatformBillingService(db, NullLogger<PlatformBillingService>.Instance, config: Config())
            .DefinirPagamentoAsync(cobranca.Id, Hoje);

        (await db.Tenants.AsNoTracking().SingleAsync()).Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public async Task Regua_AoSuspender_AvisaOLojista()
    {
        using var db = CreateDb();
        var tenant = LojaSuspensaPorAtraso();
        tenant.Status = TenantStatus.Active;
        tenant.PaymentStatus = TenantPaymentStatus.Pago;
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(new TenantCharge
        {
            TenantId = tenant.Id, Kind = TenantChargeKind.Mensalidade, Amount = 129m,
            ReferenceMonth = Hoje.AddMonths(-1), DueDate = Hoje.AddDays(-8),
        });
        await db.SaveChangesAsync();

        var notifier = new Mock<IPlatformBillingNotifier>();
        await new PlatformBillingService(db, NullLogger<PlatformBillingService>.Instance,
                config: Config(), notifier: notifier.Object)
            .AplicarReguaDeCobrancaAsync();

        notifier.Verify(n => n.NotificarLojaSuspensa(tenant.Id), Times.Once);
    }

    // ── Avisos por prazo ─────────────────────────────────────────────────────

    private sealed class Ambiente : IDisposable
    {
        public ServiceProvider Services { get; }
        public Mock<IEmailService> Email { get; } = new();
        public CatalogDbContext Db => Services.GetRequiredService<CatalogDbContext>();
        public ITenantContext? TenantDoEnvio { get; private set; }

        public Ambiente()
        {
            var banco = Guid.NewGuid().ToString();
            var colecao = new ServiceCollection();
            colecao.AddDbContext<CatalogDbContext>(o => o.UseInMemoryDatabase(banco));
            colecao.AddScoped<ITenantContext, TenantContext>();
            colecao.AddScoped(sp =>
            {
                // Registra em qual tenant o e-mail foi resolvido: tem que ser a plataforma.
                TenantDoEnvio = sp.GetRequiredService<ITenantContext>();
                return Email.Object;
            });
            Services = colecao.BuildServiceProvider();
        }

        public PlatformBillingNotifier Notifier() => new(
            Services.GetRequiredService<IServiceScopeFactory>(), Config(), NullLogger<PlatformBillingNotifier>.Instance);

        public void Dispose() => Services.Dispose();
    }

    [Fact]
    public async Task Avisos_FaturaPertoDeSuspender_AvisaUmaVezSo()
    {
        using var ambiente = new Ambiente();
        var tenant = LojaSuspensaPorAtraso();
        tenant.Status = TenantStatus.Active;
        using (var scope = ambiente.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            db.Tenants.Add(tenant);
            db.TenantCharges.Add(new TenantCharge
            {
                // Carência de 7: vencida há 5 dias, suspende em 3.
                TenantId = tenant.Id, Kind = TenantChargeKind.Mensalidade, Amount = 129m,
                ReferenceMonth = Hoje.AddMonths(-1), DueDate = Hoje.AddDays(-5),
                PaymentUrl = "https://asaas.test/i/1",
            });
            await db.SaveChangesAsync();
        }

        var notifier = ambiente.Notifier();
        await notifier.EnviarAvisosDePrazoAsync();
        await notifier.EnviarAvisosDePrazoAsync();

        ambiente.Email.Verify(e => e.SendCobrancaAvisoSuspensaoAsync(
            "dono@loja.test", "Loja Devendo", "Loja Devendo", 129m, Hoje.AddDays(-5), Hoje.AddDays(3),
            "https://asaas.test/i/1", "https://loja-devendo.3esysten.com.br/admin/assinatura"), Times.Once);
        ambiente.TenantDoEnvio!.SchemaName.Should().Be(TenantConstants.TenantZeroSchema,
            "aviso da plataforma não pode sair pelo SMTP que a loja configurou");
    }

    [Fact]
    public async Task Avisos_VencidaHaPouco_AindaNaoAvisa()
    {
        using var ambiente = new Ambiente();
        var tenant = LojaSuspensaPorAtraso();
        tenant.Status = TenantStatus.Active;
        using (var scope = ambiente.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            db.Tenants.Add(tenant);
            db.TenantCharges.Add(new TenantCharge
            {
                TenantId = tenant.Id, Kind = TenantChargeKind.Mensalidade, Amount = 129m,
                ReferenceMonth = Hoje, DueDate = Hoje.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        await ambiente.Notifier().EnviarAvisosDePrazoAsync();

        ambiente.Email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Avisos_TesteAcabandoSemDocumento_PedeOsDados()
    {
        using var ambiente = new Ambiente();
        var tenant = new Tenant
        {
            Slug = "loja-nova", SchemaName = "tenant_loja_nova", DisplayName = "Loja Nova",
            MonthlyPrice = 129m, BillingEmail = "ana@loja.test", BillingStartsOn = Hoje.AddDays(4),
        };
        var comDocumento = new Tenant
        {
            Slug = "loja-ok", SchemaName = "tenant_loja_ok", MonthlyPrice = 129m, BillingEmail = "ok@loja.test",
            BillingCnpj = "11222333000181", BillingStartsOn = Hoje.AddDays(4),
        };
        var longeDoFim = new Tenant
        {
            Slug = "loja-cedo", SchemaName = "tenant_loja_cedo", MonthlyPrice = 129m, BillingEmail = "cedo@loja.test",
            BillingStartsOn = Hoje.AddDays(12),
        };
        using (var scope = ambiente.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            db.Tenants.AddRange(tenant, comDocumento, longeDoFim);
            await db.SaveChangesAsync();
        }

        await ambiente.Notifier().EnviarAvisosDePrazoAsync();

        ambiente.Email.Verify(e => e.SendCobrancaDadosFaltandoAsync(
            "ana@loja.test", "Loja Nova", "Loja Nova", Hoje.AddDays(4),
            "https://loja-nova.3esysten.com.br/admin/assinatura"), Times.Once);
        ambiente.Email.VerifyNoOtherCalls();
    }
}
