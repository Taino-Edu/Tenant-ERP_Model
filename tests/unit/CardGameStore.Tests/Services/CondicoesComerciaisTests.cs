// =============================================================================
// CondicoesComerciaisTests.cs — A conta pura: descontos empilhados, parcelas da
// implantação e dia de vencimento negociado.
//
// É a mesma conta que o gerador, o recálculo e a prévia da tela usam. Errar
// aqui é cobrar valor diferente do combinado — o tipo de erro que o lojista
// descobre antes da gente.
// =============================================================================

using CardGameStore.Multitenancy;
using CardGameStore.Services;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class CondicoesComerciaisTests
{
    private static DateTime Mes(int ano, int mes) => new(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);
    private static DateTime Dia(int ano, int mes, int dia) => new(ano, mes, dia, 0, 0, 0, DateTimeKind.Utc);

    private static TenantBillingDiscount Desconto(
        TenantDiscountKind tipo, decimal valor, DateTime inicio, DateTime? fim = null, string nome = "Desconto")
        => new() { Kind = tipo, Value = valor, StartMonth = inicio, EndMonth = fim, Description = nome };

    // ── Descontos ────────────────────────────────────────────────────────────

    [Fact]
    public void Mensalidade_SemDesconto_SaiCheia()
    {
        var calculo = CondicoesComerciais.CalcularMensalidade(129m, [], Mes(2026, 10));

        calculo.Valor.Should().Be(129m);
        calculo.Desconto.Should().Be(0m);
        calculo.DescricaoDesconto.Should().BeNull();
    }

    [Fact]
    public void Descontos_PercentuaisSomamENaoCompoem_DepoisAbateOFixo()
    {
        var descontos = new[]
        {
            // Cadastrado primeiro de propósito: a ordem de cadastro não pode
            // mudar a conta.
            Desconto(TenantDiscountKind.ValorFixo, 30m, Mes(2026, 1), nome: "Boas-vindas"),
            Desconto(TenantDiscountKind.Percentual, 10m, Mes(2026, 1), nome: "Parceiro"),
            Desconto(TenantDiscountKind.Percentual, 20m, Mes(2026, 1), nome: "Campanha"),
        };

        var calculo = CondicoesComerciais.CalcularMensalidade(200m, descontos, Mes(2026, 10));

        // 200 − 30% = 140; 140 − 30 = 110. Compondo (10% e depois 20%) daria 144 − 30.
        calculo.Valor.Should().Be(110m);
        calculo.Desconto.Should().Be(90m);
        calculo.DescricaoDesconto.Should().Be("Parceiro 10% + Campanha 20% + Boas-vindas R$ 30,00");
    }

    [Fact]
    public void Descontos_QuePassamDoValor_ZeramSemFicarNegativo()
    {
        var descontos = new[]
        {
            Desconto(TenantDiscountKind.Percentual, 80m, Mes(2026, 1)),
            Desconto(TenantDiscountKind.Percentual, 50m, Mes(2026, 1)),
            Desconto(TenantDiscountKind.ValorFixo, 500m, Mes(2026, 1)),
        };

        var calculo = CondicoesComerciais.CalcularMensalidade(129m, descontos, Mes(2026, 10));

        calculo.Valor.Should().Be(0m);
        calculo.Desconto.Should().Be(129m);
    }

    [Theory]
    [InlineData(9, 129)]   // antes de começar
    [InlineData(10, 99)]   // primeiro mês
    [InlineData(12, 99)]   // último mês, inclusive
    [InlineData(13, 129)]  // janeiro seguinte: acabou
    public void Desconto_ValeSoDentroDaVigencia(int mes, int esperado)
    {
        var descontos = new[] { Desconto(TenantDiscountKind.ValorFixo, 30m, Mes(2026, 10), Mes(2026, 12)) };
        var competencia = Mes(2026, 1).AddMonths(mes - 1);

        CondicoesComerciais.CalcularMensalidade(129m, descontos, competencia).Valor.Should().Be(esperado);
    }

    [Fact]
    public void Desconto_SemFim_ValeParaSempre()
    {
        var descontos = new[] { Desconto(TenantDiscountKind.Percentual, 10m, Mes(2026, 10)) };

        CondicoesComerciais.CalcularMensalidade(100m, descontos, Mes(2031, 5)).Valor.Should().Be(90m);
    }

    // ── Vencimento ───────────────────────────────────────────────────────────

    [Fact]
    public void Vencimento_PrimeiraNoInicioCombinado_DemaisNoDiaNegociado()
    {
        var tenant = new Tenant { BillingStartsOn = Dia(2026, 10, 20), BillingDueDay = 5 };

        CondicoesComerciais.VencimentoDaMensalidade(tenant, Mes(2026, 9)).Should().BeNull();
        CondicoesComerciais.VencimentoDaMensalidade(tenant, Mes(2026, 10)).Should().Be(Dia(2026, 10, 20));
        CondicoesComerciais.VencimentoDaMensalidade(tenant, Mes(2026, 11)).Should().Be(Dia(2026, 11, 5));
    }

    [Fact]
    public void Vencimento_SemDiaNegociado_UsaODiaDoInicio()
    {
        var tenant = new Tenant { BillingStartsOn = Dia(2026, 10, 20) };

        CondicoesComerciais.VencimentoDaMensalidade(tenant, Mes(2026, 12)).Should().Be(Dia(2026, 12, 20));
    }

    [Fact]
    public void Vencimento_Dia31EmFevereiro_CaiNoUltimoDia()
    {
        var tenant = new Tenant { BillingStartsOn = Dia(2026, 10, 1), BillingDueDay = 31 };

        CondicoesComerciais.VencimentoDaMensalidade(tenant, Mes(2027, 2)).Should().Be(Dia(2027, 2, 28));
    }

    // ── Implantação ──────────────────────────────────────────────────────────

    [Fact]
    public void Implantacao_Parcelada_SomaExatamenteOCombinado()
    {
        var parcelas = CondicoesComerciais.ParcelasDaImplantacao(100m, 3, Dia(2026, 10, 15));

        parcelas.Select(p => p.Valor).Should().Equal(33.33m, 33.33m, 33.34m);
        parcelas.Sum(p => p.Valor).Should().Be(100m);
        parcelas.Select(p => p.Vencimento).Should().Equal(Dia(2026, 10, 15), Dia(2026, 11, 15), Dia(2026, 12, 15));
        parcelas.Should().OnlyContain(p => p.Total == 3);
    }

    [Fact]
    public void Implantacao_ComParcelaPaga_RedistribuiSoOSaldo()
    {
        // Eram 3 de 100; a primeira foi paga, e a negociação passou pra 4.
        var pagas = new Dictionary<int, decimal> { [1] = 100m };

        var parcelas = CondicoesComerciais.ParcelasDaImplantacao(300m, 4, Dia(2026, 10, 15), pagas);

        parcelas.Select(p => p.Valor).Should().Equal(100m, 66.66m, 66.66m, 66.68m);
    }

    [Fact]
    public void Implantacao_SemDataOuSemValor_NaoTemParcela()
    {
        CondicoesComerciais.ParcelasDaImplantacao(500m, 2, null).Should().BeEmpty();
        CondicoesComerciais.ParcelasDaImplantacao(0m, 2, Dia(2026, 10, 1)).Should().BeEmpty();
    }

    [Fact]
    public void Implantacao_PrimeiroVencimentoPadrao_NuncaEhHoje()
    {
        var hoje = Dia(2026, 9, 14);

        // Ainda no teste: vai junto com a primeira mensalidade.
        CondicoesComerciais.PrimeiroVencimentoPadraoDaImplantacao(new Tenant { BillingStartsOn = Dia(2026, 9, 29) }, hoje)
            .Should().Be(Dia(2026, 9, 29));

        // Já cobrando: sete dias pra frente.
        CondicoesComerciais.PrimeiroVencimentoPadraoDaImplantacao(new Tenant { BillingStartsOn = Dia(2026, 5, 1) }, hoje)
            .Should().Be(Dia(2026, 9, 21));
    }
}
