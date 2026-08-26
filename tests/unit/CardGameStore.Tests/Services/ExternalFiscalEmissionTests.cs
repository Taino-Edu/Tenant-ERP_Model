using CardGameStore.DTOs;
using CardGameStore.Services.Implementations;

namespace CardGameStore.Tests.Services;

public sealed class ExternalFiscalEmissionTests
{
    [Fact]
    public void MapsImmutableExternalSnapshotToFiscalEngineData()
    {
        var request = new IntegrationFiscalEmissionRequest
        {
            Source = "softnerd",
            ExternalDocumentId = "comanda:123",
            IdempotencyKey = "softnerd:comanda:123",
            PaymentMethod = "Pix",
            DiscountInCents = 100,
            CustomerCpf = "12345678901",
            Items =
            [
                new IntegrationFiscalItemRequest
                {
                    Name = "Produto A",
                    Ncm = "95044000",
                    Cfop = "5102",
                    Csosn = "102",
                    Quantity = 2,
                    UnitPriceInCents = 1_000,
                    SubtotalInCents = 2_000,
                },
            ],
        };

        var result = NfceEmissionService.MapearDadosIntegracao(request);

        result.ValorBrutoCentavos.Should().Be(2_000);
        result.ValorLiquidoCentavos.Should().Be(1_900);
        result.FormaPagamento.Should().Be("Pix");
        result.ClienteCpf.Should().Be("12345678901");
        result.Itens.Should().ContainSingle(i => i.Ncm == "95044000" && i.Cfop == "5102");
    }

    [Fact]
    public void RejectsDivergentLineSubtotalBeforeTransmission()
    {
        var request = new IntegrationFiscalEmissionRequest
        {
            Source = "softnerd",
            ExternalDocumentId = "venda:1",
            IdempotencyKey = "softnerd:venda:1",
            PaymentMethod = "Dinheiro",
            Items =
            [
                new IntegrationFiscalItemRequest
                {
                    Name = "Produto inconsistente",
                    Ncm = "95044000",
                    Cfop = "5102",
                    Quantity = 2,
                    UnitPriceInCents = 1_000,
                    SubtotalInCents = 1_999,
                },
            ],
        };

        var action = () => NfceEmissionService.MapearDadosIntegracao(request);

        action.Should().Throw<FiscalNaoConfiguradoException>()
            .WithMessage("*diverge*");
    }
}
