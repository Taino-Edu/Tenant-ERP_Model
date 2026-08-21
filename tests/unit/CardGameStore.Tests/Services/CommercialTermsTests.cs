using CardGameStore.Multitenancy;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;

namespace CardGameStore.Tests.Services;

public class CommercialTermsTests
{
    [Theory]
    [InlineData("Lagoa", 129, 258)]
    [InlineData("Rio", 269, 538)]
    [InlineData("Mar", 487, 974)]
    // Nome fora da tabela (cortesia, piloto, typo): mensalidade zero, e por
    // consequência implantação zero. Chutar valor infla o MRR com número que
    // parece certo.
    [InlineData("Piloto interno", 0, 0)]
    public void ApplyCommercialTerms_UsesCatalogAndFifteenDayTrial(
        string planName, decimal monthlyPrice, decimal setupFee)
    {
        var createdAt = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);
        var tenant = new Tenant { PlanName = planName, CreatedAt = createdAt };

        TenantProvisioningService.ApplyCommercialTerms(tenant);

        tenant.MonthlyPrice.Should().Be(monthlyPrice);
        tenant.SetupFee.Should().Be(setupFee);
        tenant.BillingStartsOn.Should().Be(createdAt.AddDays(15));
    }

    [Theory]
    [InlineData("Qual é o preço dos planos?", "Lagoa")]
    [InlineData("Como funciona para fundador?", "30%")]
    [InlineData("Tem comanda para restaurante?", "módulo opcional")]
    public void PublicAssistantFallback_OnlyReturnsCuratedCommercialFacts(
        string question, string expected)
    {
        var response = PublicSalesAssistantService.BuildFallback(question);

        response.Reply.Should().Contain(expected);
        response.MarketingWhatsappUrl.Should().Be("https://wa.me/5517997455482");
    }

    [Theory]
    [InlineData(null, "Octus")]
    [InlineData("Minha Loja", "Octus")]
    [InlineData("Loja da Ana", "Loja da Ana")]
    public void ResolveSiteName_OnlyReplacesTheLegacyPlatformPlaceholder(
        string? configuredName, string expected)
    {
        SiteConfig.ResolveSiteName(configuredName).Should().Be(expected);
    }
}
