// =============================================================================
// ApuracaoTributariaServiceTests.cs — Tabelas do Simples Nacional e cálculo do
// Lucro Presumido.
//
// O comparativo dos dois regimes vira decisão de enquadramento do cliente do
// contador: uma parcela a deduzir trocada muda a alíquota efetiva em pontos
// percentuais inteiros e o erro passa despercebido, porque o número continua
// "parecendo certo". Estes testes prendem os valores das faixas, a fronteira do
// fator R e o adicional de IRPJ.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class ApuracaoTributariaServiceTests
{
    private static FiscalConfig Config(
        AnexoSimplesNacional anexo = AnexoSimplesNacional.I,
        decimal presuncaoIrpj = 8m, decimal presuncaoCsll = 12m,
        decimal icms = 0m, decimal iss = 0m) => new()
    {
        AnexoSimples            = anexo,
        PercentualPresuncaoIrpj = presuncaoIrpj,
        PercentualPresuncaoCsll = presuncaoCsll,
        AliquotaIcmsPercentual  = icms,
        AliquotaIssPercentual   = iss,
    };

    [Fact]
    public void Simples_PrimeiraFaixa_UsaAliquotaNominalSemDeducao()
    {
        // RBT12 de 120 mil (1ª faixa do Anexo I): não há parcela a deduzir,
        // então a efetiva é a própria nominal de 4%.
        var resultado = ApuracaoTributariaService.CalcularSimples(
            Config(), rbt12: 120_000m, receitaPeriodo: 10_000m, folha12m: 0m, new List<string>());

        resultado.Faixa.Should().Be(1);
        resultado.AliquotaEfetiva.Should().Be(4.00m);
        resultado.ValorDas.Should().Be(400m);
    }

    [Fact]
    public void Simples_FaixaComParcelaADeduzir_AplicaFormulaDaAliquotaEfetiva()
    {
        // Anexo I, 3ª faixa: RBT12 600.000 × 9,5% − 13.860 = 43.140 → 7,19%.
        var resultado = ApuracaoTributariaService.CalcularSimples(
            Config(), rbt12: 600_000m, receitaPeriodo: 50_000m, folha12m: 0m, new List<string>());

        resultado.Faixa.Should().Be(3);
        resultado.AliquotaNominal.Should().Be(9.50m);
        resultado.ParcelaDeduzir.Should().Be(13_860m);
        resultado.AliquotaEfetiva.Should().Be(7.19m);
        resultado.ValorDas.Should().Be(3_595m); // 50.000 × 7,19%
    }

    [Fact]
    public void Simples_AcimaDoSublimite_AvisaQueIcmsEIssSaemDoDas()
    {
        var alertas = new List<string>();
        var resultado = ApuracaoTributariaService.CalcularSimples(
            Config(), rbt12: 4_000_000m, receitaPeriodo: 300_000m, folha12m: 0m, alertas);

        resultado.ExcedeuSublimite.Should().BeTrue();
        resultado.ExcedeuLimite.Should().BeFalse();
        alertas.Should().Contain(a => a.Contains("sublimite"));
    }

    [Fact]
    public void Simples_AcimaDoLimite_MarcaDesenquadramento()
    {
        var alertas = new List<string>();
        var resultado = ApuracaoTributariaService.CalcularSimples(
            Config(), rbt12: 5_000_000m, receitaPeriodo: 400_000m, folha12m: 0m, alertas);

        resultado.ExcedeuLimite.Should().BeTrue();
        alertas.Should().Contain(a => a.Contains("4.800.000"));
    }

    [Theory]
    // Fator R é folha12m ÷ RBT12: 28% é a fronteira entre o Anexo III e o V.
    [InlineData(300_000, "III")] // 300k / 1M = 30% → III
    [InlineData(280_000, "III")] // exatamente 28% ainda é III
    [InlineData(200_000, "V")]   // 20% → V
    public void Simples_FatorR_ReclassificaEntreAnexosIIIeV(int folha, string anexoEsperado)
    {
        var resultado = ApuracaoTributariaService.CalcularSimples(
            Config(AnexoSimplesNacional.III), rbt12: 1_000_000m,
            receitaPeriodo: 80_000m, folha12m: folha, new List<string>());

        resultado.AnexoAplicado.Should().Be(anexoEsperado);
        resultado.AnexoConfigurado.Should().Be("III");
    }

    [Fact]
    public void Simples_SemHistorico_NaoDividePorZero()
    {
        var resultado = ApuracaoTributariaService.CalcularSimples(
            Config(), rbt12: 0m, receitaPeriodo: 5_000m, folha12m: 0m, new List<string>());

        resultado.AliquotaEfetiva.Should().Be(4.00m, "sem RBT12 não há o que deduzir da nominal");
        resultado.ValorDas.Should().Be(200m);
    }

    [Fact]
    public void Presumido_Comercio_SomaTributosSobreAsBasesPresumidas()
    {
        // Receita 100.000 no mês, comércio (8% IRPJ / 12% CSLL), sem ICMS/ISS
        // informados e sem folha.
        var resultado = ApuracaoTributariaService.CalcularPresumido(
            Config(), receita: 100_000m, folhaMensal: 0m, mesesNoPeriodo: 1m, new List<string>());

        resultado.BaseIrpj.Should().Be(8_000m);
        resultado.Irpj.Should().Be(1_200m);          // 15% de 8.000
        resultado.AdicionalIrpj.Should().Be(0m);      // base abaixo dos 20.000/mês
        resultado.BaseCsll.Should().Be(12_000m);
        resultado.Csll.Should().Be(1_080m);           // 9% de 12.000
        resultado.Pis.Should().Be(650m);              // 0,65%
        resultado.Cofins.Should().Be(3_000m);         // 3%
        resultado.Total.Should().Be(5_930m);
    }

    [Fact]
    public void Presumido_LucroAcimaDoLimiteMensal_CobraAdicionalDeIrpj()
    {
        // Receita 400.000 → base de IRPJ 32.000, ou seja 12.000 acima do limite
        // mensal de 20.000: adicional de 10% sobre o excedente = 1.200.
        var resultado = ApuracaoTributariaService.CalcularPresumido(
            Config(), receita: 400_000m, folhaMensal: 0m, mesesNoPeriodo: 1m, new List<string>());

        resultado.BaseIrpj.Should().Be(32_000m);
        resultado.AdicionalIrpj.Should().Be(1_200m);
    }

    [Fact]
    public void Presumido_PeriodoTrimestral_ProporcionalizaOLimiteDoAdicional()
    {
        // Em 3 meses o limite vira 60.000: a mesma base de 32.000 não paga adicional.
        var resultado = ApuracaoTributariaService.CalcularPresumido(
            Config(), receita: 400_000m, folhaMensal: 0m, mesesNoPeriodo: 3m, new List<string>());

        resultado.AdicionalIrpj.Should().Be(0m);
    }

    [Fact]
    public void Presumido_ComFolha_IncluiInssPatronalParaCompararComOSimples()
    {
        // O DAS dos anexos I a III já embute a CPP; sem somar os 20% da folha
        // aqui, o Presumido apareceria barato demais no comparativo.
        var resultado = ApuracaoTributariaService.CalcularPresumido(
            Config(), receita: 100_000m, folhaMensal: 10_000m, mesesNoPeriodo: 1m, new List<string>());

        resultado.InssPatronal.Should().Be(2_000m);
        resultado.Total.Should().Be(7_930m);
    }

    [Fact]
    public void Presumido_ComIcmsEIss_EntramNoTotal()
    {
        var resultado = ApuracaoTributariaService.CalcularPresumido(
            Config(icms: 4m, iss: 5m), receita: 100_000m, folhaMensal: 0m,
            mesesNoPeriodo: 1m, new List<string>());

        resultado.Icms.Should().Be(4_000m);
        resultado.Iss.Should().Be(5_000m);
        resultado.Total.Should().Be(14_930m);
    }

    [Fact]
    public void Presumido_PresuncaoDeServico_AvisaQuePodeEstarSuperestimado()
    {
        var alertas = new List<string>();
        ApuracaoTributariaService.CalcularPresumido(
            Config(presuncaoIrpj: 32m, presuncaoCsll: 32m), receita: 50_000m,
            folhaMensal: 0m, mesesNoPeriodo: 1m, alertas);

        alertas.Should().Contain(a => a.Contains("32%"));
    }
}
