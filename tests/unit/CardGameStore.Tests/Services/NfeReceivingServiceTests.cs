using CardGameStore.Services.Implementations;
using FluentAssertions;

namespace CardGameStore.Tests.Services;

public class NfeReceivingServiceTests
{
    [Fact]
    public void ParseXml_DeveLerItensECalcularCustoSugerido()
    {
        const string xml = """
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe">
              <NFe><infNFe Id="NFe123">
                <emit><CNPJ>12.345.678/0001-90</CNPJ><xNome>Fornecedor Teste</xNome></emit>
                <det nItem="1"><prod>
                  <cProd>ABC-1</cProd><cEAN>7891234567890</cEAN><xProd>Produto Teste</xProd>
                  <NCM>12345678</NCM><CFOP>5102</CFOP><uCom>UN</uCom><qCom>10.0000</qCom>
                  <vUnCom>12.000000</vUnCom><vProd>120.00</vProd><vDesc>10.00</vDesc><vFrete>5.00</vFrete>
                </prod></det>
              </infNFe></NFe>
            </nfeProc>
            """;

        var parsed = NfeReceivingService.ParseXml(xml);

        parsed.SupplierCnpj.Should().Be("12345678000190");
        parsed.SupplierName.Should().Be("Fornecedor Teste");
        parsed.Items.Should().ContainSingle();
        var item = parsed.Items[0];
        item.ItemNumber.Should().Be(1);
        item.SupplierProductCode.Should().Be("ABC-1");
        item.Gtin.Should().Be("7891234567890");
        item.SuggestedQuantity.Should().Be(10);
        item.SuggestedUnitCostInCents.Should().Be(1150); // (120 - 10 + 5) / 10
    }

    [Fact]
    public void ParseXml_QuantidadeFracionada_NaoDeveInventarUnidadesInteiras()
    {
        const string xml = """
            <NFe xmlns="http://www.portalfiscal.inf.br/nfe"><infNFe>
              <emit><CNPJ>12345678000190</CNPJ></emit>
              <det nItem="1"><prod><cProd>KG1</cProd><xProd>Produto por peso</xProd>
              <qCom>1.500</qCom><vProd>30.00</vProd></prod></det>
            </infNFe></NFe>
            """;

        var item = NfeReceivingService.ParseXml(xml).Items.Single();

        item.XmlQuantity.Should().Be(1.5m);
        item.SuggestedQuantity.Should().BeNull();
        item.SuggestedUnitCostInCents.Should().Be(2000);
    }

    [Theory]
    [InlineData(10, 1000, 10, 2000, 1500)]
    [InlineData(0, 9999, 3, 1234, 1234)]
    [InlineData(-2, 500, 2, 900, 900)]
    public void CalculateWeightedAverageCost_DeveUsarSaldoECustoDaEntrada(
        int stock, int currentCost, int quantity, int incomingCost, int expected)
    {
        NfeReceivingService.CalculateWeightedAverageCost(stock, currentCost, quantity, incomingCost)
            .Should().Be(expected);
    }
}
