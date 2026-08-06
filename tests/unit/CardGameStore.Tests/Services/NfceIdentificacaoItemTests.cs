// =============================================================================
// NfceIdentificacaoItemTests.cs — Identificação do item no XML (XML-001).
//
// Três defeitos que a SEFAZ rejeita ou que impedem cruzar a venda com o
// estoque, agora travados:
//   • cProd era a posição do item ("000001"), não o código do produto;
//   • cEAN saía sempre "SEM GTIN", ignorando o código de barras cadastrado;
//   • um código de barras interno malformado, mandado como GTIN, é rejeição 611.
//
// O dígito verificador GS1 é validado contra GTINs reais conhecidos — se o
// algoritmo estiver errado, um produto legítimo perderia o GTIN ou um inválido
// passaria.
// =============================================================================

using CardGameStore.Services.Implementations;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class NfceIdentificacaoItemTests
{
    private static NfceEmissionService.ItemFiscal Item(Guid? produtoId = null, string? gtin = null) =>
        new(Nome: "Produto Teste", Ncm: "95044000", Cfop: "5102", Csosn: "102",
            PercentualCreditoSn: null, Quantidade: 1, PrecoUnitarioCentavos: 1000, SubtotalCentavos: 1000,
            PercentualTributosFederais: 10m, PercentualTributosEstaduais: 5m,
            PercentualTributosMunicipais: 0m, FonteTributos: "Tabela teste 2026",
            ProdutoId: produtoId, Gtin: gtin);

    // ── cProd ────────────────────────────────────────────────────────────────

    [Fact]
    public void CProd_UsaOIdDoProduto_NaoAPosicao()
    {
        var id = Guid.NewGuid();

        NfceEmissionService.MontarCodigoProduto(Item(produtoId: id), numero: 3)
            .Should().Be(id.ToString("N"), "o código do produto tem que ser estável, não a posição na nota");
    }

    [Fact]
    public void CProd_SemIdDoProduto_CaiNaPosicaoComoUltimoRecurso()
    {
        NfceEmissionService.MontarCodigoProduto(Item(produtoId: null), numero: 7)
            .Should().Be("000007");
    }

    // ── GTIN ─────────────────────────────────────────────────────────────────

    [Theory]
    // GTINs reais com dígito verificador correto.
    [InlineData("7891910000197")] // EAN-13 (açúcar União, exemplo clássico)
    [InlineData("0075678164125")] // EAN-13
    [InlineData("40170725")]      // GTIN-8
    [InlineData("00012345678905")]// GTIN-14
    public void Gtin_Valido_EhAceito(string gtin)
    {
        NfceEmissionService.SanitizarGtin(gtin).Should().Be(gtin);
    }

    [Theory]
    [InlineData("7891910000198")] // último dígito trocado — DV errado
    [InlineData("1234567890123")] // sequência arbitrária
    [InlineData("789191000019")]  // 12 dígitos com DV que não fecha
    public void Gtin_ComDigitoVerificadorErrado_ViraNull(string gtin)
    {
        // Melhor não declarar GTIN nenhum do que declarar um inválido: cEAN
        // inválido é rejeição 611; "SEM GTIN" é aceito.
        NfceEmissionService.SanitizarGtin(gtin).Should().BeNull();
    }

    [Theory]
    [InlineData("123")]            // curto demais
    [InlineData("123456789012345")]// longo demais
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ABC")]            // sem dígitos
    public void Gtin_ComComprimentoInvalido_ViraNull(string? gtin)
    {
        NfceEmissionService.SanitizarGtin(gtin).Should().BeNull();
    }

    [Fact]
    public void Gtin_ComFormatacao_ExtraiSoOsDigitos()
    {
        NfceEmissionService.SanitizarGtin("7 891910 000197").Should().Be("7891910000197");
    }

    // ── xProd ────────────────────────────────────────────────────────────────

    [Fact]
    public void XProd_AcimaDe120Caracteres_EhTruncado()
    {
        var nomeLongo = new string('A', 200);

        var resultado = NfceEmissionService.SanitizarXProd(nomeLongo);

        resultado.Length.Should().Be(120, "o leiaute limita xProd a 120 caracteres");
    }

    [Fact]
    public void XProd_ColapsaEspacosEQuebras()
    {
        NfceEmissionService.SanitizarXProd("  Booster   box\ncolecao  ")
            .Should().Be("Booster box colecao");
    }

    [Fact]
    public void XProd_Vazio_RecebePlaceholder()
    {
        NfceEmissionService.SanitizarXProd("   ").Should().Be("Item sem descricao");
    }
}
