// =============================================================================
// NfceRegimeNormalTests.cs — Emissão de NFC-e fora do Simples Nacional (CRT=3).
//
// Aqui o ICMS da operação própria é DESTACADO no XML (no Simples ele fica dentro
// do DAS), e PIS/COFINS deixam de ser "CST 99 zerado" para virar tributo com
// base e alíquota. São justamente os campos que a SEFAZ confere: um vBC errado
// ou um CST incompatível com o CRT derruba a autorização, e o lojista descobre
// no balcão. Estes testes prendem cada CST suportado e as duas tabelas de
// PIS/COFINS (cumulativo do Presumido e não-cumulativo do Real).
// =============================================================================

using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using NFe.Classes.Informacoes.Detalhe;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual.Tipos;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal.Tipos;

namespace CardGameStore.Tests.Services;

public class NfceRegimeNormalTests
{
    /// <summary>Item de R$ 100,00 com a transparência tributária já preenchida.</summary>
    private static NfceEmissionService.ItemFiscal Item(string cst) =>
        new(Nome: "Item Teste", Ncm: "95044000", Cfop: "5102", Csosn: null,
            PercentualCreditoSn: null, Quantidade: 1,
            PrecoUnitarioCentavos: 10_000, SubtotalCentavos: 10_000,
            PercentualTributosFederais: 10m, PercentualTributosEstaduais: 5m,
            PercentualTributosMunicipais: 0m, FonteTributos: "Tabela teste 2026")
        { Cst = cst };

    private static det Montar(
        NfceEmissionService.ItemFiscal item,
        RegimeTributario regime = RegimeTributario.LucroPresumido,
        int desconto = 0) =>
        NfceEmissionService.MontarItem(item, 1, desconto, regraIbsCbs: null, regime);

    // ── ICMS ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Cst00_DestacaIcmsSobreOValorDaOperacao()
    {
        var det = Montar(Item("00") with { AliquotaIcmsProprio = 18m });

        var icms = det.imposto.ICMS.TipoICMS.Should().BeOfType<ICMS00>().Subject;
        icms.CST.Should().Be(Csticms.Cst00);
        icms.vBC.Should().Be(100.00m);
        icms.pICMS.Should().Be(18m);
        icms.vICMS.Should().Be(18.00m);
    }

    [Fact]
    public void Cst00_ComDesconto_ReduzABaseDeCalculo()
    {
        // R$ 100,00 com R$ 25,00 de desconto incondicional → base de R$ 75,00.
        var det = Montar(Item("00") with { AliquotaIcmsProprio = 18m }, desconto: 2_500);

        var icms = (ICMS00)det.imposto.ICMS.TipoICMS;
        icms.vBC.Should().Be(75.00m);
        icms.vICMS.Should().Be(13.50m);
    }

    [Fact]
    public void Cst00_SemAliquota_ExplicaOQueFalta()
    {
        var act = () => Montar(Item("00"));

        act.Should().Throw<FiscalNaoConfiguradoException>()
           .WithMessage("*alíquota de ICMS da operação própria*");
    }

    [Fact]
    public void Cst20_AplicaReducaoDeBaseAntesDaAliquota()
    {
        // Base reduzida em 40% → R$ 60,00 × 18% = R$ 10,80.
        var det = Montar(Item("20") with { AliquotaIcmsProprio = 18m, PercentualReducaoBc = 40m });

        var icms = det.imposto.ICMS.TipoICMS.Should().BeOfType<ICMS20>().Subject;
        icms.pRedBC.Should().Be(40m);
        icms.vBC.Should().Be(60.00m);
        icms.vICMS.Should().Be(10.80m);
    }

    [Fact]
    public void Cst20_SemReducao_ExigeOPercentual()
    {
        // Sem redução o CST correto é 00, não 20 com redução zero — deixar passar
        // geraria um XML que diz "reduzida" e mostra a base cheia.
        var act = () => Montar(Item("20") with { AliquotaIcmsProprio = 18m });

        act.Should().Throw<FiscalNaoConfiguradoException>().WithMessage("*redução da base*");
    }

    [Theory]
    [InlineData("40", Csticms.Cst40)]
    [InlineData("41", Csticms.Cst41)]
    [InlineData("50", Csticms.Cst50)]
    public void CstsSemTributacao_NaoDestacamValor(string cst, Csticms esperado)
    {
        var det = Montar(Item(cst));

        var icms = det.imposto.ICMS.TipoICMS.Should().BeOfType<ICMS40>().Subject;
        icms.CST.Should().Be(esperado);
    }

    [Fact]
    public void Cst60_NaoRecalculaSt_ApenasInformaARetencaoAnterior()
    {
        // ST já foi recolhido pelo fornecedor: recalcular aqui cobraria o imposto
        // duas vezes. O grupo só repassa o que o contador cadastrou.
        var det = Montar(Item("60") with
        {
            Cest = "2806300",
            BaseStRetidaEmCentavos = 12_000,
            ValorStRetidoEmCentavos = 2_160,
        });

        var icms = det.imposto.ICMS.TipoICMS.Should().BeOfType<ICMS60>().Subject;
        icms.CST.Should().Be(Csticms.Cst60);
        icms.vBCSTRet.Should().Be(120.00m);
        icms.vICMSSTRet.Should().Be(21.60m);
    }

    [Fact]
    public void Cst60_SemDadosDaRetencao_OmiteOsCamposEmVezDeZerar()
    {
        // Boa parte do varejo não recebe esse dado do fornecedor. Zero explícito
        // afirmaria que não houve retenção — o correto é não informar.
        var det = Montar(Item("60") with { Cest = "2806300" });

        var icms = (ICMS60)det.imposto.ICMS.TipoICMS;
        icms.vBCSTRet.Should().BeNull();
        icms.vICMSSTRet.Should().BeNull();
    }

    [Fact]
    public void Cst10_SeparaOperacaoPropriaEStSemAlterarOTotalCobrado()
    {
        var item = Item("10") with
        {
            Cest = "2806300", ModalidadeBcSt = 4, PercentualMvaSt = 40m,
            AliquotaIcmsSt = 18m, AliquotaIcmsProprio = 12m,
        };

        var det = Montar(item);
        var icms = det.imposto.ICMS.TipoICMS.Should().BeOfType<ICMS10>().Subject;

        icms.CST.Should().Be(Csticms.Cst10);
        icms.vICMSST.Should().BeGreaterThan(0);
        icms.vBC.Should().Be(det.prod.vProd, "a operação própria é a base do ICMS destacado");
        // O preço de cadastro é final ao consumidor: produto + ST continua sendo R$ 100.
        (det.prod.vProd + icms.vICMSST).Should().BeApproximately(100.00m, 0.02m);
    }

    [Fact]
    public void Cst70_ReduzABaseDaOperacaoPropriaEMantemOSt()
    {
        var item = Item("70") with
        {
            Cest = "2806300", ModalidadeBcSt = 4, PercentualMvaSt = 40m,
            AliquotaIcmsSt = 18m, AliquotaIcmsProprio = 12m, PercentualReducaoBc = 30m,
        };

        var icms = (ICMS70)Montar(item).imposto.ICMS.TipoICMS;

        icms.pRedBC.Should().Be(30m);
        icms.vICMSST.Should().BeGreaterThan(0);
        icms.vBC.Should().BeLessThan(100m, "a base própria entra reduzida");
    }

    [Fact]
    public void CstNaoSuportado_ListaOsQueValem()
    {
        var act = () => Montar(Item("99"));

        act.Should().Throw<FiscalNaoConfiguradoException>().WithMessage("*00, 10, 20, 30, 40, 41, 50, 60, 70 ou 90*");
    }

    [Fact]
    public void SemCst_ApontaOndeCadastrar()
    {
        // Natureza cadastrada só com CSOSN e loja fora do Simples: precisa dizer
        // exatamente onde resolver, não só que "faltou configuração".
        var item = Item("00") with { Cst = null, Csosn = "102" };

        var act = () => Montar(item);

        act.Should().Throw<FiscalNaoConfiguradoException>()
           .WithMessage("*Naturezas de operação*");
    }

    [Fact]
    public void RegimeNormal_NaoAceitaCsosnNoLugarDoCst()
    {
        // O par CRT=3 com CSOSN é rejeitado pela SEFAZ. Falhar no cadastro é o
        // ponto certo; deixar passar viraria rejeição na hora da venda.
        var item = Item("00") with { Cst = null, Csosn = "201" };

        var act = () => Montar(item);

        act.Should().Throw<FiscalNaoConfiguradoException>();
    }

    // ── PIS / COFINS ─────────────────────────────────────────────────────────

    [Fact]
    public void Presumido_UsaAliquotasDoRegimeCumulativo()
    {
        var det = Montar(Item("00") with { AliquotaIcmsProprio = 18m });

        var pis = det.imposto.PIS.TipoPIS.Should().BeOfType<PISAliq>().Subject;
        pis.CST.Should().Be(CSTPIS.pis01);
        pis.vBC.Should().Be(100.00m);
        pis.pPIS.Should().Be(0.65m);
        pis.vPIS.Should().Be(0.65m);

        var cofins = det.imposto.COFINS.TipoCOFINS.Should().BeOfType<COFINSAliq>().Subject;
        cofins.pCOFINS.Should().Be(3.00m);
        cofins.vCOFINS.Should().Be(3.00m);
    }

    [Fact]
    public void LucroReal_UsaAliquotasDoRegimeNaoCumulativo()
    {
        var det = Montar(Item("00") with { AliquotaIcmsProprio = 18m }, RegimeTributario.LucroReal);

        ((PISAliq)det.imposto.PIS.TipoPIS).pPIS.Should().Be(1.65m);
        ((COFINSAliq)det.imposto.COFINS.TipoCOFINS).pCOFINS.Should().Be(7.60m);
    }

    [Fact]
    public void Simples_ContinuaComCst99Zerado()
    {
        // Garantia de não-regressão: no Simples os dois estão dentro do DAS e o
        // XML precisa continuar exatamente como era antes desta mudança.
        var item = Item("00") with { Cst = null, Csosn = "102" };

        var det = NfceEmissionService.MontarItem(item, 1, 0, null, RegimeTributario.SimplesNacional);

        var pis = det.imposto.PIS.TipoPIS.Should().BeOfType<PISOutr>().Subject;
        pis.CST.Should().Be(CSTPIS.pis99);
        pis.vPIS.Should().Be(0);
        ((COFINSOutr)det.imposto.COFINS.TipoCOFINS).CST.Should().Be(CSTCOFINS.cofins99);
    }

    [Fact]
    public void CstMonofasico_NaoDestacaBaseNemAliquota()
    {
        // Alíquota zero por monofasia (combustível, autopeça, bebida fria): a
        // SEFAZ espera o grupo "não tributado", sem vBC.
        var det = Montar(Item("00") with
        {
            AliquotaIcmsProprio = 18m, CstPis = "04", CstCofins = "04",
        });

        det.imposto.PIS.TipoPIS.Should().BeOfType<PISNT>()
           .Which.CST.Should().Be(CSTPIS.pis04);
        det.imposto.COFINS.TipoCOFINS.Should().BeOfType<COFINSNT>()
           .Which.CST.Should().Be(CSTCOFINS.cofins04);
    }

    [Fact]
    public void AliquotaDaNatureza_SobrepoeOPadraoDoRegime()
    {
        var det = Montar(Item("00") with
        {
            AliquotaIcmsProprio = 18m, AliquotaPis = 1.65m, AliquotaCofins = 7.6m,
        });

        ((PISAliq)det.imposto.PIS.TipoPIS).pPIS.Should().Be(1.65m);
        ((COFINSAliq)det.imposto.COFINS.TipoCOFINS).pCOFINS.Should().Be(7.6m);
    }

    [Fact]
    public void Cst49_UsaOGrupoDeOutrasOperacoesComValor()
    {
        var det = Montar(Item("00") with
        {
            AliquotaIcmsProprio = 18m, CstPis = "49", CstCofins = "49",
        });

        var pis = det.imposto.PIS.TipoPIS.Should().BeOfType<PISOutr>().Subject;
        pis.CST.Should().Be(CSTPIS.pis49);
        pis.vBC.Should().Be(100.00m);
        pis.vPIS.Should().Be(0.65m);
    }
}
