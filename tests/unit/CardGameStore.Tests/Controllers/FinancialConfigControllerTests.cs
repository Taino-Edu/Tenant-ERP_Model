using CardGameStore.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CardGameStore.Tests.Controllers;

public class FinancialConfigControllerTests
{
    [Fact]
    public async Task Get_SemConfiguracao_RetornaPremissasZeradas()
    {
        using var db = TestDbFactory.Create(nameof(Get_SemConfiguracao_RetornaPremissasZeradas));
        var controller = new FinancialConfigController(db);

        var response = await controller.Get();

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        var config = ok.Value.Should().BeOfType<FinancialConfigDto>().Subject;
        config.CardFeePercent.Should().Be(0);
        config.MinimumCashReserve.Should().Be(0);
        db.FinancialConfigs.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_PersisteTodasAsPremissas()
    {
        var schema = nameof(Save_PersisteTodasAsPremissas);
        using var db = TestDbFactory.Create(schema);
        var controller = new FinancialConfigController(db);
        var request = new SaveFinancialConfigRequest
        {
            CardFeePercent = 2.75m,
            CommissionPercent = 1.5m,
            FreightPercent = 0.8m,
            ExpectedDailyNetCash = 125.50m,
            MinimumCashReserve = 2000m,
        };

        await controller.Save(request);
        db.ChangeTracker.Clear();
        var response = await controller.Get();

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        var config = ok.Value.Should().BeOfType<FinancialConfigDto>().Subject;
        config.CardFeePercent.Should().Be(2.75m);
        config.CommissionPercent.Should().Be(1.5m);
        config.FreightPercent.Should().Be(0.8m);
        config.ExpectedDailyNetCash.Should().Be(125.50m);
        config.MinimumCashReserve.Should().Be(2000m);
    }
}
