using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class ReferralCommissionServiceTests
{
    private static CatalogDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Tenant Tenant, ReferralPartner Partner, TenantReferral Referral)> SeedAsync(
        CatalogDbContext db, int paymentGraceDays = 5, int? cycles = null)
    {
        var tenant = new Tenant { Slug = "cliente", SchemaName = "tenant_cliente", MonthlyPrice = 269m };
        var partner = new ReferralPartner
        {
            Name = "Vendedor", PaymentGraceDays = paymentGraceDays,
            SetupCommissionPercent = 20m, MonthlyCommissionPercent = 10m,
        };
        var referral = new TenantReferral
        {
            TenantId = tenant.Id, PartnerId = partner.Id,
            SetupCommissionPercent = 20m, MonthlyCommissionPercent = 10m,
            MonthlyCommissionCycles = cycles,
            StartedOn = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
        };
        db.AddRange(tenant, partner, referral);
        await db.SaveChangesAsync();
        return (tenant, partner, referral);
    }

    [Fact]
    public async Task PagamentoDoCliente_GeraUmaUnicaComissaoComDataDeRepasse()
    {
        using var db = CreateDb();
        var seed = await SeedAsync(db, paymentGraceDays: 5);
        var charge = new TenantCharge
        {
            TenantId = seed.Tenant.Id, Kind = TenantChargeKind.Mensalidade,
            Amount = 269m, ReferenceMonth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            PaidAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        };
        db.TenantCharges.Add(charge);
        await db.SaveChangesAsync();
        var service = new ReferralCommissionService(db);

        await service.SynchronizeChargeAsync(charge, null);
        await db.SaveChangesAsync();
        await service.SynchronizeChargeAsync(charge, charge.PaidAt);
        await db.SaveChangesAsync();

        var commission = await db.ReferralCommissions.SingleAsync();
        commission.Amount.Should().Be(26.90m);
        commission.CommissionPercent.Should().Be(10m);
        commission.DueDate.Should().Be(new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CarenciaPersonalizada_ContaDaLiquidacaoDoCliente()
    {
        using var db = CreateDb();
        var seed = await SeedAsync(db, paymentGraceDays: 8);
        var charge = new TenantCharge
        {
            TenantId = seed.Tenant.Id, Kind = TenantChargeKind.Mensalidade,
            Amount = 269m, ReferenceMonth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            PaidAt = new DateTime(2026, 1, 31, 23, 30, 0, DateTimeKind.Utc),
        };
        db.TenantCharges.Add(charge);
        await db.SaveChangesAsync();

        await new ReferralCommissionService(db).SynchronizeChargeAsync(charge, null);
        await db.SaveChangesAsync();

        (await db.ReferralCommissions.SingleAsync()).DueDate
            .Should().Be(new DateTime(2026, 2, 8, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Implantacao_UsaPercentualProprioDoContrato()
    {
        using var db = CreateDb();
        var seed = await SeedAsync(db);
        var charge = new TenantCharge
        {
            TenantId = seed.Tenant.Id, Kind = TenantChargeKind.Implantacao,
            Amount = 538m, ReferenceMonth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            PaidAt = new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc),
        };
        db.TenantCharges.Add(charge); await db.SaveChangesAsync();

        await new ReferralCommissionService(db).SynchronizeChargeAsync(charge, null);
        await db.SaveChangesAsync();

        (await db.ReferralCommissions.SingleAsync()).Amount.Should().Be(107.60m);
    }

    [Fact]
    public async Task MensalidadeForaDoLimiteDeCiclos_NaoGeraComissao()
    {
        using var db = CreateDb();
        var seed = await SeedAsync(db, cycles: 2);
        var charge = new TenantCharge
        {
            TenantId = seed.Tenant.Id, Kind = TenantChargeKind.Mensalidade, Amount = 269m,
            ReferenceMonth = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            PaidAt = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
        };
        db.TenantCharges.Add(charge); await db.SaveChangesAsync();

        await new ReferralCommissionService(db).SynchronizeChargeAsync(charge, null);
        await db.SaveChangesAsync();

        (await db.ReferralCommissions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CobrancaNaoPodeSerReabertaDepoisDaComissaoPaga()
    {
        using var db = CreateDb();
        var seed = await SeedAsync(db);
        var charge = new TenantCharge
        {
            TenantId = seed.Tenant.Id, Kind = TenantChargeKind.Mensalidade, Amount = 269m,
            ReferenceMonth = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            PaidAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
        };
        db.TenantCharges.Add(charge); await db.SaveChangesAsync();
        var referralService = new ReferralCommissionService(db);
        await referralService.SynchronizeChargeAsync(charge, null); await db.SaveChangesAsync();
        (await db.ReferralCommissions.SingleAsync()).PaidAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        var billing = new PlatformBillingService(db, NullLogger<PlatformBillingService>.Instance, referralService);

        var action = () => billing.DefinirPagamentoAsync(charge.Id, null);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*comissão já foi paga*");
    }
}
