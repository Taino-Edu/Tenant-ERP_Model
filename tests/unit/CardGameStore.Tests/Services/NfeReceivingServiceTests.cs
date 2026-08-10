using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Tests.Services;

public class NfeReceivingServiceTests
{
    private const string XmlEntrada = """
        <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe"><NFe><infNFe>
          <emit><CNPJ>12345678000190</CNPJ><xNome>Fornecedor Teste</xNome></emit>
          <det nItem="1"><prod><cProd>ABC-1</cProd><xProd>Produto Teste</xProd>
          <NCM>95044000</NCM><CFOP>5102</CFOP><uCom>UN</uCom><qCom>2</qCom>
          <vUnCom>10</vUnCom><vProd>20</vProd></prod></det>
        </infNFe></NFe></nfeProc>
        """;

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
        item.Ncm.Should().Be("12345678");
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

    [Fact]
    public async Task ReceiveAsync_NcmAusente_PreencheProdutoEPreservaOrigemDocumental()
    {
        await using var db = TestDbFactory.Create(nameof(ReceiveAsync_NcmAusente_PreencheProdutoEPreservaOrigemDocumental));
        var product = new Product
        {
            Name = "Deck teste", Category = "TCG", PriceInCents = 2000,
            StockQuantity = 3, IsActive = true,
        };
        var nota = new NotaDestinada
        {
            ChaveAcesso = new string('1', 44), EmitenteCnpj = "12345678000190",
            EmitenteNome = "Fornecedor Teste", XmlProc = XmlEntrada,
            Status = NotaDestinadaStatus.XmlBaixado,
        };
        db.AddRange(product, nota);
        await db.SaveChangesAsync();

        await new NfeReceivingService(db).ReceiveAsync(nota.Id, new ReceiveNfeRequest
        {
            Items = [new ReceiveNfeItemRequest
            {
                ItemNumber = 1, ProductId = product.Id, Quantity = 2, UnitCostInCents = 1000,
            }],
        });

        db.ChangeTracker.Clear();
        var salvo = await db.Products.SingleAsync(p => p.Id == product.Id);
        var evidencia = await db.NfeReceiptItems.Include(i => i.NotaDestinada).SingleAsync();
        salvo.Ncm.Should().Be("95044000");
        evidencia.SourceNcm.Should().Be("95044000");
        evidencia.NotaDestinada.ChaveAcesso.Should().Be(new string('1', 44));
    }

    [Fact]
    public async Task ReceiveAsync_NcmDivergente_NaoSobrescreveCadastro()
    {
        await using var db = TestDbFactory.Create(nameof(ReceiveAsync_NcmDivergente_NaoSobrescreveCadastro));
        var product = new Product
        {
            Name = "Deck teste", Category = "TCG", PriceInCents = 2000,
            StockQuantity = 0, IsActive = true, Ncm = "49019900",
        };
        var nota = new NotaDestinada
        {
            ChaveAcesso = new string('2', 44), XmlProc = XmlEntrada,
            Status = NotaDestinadaStatus.XmlBaixado,
        };
        db.AddRange(product, nota);
        await db.SaveChangesAsync();

        await new NfeReceivingService(db).ReceiveAsync(nota.Id, new ReceiveNfeRequest
        {
            Items = [new ReceiveNfeItemRequest
            {
                ItemNumber = 1, ProductId = product.Id, Quantity = 2, UnitCostInCents = 1000,
            }],
        });

        db.ChangeTracker.Clear();
        (await db.Products.SingleAsync(p => p.Id == product.Id)).Ncm.Should().Be("49019900");
        (await db.NfeReceiptItems.SingleAsync()).SourceNcm.Should().Be("95044000");
    }
}
