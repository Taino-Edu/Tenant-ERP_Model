using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class CashPaymentCalculatorTests
{
    [Fact]
    public void DinheiroIntegral_CalculaTroco()
    {
        var result = CashPaymentCalculator.Calculate(
            6774, PaymentMethod.Dinheiro, null, 0, 7000);

        result.ReceivedInCents.Should().Be(7000);
        result.ChangeInCents.Should().Be(226);
    }

    [Fact]
    public void SplitDinheiroPix_CalculaTrocoSomenteDaParcelaEmDinheiro()
    {
        var result = CashPaymentCalculator.Calculate(
            6774, PaymentMethod.Dinheiro, PaymentMethod.Pix, 5000, 2000);

        result.ChangeInCents.Should().Be(226);
    }

    [Fact]
    public void ValorEntregueMenorQueDevido_EBloqueado()
    {
        var act = () => CashPaymentCalculator.Calculate(
            1000, PaymentMethod.Dinheiro, null, 0, 999);

        act.Should().Throw<InvalidOperationException>().WithMessage("*menor*");
    }
}
