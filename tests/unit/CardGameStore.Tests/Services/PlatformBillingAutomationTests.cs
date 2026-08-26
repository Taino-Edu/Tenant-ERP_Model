// =============================================================================
// PlatformBillingAutomationTests.cs — Automação da cobrança da plataforma (RB-01):
// régua de suspensão/reativação, baixa por webhook e leitura do payload do Asaas.
//
// O que se protege aqui é o que dá prejuízo ou constrangimento com cliente:
// suspender loja adimplente, deixar inadimplente rodando de graça, reativar
// loja que foi desligada à mão por outro motivo, e webhook duplicado
// reescrevendo baixa (que reprocessaria comissão de indicação já apurada).
//
// InMemory pelo mesmo motivo de PlatformBillingServiceTests — e com a mesma
// ressalva: o provider não enforça unique index, então a trava contra dois ids
// externos iguais é a da migration, não deste teste.
// =============================================================================

using System.Text.Json;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class PlatformBillingAutomationTests
{
    private static CatalogDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PlatformBillingService CreateService(CatalogDbContext db, int carencia = 7)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Billing:DiasDeCarenciaAposVencimento"] = carencia.ToString(),
            })
            .Build();

        return new PlatformBillingService(db, NullLogger<PlatformBillingService>.Instance, config: config);
    }

    private static Tenant NovoTenant(
        string slug = "loja-teste",
        TenantStatus status = TenantStatus.Active,
        TenantPaymentStatus pagamento = TenantPaymentStatus.Pago)
        => new()
        {
            Slug          = slug,
            SchemaName    = "tenant_" + slug.Replace('-', '_'),
            Status        = status,
            PaymentStatus = pagamento,
            MonthlyPrice  = 269m,
        };

    private static TenantCharge Cobranca(
        Guid tenantId,
        int diasDesdeVencimento,
        DateTime? paga = null,
        string? gateway = null,
        string? externalId = null)
        => new()
        {
            TenantId         = tenantId,
            Kind             = TenantChargeKind.Mensalidade,
            Amount           = 269m,
            ReferenceMonth   = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate          = DateTime.UtcNow.Date.AddDays(-diasDesdeVencimento),
            PaidAt           = paga,
            Gateway          = gateway,
            ExternalChargeId = externalId,
        };

    // ── Régua: suspensão ─────────────────────────────────────────────────────

    [Fact]
    public async Task Regua_VencidaAlemDaCarencia_SuspendeLoja()
    {
        using var db = CreateDb();
        var tenant = NovoTenant();
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(Cobranca(tenant.Id, diasDesdeVencimento: 10));
        await db.SaveChangesAsync();

        var resultado = await CreateService(db).AplicarReguaDeCobrancaAsync();

        resultado.Suspensos.Should().ContainSingle().Which.Should().Be("loja-teste");
        var atualizado = await db.Tenants.FirstAsync();
        atualizado.Status.Should().Be(TenantStatus.Suspended);
        atualizado.PaymentStatus.Should().Be(TenantPaymentStatus.Atrasado);
    }

    [Fact]
    public async Task Regua_DentroDaCarencia_NaoSuspende()
    {
        // Boleto compensa em dois dias úteis: suspender no dia seguinte ao
        // vencimento derrubaria loja que já pagou.
        using var db = CreateDb();
        var tenant = NovoTenant();
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(Cobranca(tenant.Id, diasDesdeVencimento: 3));
        await db.SaveChangesAsync();

        var resultado = await CreateService(db).AplicarReguaDeCobrancaAsync();

        resultado.Suspensos.Should().BeEmpty();
        (await db.Tenants.FirstAsync()).Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task Regua_TenantIsento_NuncaSuspende()
    {
        using var db = CreateDb();
        var tenant = NovoTenant(pagamento: TenantPaymentStatus.Isento);
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(Cobranca(tenant.Id, diasDesdeVencimento: 90));
        await db.SaveChangesAsync();

        var resultado = await CreateService(db).AplicarReguaDeCobrancaAsync();

        resultado.Suspensos.Should().BeEmpty();
        (await db.Tenants.FirstAsync()).Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task Regua_CobrancaPaga_NaoSuspende()
    {
        using var db = CreateDb();
        var tenant = NovoTenant();
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(Cobranca(tenant.Id, diasDesdeVencimento: 40, paga: DateTime.UtcNow.Date));
        await db.SaveChangesAsync();

        var resultado = await CreateService(db).AplicarReguaDeCobrancaAsync();

        resultado.Suspensos.Should().BeEmpty();
        (await db.Tenants.FirstAsync()).Status.Should().Be(TenantStatus.Active);
    }

    // ── Régua: reativação ────────────────────────────────────────────────────

    [Fact]
    public async Task Regua_QuitouDepoisDeSuspensa_Reativa()
    {
        using var db = CreateDb();
        var tenant = NovoTenant(status: TenantStatus.Suspended, pagamento: TenantPaymentStatus.Atrasado);
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(Cobranca(tenant.Id, diasDesdeVencimento: 20, paga: DateTime.UtcNow.Date));
        await db.SaveChangesAsync();

        var resultado = await CreateService(db).AplicarReguaDeCobrancaAsync();

        resultado.Reativados.Should().ContainSingle().Which.Should().Be("loja-teste");
        var atualizado = await db.Tenants.FirstAsync();
        atualizado.Status.Should().Be(TenantStatus.Active);
        atualizado.PaymentStatus.Should().Be(TenantPaymentStatus.Pago);
    }

    [Fact]
    public async Task Regua_SuspensaManualmente_NaoReativaSozinha()
    {
        // Suspensão manual (fim de contrato, abuso) não carrega PaymentStatus
        // Atrasado. Sem essa distinção a régua reabriria uma loja que o dono da
        // plataforma desligou de propósito — e ninguém ficaria sabendo.
        using var db = CreateDb();
        var tenant = NovoTenant(status: TenantStatus.Suspended, pagamento: TenantPaymentStatus.Pago);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var resultado = await CreateService(db).AplicarReguaDeCobrancaAsync();

        resultado.Reativados.Should().BeEmpty();
        (await db.Tenants.FirstAsync()).Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public async Task Regua_QuitouAntesDeSuspender_VoltaStatusParaPago()
    {
        using var db = CreateDb();
        var tenant = NovoTenant(status: TenantStatus.Active, pagamento: TenantPaymentStatus.Atrasado);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        await CreateService(db).AplicarReguaDeCobrancaAsync();

        var atualizado = await db.Tenants.FirstAsync();
        atualizado.Status.Should().Be(TenantStatus.Active);
        atualizado.PaymentStatus.Should().Be(TenantPaymentStatus.Pago);
    }

    // ── Baixa por webhook ────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_CobrancaConhecida_DaBaixa()
    {
        using var db = CreateDb();
        var tenant = NovoTenant();
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(Cobranca(tenant.Id, 5, gateway: "asaas", externalId: "pay_123"));
        await db.SaveChangesAsync();

        var achou = await CreateService(db).RegistrarPagamentoExternoAsync(
            "asaas", "pay_123", paga: true, pagoEm: new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc));

        achou.Should().BeTrue();
        (await db.TenantCharges.FirstAsync()).PaidAt
            .Should().Be(new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Webhook_Reentregue_NaoReescreveDataDaBaixa()
    {
        // O Asaas reenvia webhook, e PAYMENT_CONFIRMED e PAYMENT_RECEIVED chegam
        // os dois pro mesmo Pix. Reescrever a baixa mandaria o serviço de
        // comissão recalcular repasse já apurado.
        using var db = CreateDb();
        var tenant = NovoTenant();
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(Cobranca(tenant.Id, 5, gateway: "asaas", externalId: "pay_123"));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var primeira = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);

        await service.RegistrarPagamentoExternoAsync("asaas", "pay_123", true, primeira);
        await service.RegistrarPagamentoExternoAsync("asaas", "pay_123", true, primeira.AddDays(3));

        (await db.TenantCharges.FirstAsync()).PaidAt.Should().Be(primeira);
    }

    [Fact]
    public async Task Webhook_Estorno_ReabreCobranca()
    {
        using var db = CreateDb();
        var tenant = NovoTenant();
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(Cobranca(tenant.Id, 5, paga: DateTime.UtcNow.Date,
            gateway: "asaas", externalId: "pay_123"));
        await db.SaveChangesAsync();

        var achou = await CreateService(db).RegistrarPagamentoExternoAsync(
            "asaas", "pay_123", paga: false, pagoEm: null);

        achou.Should().BeTrue();
        (await db.TenantCharges.FirstAsync()).PaidAt.Should().BeNull();
    }

    [Fact]
    public async Task Webhook_IdDesconhecido_NaoEncontraENaoQuebra()
    {
        // O gateway notifica tudo que acontece na conta; nem toda cobrança lá é
        // mensalidade nossa. Isso é caso normal, não erro.
        using var db = CreateDb();

        var achou = await CreateService(db).RegistrarPagamentoExternoAsync(
            "asaas", "pay_de_outra_coisa", paga: true, pagoEm: null);

        achou.Should().BeFalse();
    }

    [Fact]
    public async Task Webhook_GatewayDiferente_NaoCasaPeloIdSozinho()
    {
        // Id de gateway só é único dentro do gateway. Buscar só pelo id casaria
        // a cobrança errada no dia em que rodarmos dois PSPs em paralelo.
        using var db = CreateDb();
        var tenant = NovoTenant();
        db.Tenants.Add(tenant);
        db.TenantCharges.Add(Cobranca(tenant.Id, 5, gateway: "asaas", externalId: "pay_123"));
        await db.SaveChangesAsync();

        var achou = await CreateService(db).RegistrarPagamentoExternoAsync(
            "woovi", "pay_123", paga: true, pagoEm: null);

        achou.Should().BeFalse();
        (await db.TenantCharges.FirstAsync()).PaidAt.Should().BeNull();
    }
}

// =============================================================================

public class AsaasPlatformGatewayTests
{
    private static AsaasPlatformGateway CreateGateway(string? apiKey = "chave", string? webhookToken = "segredo")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Billing:Asaas:ApiKey"]       = apiKey,
                ["Billing:Asaas:WebhookToken"] = webhookToken,
            })
            .Build();

        return new AsaasPlatformGateway(
            new StubHttpClientFactory(), config, NullLogger<AsaasPlatformGateway>.Instance);
    }

    private static JsonElement Payload(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Theory]
    [InlineData("PAYMENT_RECEIVED",  GatewayPaymentOutcome.Paga)]
    [InlineData("PAYMENT_CONFIRMED", GatewayPaymentOutcome.Paga)]
    [InlineData("PAYMENT_REFUNDED",  GatewayPaymentOutcome.Revertida)]
    [InlineData("PAYMENT_DELETED",   GatewayPaymentOutcome.Revertida)]
    [InlineData("PAYMENT_CREATED",   GatewayPaymentOutcome.Ignorada)]
    [InlineData("PAYMENT_OVERDUE",   GatewayPaymentOutcome.Ignorada)]
    public void InterpretarWebhook_TraduzOEventoParaODesfecho(string evento, GatewayPaymentOutcome esperado)
    {
        var json = "{\"event\":\"" + evento
                 + "\",\"payment\":{\"id\":\"pay_1\",\"paymentDate\":\"2026-08-20\"}}";

        var notificacao = CreateGateway().InterpretarWebhook(Payload(json));

        notificacao.Should().NotBeNull();
        notificacao!.Outcome.Should().Be(esperado);
        notificacao.ExternalChargeId.Should().Be("pay_1");
    }

    [Fact]
    public void InterpretarWebhook_PagamentoSemData_DeixaAQuemChamouDecidir()
    {
        var notificacao = CreateGateway().InterpretarWebhook(Payload(
            """{"event":"PAYMENT_RECEIVED","payment":{"id":"pay_1"}}"""));

        notificacao!.PagoEm.Should().BeNull();
    }

    [Fact]
    public void InterpretarWebhook_SemObjetoPayment_DevolveNull()
    {
        // Null é "não entendi", diferente de Ignorada, que é "entendi e não muda
        // nada" — o controller loga um e não o outro.
        CreateGateway().InterpretarWebhook(Payload("""{"event":"PAYMENT_RECEIVED"}"""))
            .Should().BeNull();
    }

    [Fact]
    public void ValidarAutenticacao_TokenCorreto_Aceita() =>
        CreateGateway().ValidarAutenticacao("segredo").Should().BeTrue();

    [Fact]
    public void ValidarAutenticacao_TokenErrado_Recusa() =>
        CreateGateway().ValidarAutenticacao("outro").Should().BeFalse();

    [Fact]
    public void ValidarAutenticacao_SemTokenConfigurado_FechaOEndpoint()
    {
        // Falha fechando, não abrindo: webhook público sem segredo é um botão de
        // "quitar mensalidade" para qualquer um na internet.
        CreateGateway(webhookToken: null).ValidarAutenticacao("qualquer-coisa")
            .Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_SemChave_DeixaAPlataformaSubirEmModoManual() =>
        CreateGateway(apiKey: null).IsConfigured.Should().BeFalse();

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new() { BaseAddress = new Uri("https://exemplo.invalido/") };
    }
}
