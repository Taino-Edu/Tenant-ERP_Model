// =============================================================================
// RegrasIbsCbsTests.cs — RTC-001: IBS/CBS por vigência e perfil, sem bloqueio
// fixo por ano.
//
// O defeito que motivou o cartão era simples de descrever e caro de sofrer: uma
// condição `ano >= 2027` no motor derrubava TODA a emissão na virada do
// calendário, até alguém alterar o código. O aceite do plano é literal —
// "virar a data em teste não causa parada geral; o XML muda somente conforme a
// regra versionada aplicável ao contribuinte".
//
// Por isso o teste central aqui não verifica alíquota: verifica que o motor
// continua produzindo documento em 2027, 2030 e 2040.
// =============================================================================

using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using DFe.Classes.Flags;

namespace CardGameStore.Tests.Services;

public class RegrasIbsCbsTests
{
    private static NfceEmissionService.ItemFiscal Item() => new(
        Nome: "Booster Pack",
        Ncm: "95044000",
        Cfop: "5102",
        Csosn: "102",
        PercentualCreditoSn: null,
        Quantidade: 1,
        PrecoUnitarioCentavos: 1000,
        SubtotalCentavos: 1000,
        PercentualTributosFederais: 10m,
        PercentualTributosEstaduais: 5m,
        PercentualTributosMunicipais: 0m,
        FonteTributos: "Tabela teste 2026");

    // ── O aceite do plano ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(2026)]
    [InlineData(2027)]
    [InlineData(2030)]
    [InlineData(2040)]
    public void Catalogo_QualquerAnoFuturo_ContinuaTendoRegraAplicavel(int ano)
    {
        // Este é o cartão inteiro: a passagem do tempo não pode deixar o motor
        // sem regra, porque "sem regra" virava exceção e exceção parava o caixa.
        var regra = CatalogoRegrasIbsCbs.Para(
            new DateOnly(ano, 6, 15), PerfilIbsCbs.SimplesNacional);

        regra.Should().NotBeNull($"virar para {ano} não pode deixar a emissão sem regra aplicável");
    }

    [Fact]
    public void Catalogo_UltimaFaixa_EhAberta()
    {
        // A propriedade estrutural que sustenta o teste acima. Se alguém fechar a
        // última faixa sem publicar a seguinte, o defeito de RTC-001 volta — e
        // este teste falha antes de chegar em produção.
        CatalogoRegrasIbsCbs.Todas
            .OrderByDescending(r => r.VigenciaInicio)
            .First()
            .EhAberta.Should().BeTrue(
                "fechar a última faixa sem publicar a próxima recria a parada geral na virada do ano");
    }

    [Fact]
    public void Catalogo_AntesDaVigenciaInicial_NaoTemRegra()
    {
        // 2025 é anterior à transição: não há IBS/CBS a destacar, e isso é
        // ausência legítima de regra — não é buraco no catálogo.
        CatalogoRegrasIbsCbs.Para(new DateOnly(2025, 12, 31), PerfilIbsCbs.SimplesNacional)
            .Should().BeNull();
    }

    // ── Perfil do contribuinte ────────────────────────────────────────────────

    [Theory]
    [InlineData(RegimeTributario.SimplesNacional, false, false, PerfilIbsCbs.SimplesNacional)]
    [InlineData(RegimeTributario.SimplesNacional, true, false, PerfilIbsCbs.SimplesExcessoSublimite)]
    [InlineData(RegimeTributario.SimplesNacional, false, true, PerfilIbsCbs.SimplesRegimeRegular)]
    [InlineData(RegimeTributario.SimplesNacional, true, true, PerfilIbsCbs.SimplesRegimeRegular)]
    [InlineData(RegimeTributario.LucroPresumido, false, false, PerfilIbsCbs.RegimeNormal)]
    [InlineData(RegimeTributario.LucroReal, true, true, PerfilIbsCbs.RegimeNormal)]
    public void PerfilDe_DiferenciaAsQuatroSituacoesDoPlano(
        RegimeTributario regime, bool excedeuSublimite, bool optouRegimeRegular, PerfilIbsCbs esperado)
    {
        CatalogoRegrasIbsCbs.PerfilDe(regime, excedeuSublimite, optouRegimeRegular)
            .Should().Be(esperado);
    }

    [Fact]
    public void PerfilDe_OpcaoPeloRegimeRegular_PrevaleceSobreSublimite()
    {
        // Quem optou pelo regime regular já está sob as regras dele; o excesso de
        // sublimite deixa de ser o critério que distingue.
        CatalogoRegrasIbsCbs.PerfilDe(RegimeTributario.SimplesNacional,
            excedeuSublimite: true, optouRegimeRegular: true)
            .Should().Be(PerfilIbsCbs.SimplesRegimeRegular);
    }

    [Fact]
    public void Catalogo_FaixaDeTransicao_ValeParaTodosOsPerfis()
    {
        // Em 2026 o destaque é o mesmo para todo mundo. O que importa é que a
        // SELEÇÃO por perfil já exista: quando 2027 trouxer alíquotas distintas,
        // é dado novo no catálogo, não código novo no motor.
        var data = new DateOnly(2026, 8, 6);

        foreach (var perfil in Enum.GetValues<PerfilIbsCbs>())
            CatalogoRegrasIbsCbs.Para(data, perfil).Should().NotBeNull($"perfil {perfil} precisa de regra em 2026");
    }

    // ── Regra versionada → XML ────────────────────────────────────────────────

    [Fact]
    public void MontarIbsCbs_UsaAsAliquotasDaRegra_NaoLiteraisDoMotor()
    {
        var regra = CatalogoRegrasIbsCbs.Para(new DateOnly(2026, 8, 6), PerfilIbsCbs.SimplesNacional)!;

        var det = NfceEmissionService.MontarItem(Item(), numero: 1, descontoCentavos: 200, regraIbsCbs: regra);

        var ibsCbs = det.imposto.IBSCBS!;
        ibsCbs.gIBSCBS!.vBC.Should().Be(8m, "a base subtrai o desconto incondicional (UB16-10)");
        ibsCbs.gIBSCBS.gIBSUF!.pIBSUF.Should().Be(regra.AliquotaIbsUf);
        ibsCbs.gIBSCBS.gIBSMun!.pIBSMun.Should().Be(regra.AliquotaIbsMun);
        ibsCbs.gIBSCBS.gCBS!.pCBS.Should().Be(regra.AliquotaCbs);
    }

    [Fact]
    public void MontarIbsCbs_ComRegraHipoteticaDeOutraFaixa_ProduzOutroValor()
    {
        // Prova que o valor do XML segue a REGRA e não um percentual fixo: a mesma
        // base, com outra faixa, dá outro imposto — sem tocar no motor.
        var faixaFutura = new RegraIbsCbs(
            Versao: "hipotetica",
            VigenciaInicio: new DateOnly(2030, 1, 1),
            VigenciaFim: null,
            Perfis: new[] { PerfilIbsCbs.SimplesNacional },
            AliquotaIbsUf: 10m,
            AliquotaIbsMun: 2m,
            AliquotaCbs: 8m,
            CstSuportados: new[] { "000" },
            DestaqueObrigatorio: true,
            FonteOficial: "fixture de teste",
            ConsultadoEm: new DateOnly(2030, 1, 1),
            Observacao: "cenário hipotético");

        var det = NfceEmissionService.MontarItem(Item(), 1, 0, faixaFutura);

        var valores = det.imposto.IBSCBS!.gIBSCBS!;
        valores.vBC.Should().Be(10m);
        valores.gIBSUF!.vIBSUF.Should().Be(1m);
        valores.gIBSMun!.vIBSMun.Should().Be(0.2m);
        valores.vIBS.Should().Be(1.2m, "o IBS total soma a parcela estadual e a municipal");
        valores.gCBS!.vCBS.Should().Be(0.8m);
    }

    [Fact]
    public void MontarItem_SemRegra_NaoDestacaIbsCbs()
    {
        // Ausência de regra é ausência de grupo — nunca uma exceção que impede a
        // venda de virar documento.
        var det = NfceEmissionService.MontarItem(Item(), 1, 0, regraIbsCbs: null);

        det.imposto.IBSCBS.Should().BeNull();
    }

    [Fact]
    public void MontarIbsCbs_CstForaDaRegra_RecusaAntesDeReservarNumeracao()
    {
        var regra = CatalogoRegrasIbsCbs.Para(new DateOnly(2026, 8, 6), PerfilIbsCbs.SimplesNacional)!;
        var itemComCstNaoSuportado = Item() with { IbsCbsCst = "200" };

        var act = () => NfceEmissionService.MontarItem(itemComCstNaoSuportado, 1, 0, regra);

        act.Should().Throw<FiscalNaoConfiguradoException>()
            .WithMessage("*não é calculável pela regra 2026.1-transicao*",
                "emitir valor inventado seria pior do que recusar antes de queimar numeração");
    }

    [Fact]
    public void MontarTotaisIbsCbs_SomaOsItensIndependenteDaRegra()
    {
        var regra = CatalogoRegrasIbsCbs.Para(new DateOnly(2026, 8, 6), PerfilIbsCbs.SimplesNacional)!;
        var itens = new[]
        {
            NfceEmissionService.MontarItem(Item(), 1, 200, regra),
            NfceEmissionService.MontarItem(Item(), 2, 0, regra),
        };

        var total = NfceEmissionService.MontarTotaisIbsCbs(itens);

        total.vBCIBSCBS.Should().Be(18m);
        total.gIBS!.gIBSUF!.vIBSUF.Should().Be(0.02m);
        total.gIBS.vIBS.Should().Be(0.02m);
        total.gCBS!.vCBS.Should().Be(0.16m);
    }

    // ── Quando a regra vira destaque no XML ───────────────────────────────────

    [Fact]
    public void RegraParaDestaque_Em2026Producao_NaoDestaca()
    {
        // Comportamento já homologado, preservado de propósito: em 2026 o
        // destaque é informativo e há dispensa de penalidades pela omissão.
        // Se acrescentar a regra ao catálogo tivesse ligado o destaque em
        // produção, isso teria mudado silenciosamente o XML de tenants reais.
        var regra = CatalogoRegrasIbsCbs.Para(new DateOnly(2026, 8, 6), PerfilIbsCbs.SimplesNacional)!;

        NfceEmissionService.RegraParaDestaque(regra, TipoAmbiente.Producao)
            .Should().BeNull();
    }

    [Fact]
    public void RegraParaDestaque_Homologacao_SempreDestaca()
    {
        var regra = CatalogoRegrasIbsCbs.Para(new DateOnly(2026, 8, 6), PerfilIbsCbs.SimplesNacional)!;

        NfceEmissionService.RegraParaDestaque(regra, TipoAmbiente.Homologacao)
            .Should().BeSameAs(regra, "é em homologação que se testa o leiaute novo antes de ele valer");
    }

    [Fact]
    public void RegraParaDestaque_QuandoARegraTornaODestaqueObrigatorio_DestacaEmProducao()
    {
        // A virada de 2027 é edição de dado no catálogo, não alteração de motor.
        var obrigatoria = CatalogoRegrasIbsCbs.Para(new DateOnly(2026, 8, 6), PerfilIbsCbs.SimplesNacional)!
            with { DestaqueObrigatorio = true };

        NfceEmissionService.RegraParaDestaque(obrigatoria, TipoAmbiente.Producao)
            .Should().BeSameAs(obrigatoria);
    }

    [Fact]
    public void RegraParaDestaque_SemRegra_NaoDestacaEmNenhumAmbiente()
    {
        NfceEmissionService.RegraParaDestaque(null, TipoAmbiente.Homologacao).Should().BeNull();
        NfceEmissionService.RegraParaDestaque(null, TipoAmbiente.Producao).Should().BeNull();
    }

    // ── Rastreabilidade da fonte ──────────────────────────────────────────────

    [Fact]
    public void Catalogo_TodaFaixaRegistraFonteOficialEDataDeConsulta()
    {
        // "registrar a fonte oficial e a versão usada em cada alteração" é
        // requisito do cartão: uma alíquota sem procedência não é auditável.
        foreach (var regra in CatalogoRegrasIbsCbs.Todas)
        {
            regra.Versao.Should().NotBeNullOrWhiteSpace();
            regra.FonteOficial.Should().NotBeNullOrWhiteSpace($"a faixa {regra.Versao} precisa citar a fonte");
            regra.ConsultadoEm.Should().BeOnOrAfter(regra.VigenciaInicio.AddYears(-1));
            regra.Perfis.Should().NotBeEmpty();
            regra.CstSuportados.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void Catalogo_FaixasNaoSeSobrepoemParaOMesmoPerfil()
    {
        // Sobreposição tornaria a regra aplicada dependente da ordem da lista —
        // ou seja, o XML mudaria por acidente de implementação.
        foreach (var perfil in Enum.GetValues<PerfilIbsCbs>())
        {
            var doPerfil = CatalogoRegrasIbsCbs.Todas
                .Where(r => r.AplicaA(perfil))
                .OrderBy(r => r.VigenciaInicio)
                .ToList();

            for (var i = 1; i < doPerfil.Count; i++)
            {
                var anterior = doPerfil[i - 1];
                anterior.VigenciaFim.Should().NotBeNull(
                    $"a faixa {anterior.Versao} precisa terminar antes de {doPerfil[i].Versao} começar");
                anterior.VigenciaFim!.Value.Should().BeBefore(doPerfil[i].VigenciaInicio);
            }
        }
    }
}
