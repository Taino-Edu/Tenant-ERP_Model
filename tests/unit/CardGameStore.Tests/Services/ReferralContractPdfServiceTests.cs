using System.Text;
using CardGameStore.Services;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.Services;

public sealed class ReferralContractPdfServiceTests
{
    [Fact]
    public void Generate_ProducesValidMultipagePdf()
    {
        var bytes = new ReferralContractPdfService().Generate(new ReferralContractPdfData(
            "Maria da Silva", "PF", "12345678901", "maria@example.com", "(11) 99999-9999",
            "Parceiro de indicação", 30m, 5m, 5, ReferralPartnerTerms.Version, ReferralPartnerTerms.Text,
            new DateTime(2026, 8, 17, 20, 30, 0, DateTimeKind.Utc),
            "36ee537e-8497-4a78-9d3d-f6284db6046d", new string('A', 64), new string('B', 64), "Chrome 140 / Windows 11"));

        bytes.Should().StartWith(Encoding.ASCII.GetBytes("%PDF-1.4"));
        Encoding.Latin1.GetString(bytes).Should().Contain("/Count 2").And.Contain("REGULAMENTO ACEITO");
        var output = Environment.GetEnvironmentVariable("REFERRAL_PDF_QA_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output)) File.WriteAllBytes(output, bytes);
    }
}
