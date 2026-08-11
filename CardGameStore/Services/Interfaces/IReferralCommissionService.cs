using CardGameStore.Multitenancy;

namespace CardGameStore.Services.Interfaces;

public interface IReferralCommissionService
{
    Task SynchronizeChargeAsync(TenantCharge charge, DateTime? previousPaidAt);
    Task SynchronizeReferralAsync(Guid referralId);
}
