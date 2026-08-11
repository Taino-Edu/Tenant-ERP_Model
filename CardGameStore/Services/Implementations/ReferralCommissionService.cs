using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class ReferralCommissionService : IReferralCommissionService
{
    private readonly CatalogDbContext _catalog;

    public ReferralCommissionService(CatalogDbContext catalog) => _catalog = catalog;

    public async Task SynchronizeChargeAsync(TenantCharge charge, DateTime? previousPaidAt)
    {
        var existing = await _catalog.ReferralCommissions
            .FirstOrDefaultAsync(c => c.TenantChargeId == charge.Id);

        if (charge.PaidAt is null)
        {
            if (existing?.PaidAt is not null)
                throw new InvalidOperationException("A cobrança não pode ser reaberta porque a comissão já foi paga ao vendedor.");
            if (existing is not null) _catalog.ReferralCommissions.Remove(existing);
            return;
        }

        if (existing is not null) return;

        var referral = await _catalog.TenantReferrals.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == charge.TenantId && r.Active);
        if (referral is null || charge.PaidAt.Value.Date < referral.StartedOn.Date) return;

        var percent = charge.Kind == TenantChargeKind.Implantacao
            ? referral.SetupCommissionPercent
            : referral.MonthlyCommissionPercent;
        if (percent <= 0) return;

        if (charge.Kind == TenantChargeKind.Mensalidade && referral.MonthlyCommissionCycles.HasValue)
        {
            var cycle = ((charge.ReferenceMonth.Year - referral.StartedOn.Year) * 12)
                      + charge.ReferenceMonth.Month - referral.StartedOn.Month + 1;
            if (cycle < 1 || cycle > referral.MonthlyCommissionCycles.Value) return;
        }

        var paymentDay = await _catalog.ReferralPartners.AsNoTracking()
            .Where(p => p.Id == referral.PartnerId).Select(p => p.PaymentDay).SingleAsync();
        var earnedAt = charge.PaidAt.Value;

        _catalog.ReferralCommissions.Add(new ReferralCommission
        {
            ReferralId = referral.Id,
            TenantChargeId = charge.Id,
            ChargeKind = charge.Kind,
            BaseAmount = charge.Amount,
            CommissionPercent = percent,
            Amount = decimal.Round(charge.Amount * percent / 100m, 2, MidpointRounding.AwayFromZero),
            ReferenceMonth = charge.ReferenceMonth,
            EarnedAt = earnedAt,
            DueDate = NextPaymentDate(earnedAt, paymentDay),
        });
    }

    public async Task SynchronizeReferralAsync(Guid referralId)
    {
        var referral = await _catalog.TenantReferrals.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == referralId)
            ?? throw new InvalidOperationException("Indicação não encontrada.");

        var charges = await _catalog.TenantCharges
            .Where(c => c.TenantId == referral.TenantId && c.PaidAt != null)
            .OrderBy(c => c.ReferenceMonth).ToListAsync();

        foreach (var charge in charges)
            await SynchronizeChargeAsync(charge, charge.PaidAt);
    }

    internal static DateTime NextPaymentDate(DateTime earnedAt, int paymentDay)
    {
        var utc = earnedAt.Kind == DateTimeKind.Utc ? earnedAt : earnedAt.ToUniversalTime();
        var year = utc.Year;
        var month = utc.Month;
        var day = Math.Min(paymentDay, DateTime.DaysInMonth(year, month));
        var candidate = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        if (candidate.Date >= utc.Date) return candidate;

        var next = candidate.AddMonths(1);
        return new DateTime(next.Year, next.Month,
            Math.Min(paymentDay, DateTime.DaysInMonth(next.Year, next.Month)), 0, 0, 0, DateTimeKind.Utc);
    }
}
