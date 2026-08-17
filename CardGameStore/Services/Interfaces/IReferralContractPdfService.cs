namespace CardGameStore.Services.Interfaces;

public sealed record ReferralContractPdfData(
    string PartnerName,
    string PersonType,
    string Document,
    string Email,
    string? Phone,
    string PartnerKind,
    decimal SetupCommissionPercent,
    decimal MonthlyCommissionPercent,
    int PaymentGraceDays,
    string ContractVersion,
    string ContractText,
    DateTime AcceptedAtUtc,
    string EvidenceId,
    string EvidenceSha256,
    string IpHash,
    string UserAgent);

public interface IReferralContractPdfService
{
    byte[] Generate(ReferralContractPdfData data);
}
