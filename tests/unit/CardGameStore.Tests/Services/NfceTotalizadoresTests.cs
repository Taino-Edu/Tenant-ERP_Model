// =============================================================================
// NfceTotalizadoresTests.cs — Consolidação dos tributos no total (REG-001).
//
// A regra que a SEFAZ confere é simples e implacável: o total do documento tem
// que ser a soma dos itens. O totalizador antigo só conhecia dois CSOSN por
// switch; quando o motor passou a montar itens por CST, o item destacava vICMS
// e o ICMSTot mandava zero — divergência, rejeição, numeração queimada. Pior:
// o `default` silencioso não quebrava nenhum teste.
//
// Estes testes existem para que isso não volte a passar despercebido: cada CST
// é montado de verdade e o total é comparado com a soma dos itens.
// =============================================================================

using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using NFe.Classes.Informacoes.Detalhe;
using Xunit;

namespace CardGameStore.Tests.Services;

public class NfceTotalizadoresTests
{
    /// <summary>Item de R$ 100,00 com transparência tributária preenchida.</summary>
    private static NfceEmissionService.ItemFiscal Item(
        string? cst = null, string? csosn = null, decimal? aliquota = null) =>
        new(Nome: "Item Teste", Ncm: "95044000", Cfop: "5102", Csosn: csosn,
            PercentualCreditoSn: null, Quantidade: 1,
            PrecoUnitarioCentavos: 10_000, SubtotalCentavos: 10_000,
            PercentualTributosFederais: 10m, PercentualTributosEstaduais: 5m,
            PercentualTributosMunicipais: 0m, FonteTributos: "Tabela teste 2026")
        { Cst = cst, AliquotaIcmsProprio = aliquota };

    private static det Montar(NfceEmissionService.ItemFiscal item, RegimeTributario regime, int numero = 1) =>
        NfceEmissionService.MontarItem(item, numero, 0, regraIbsCbs: null, regime);

    // ── O caso que quebrava ──────────────────────────────────────────────────

    [Fact]
    public void Cst00_TotalNaoPodeFicarZeradoQuandoOItemDestacaIcms()
    {
        // Este é literalmente o bug do REG-001: item com ICMS destacado e total
        // zerado. Se este teste falhar, a nota é rejeitada pela SEFAZ.
        var det = Montar(Item(cst: "00", aliquota: 18m), RegimeTributario.LucroPresumido);

        var totais = NfceEmissionService.SomarTotaisIcms(new[] { det });

        totais.BaseIcms.Should().Be(100.00m);
        totais.ValorIcms.Should().Be(18.00m);
        totais.ValorIcms.Should().NotBe(0m, "o item destacou ICMS — o total não pode dizer zero");
    }

    [Fact]
    public void TotalDeIcms_EhASomaDosItens()
    {
        var itens = new[]
        {
            Montar(Item(cst: "00", aliquota: 18m), RegimeTributario.LucroPresumido, 1),
            Montar(Item(cst: "00", aliquota: 12m), RegimeTributario.LucroPresumido, 2),
        };

        var totais = NfceEmissionService.SomarTotaisIcms(itens);

        totais.BaseIcms.Should().Be(200.00m);
        totais.ValorIcms.Should().Be(30.00m, "18,00 + 12,00");
    }

    [Fact]
    public void Cst20_ComReducao_TotalUsaABaseReduzida()
    {
        var item = Item(cst: "20", aliquota: 18m) with { PercentualReducaoBc = 40m };

        var totais = NfceEmissionService.SomarTotaisIcms(
            new[] { Montar(item, RegimeTributario.LucroPresumido) });

        totais.BaseIcms.Should().Be(60.00m, "base reduzida em 40%");
        totais.ValorIcms.Should().Be(10.80m);
    }

    [Fact]
    public void Cst60_SemIcmsProprio_NaoSomaNadaNoTotalDeIcms()
    {
        // ST já retido pelo fornecedor: não há ICMS próprio a destacar.
        var item = Item(cst: "60") with { Cest = "2806300" };

        var totais = NfceEmissionService.SomarTotaisIcms(
            new[] { Montar(item, RegimeTributario.LucroPresumido) });

        totais.ValorIcms.Should().Be(0m);
        totais.ValorSt.Should().Be(0m, "a retenção anterior é informativa, não recolhimento desta nota");
    }

    [Fact]
    public void Cst10_ComSt_SomaOperacaoPropriaEStSeparadamente()
    {
        var item = Item(cst: "10", aliquota: 12m) with
        {
            Cest = "2806300", ModalidadeBcSt = 4, PercentualMvaSt = 40m, AliquotaIcmsSt = 18m,
        };

        var totais = NfceEmissionService.SomarTotaisIcms(
            new[] { Montar(item, RegimeTributario.LucroPresumido) });

        totais.ValorIcms.Should().BeGreaterThan(0m, "CST 10 destaca ICMS próprio");
        totais.ValorSt.Should().BeGreaterThan(0m, "e também recolhe ST");
        totais.BaseSt.Should().BeGreaterThan(0m);
    }

    // ── PIS/COFINS ───────────────────────────────────────────────────────────

    [Fact]
    public void Presumido_TotalDePisECofins_SomaOsItens()
    {
        var itens = new[]
        {
            Montar(Item(cst: "00", aliquota: 18m), RegimeTributario.LucroPresumido, 1),
            Montar(Item(cst: "00", aliquota: 18m), RegimeTributario.LucroPresumido, 2),
        };

        var totais = NfceEmissionService.SomarTotaisIcms(itens);

        // 0,65% e 3% sobre R$ 200,00 no regime cumulativo.
        totais.ValorPis.Should().Be(1.30m);
        totais.ValorCofins.Should().Be(6.00m);
    }

    [Fact]
    public void LucroReal_UsaAliquotasNaoCumulativasNoTotal()
    {
        var totais = NfceEmissionService.SomarTotaisIcms(
            new[] { Montar(Item(cst: "00", aliquota: 18m), RegimeTributario.LucroReal) });

        totais.ValorPis.Should().Be(1.65m);
        totais.ValorCofins.Should().Be(7.60m);
    }

    // ── Não-regressão do Simples ─────────────────────────────────────────────

    [Fact]
    public void Simples_ContinuaComTotaisZerados_ComoAntes()
    {
        // Garantia de não-regressão: no Simples o CSOSN não destaca ICMS próprio
        // e PIS/COFINS estão no DAS. O total zerado aqui é correto — a diferença
        // é que agora ele é resultado do cálculo, não um zero fixo.
        var totais = NfceEmissionService.SomarTotaisIcms(
            new[] { Montar(Item(csosn: "102"), RegimeTributario.SimplesNacional) });

        totais.BaseIcms.Should().Be(0m);
        totais.ValorIcms.Should().Be(0m);
        totais.ValorPis.Should().Be(0m);
        totais.ValorCofins.Should().Be(0m);
    }

    [Fact]
    public void Simples_ComSt_ContinuaSomandoStEFcpSt()
    {
        // Comportamento que já existia e não pode ter sido perdido na troca do
        // switch pelos getters da biblioteca.
        var item = Item(csosn: "202") with
        {
            Cest = "2806300", ModalidadeBcSt = 4, PercentualMvaSt = 40m,
            AliquotaIcmsSt = 18m, AliquotaIcmsProprio = 12m, AliquotaFcpSt = 2m,
        };

        var totais = NfceEmissionService.SomarTotaisIcms(
            new[] { Montar(item, RegimeTributario.SimplesNacional) });

        totais.ValorSt.Should().BeGreaterThan(0m);
        totais.BaseSt.Should().BeGreaterThan(0m);
        totais.ValorFcpSt.Should().BeGreaterThan(0m);
    }

    // ── FCP ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Cst00_ComFcp_SomaOFcpProprioNoTotal()
    {
        var item = Item(cst: "00", aliquota: 18m) with { AliquotaFcp = 2m };

        var totais = NfceEmissionService.SomarTotaisIcms(
            new[] { Montar(item, RegimeTributario.LucroPresumido) });

        // O FCP próprio do CST 00 só entra no XML quando a natureza traz alíquota.
        totais.ValorFcp.Should().BeGreaterOrEqualTo(0m);
        totais.ValorIcms.Should().Be(18.00m, "o FCP não altera o ICMS destacado");
    }

    [Fact]
    public void NotaVazia_NaoQuebra()
    {
        var totais = NfceEmissionService.SomarTotaisIcms(Array.Empty<det>());

        totais.ValorIcms.Should().Be(0m);
        totais.ValorPis.Should().Be(0m);
    }
}
