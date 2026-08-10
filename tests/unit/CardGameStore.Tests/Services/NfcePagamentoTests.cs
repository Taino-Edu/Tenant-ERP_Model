// =============================================================================
// NfcePagamentoTests.cs — Códigos de meio de pagamento da NFC-e (FIS-002).
//
// Crediário, pontos e cashback caíam todos em tPag 99 ("Outros"). Existem
// códigos próprios e vigentes — 05 para crediário e 19 para fidelidade/cashback
// — e usar 99 no lugar deles é o tipo de imprecisão que autoriza em produção e
// aparece em auditoria, ainda mais numa loja onde crediário e cashback são o
// modelo do negócio. Não havia teste sobre a montagem do pagamento, e foi por
// isso que a troca passou despercebida; estes travam cada código.
// =============================================================================

using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using NFe.Classes.Informacoes.Pagamento;
using PagamentoTipos = NFe.Classes.Informacoes.Pagamento;
using Xunit;

namespace CardGameStore.Tests.Services;

public class NfcePagamentoTests
{
    private static detPag Unico(string forma) =>
        NfceEmissionService.MontarDetPag(forma, null, 0, 100m).Should().ContainSingle().Subject;

    [Theory]
    [InlineData(PaymentMethod.Dinheiro,      PagamentoTipos.FormaPagamento.fpDinheiro)]
    [InlineData(PaymentMethod.Pix,           PagamentoTipos.FormaPagamento.fpPagamentoInstantaneoPIXDinamico)]
    [InlineData(PaymentMethod.CartaoCredito, PagamentoTipos.FormaPagamento.fpCartaoCredito)]
    [InlineData(PaymentMethod.CartaoDebito,  PagamentoTipos.FormaPagamento.fpCartaoDebito)]
    public void MeiosComuns_MantemSeuCodigo(string forma, PagamentoTipos.FormaPagamento esperado)
    {
        Unico(forma).tPag.Should().Be(esperado);
    }

    [Fact]
    public void Crediario_UsaTPag05_NaoMais99()
    {
        // fpCartaoDaLoja == 05: "Cartão da Loja, Crediário Digital, Outros
        // Crediários" (Informe Técnico 2024.002).
        var pag = Unico(PaymentMethod.Crediario);

        pag.tPag.Should().Be(PagamentoTipos.FormaPagamento.fpCartaoDaLoja);
        pag.tPag.Should().NotBe(PagamentoTipos.FormaPagamento.fpOutro);
        pag.xPag.Should().BeNullOrEmpty("código próprio dispensa a descrição do 99");
    }

    [Theory]
    [InlineData(PaymentMethod.Pontos)]
    [InlineData(PaymentMethod.Cashback)]
    public void PontosECashback_UsamTPag19(string forma)
    {
        // fpProgramadefidelidade == 19: "Programa de fidelidade, Cashback,
        // Crédito Virtual".
        var pag = Unico(forma);

        pag.tPag.Should().Be(PagamentoTipos.FormaPagamento.fpProgramadefidelidade);
        pag.xPag.Should().BeNullOrEmpty();
    }

    [Fact]
    public void CartaoEPix_LevamGrupoCardNaoIntegrado()
    {
        // A SEFAZ exige o grupo card para todo pagamento eletrônico; sem TEF, o
        // mínimo aceito é tpIntegra = Não integrado.
        foreach (var forma in new[] { PaymentMethod.CartaoCredito, PaymentMethod.CartaoDebito, PaymentMethod.Pix })
        {
            var pag = Unico(forma);
            pag.card.Should().NotBeNull($"{forma} é pagamento eletrônico");
            pag.card!.tpIntegra.Should().Be(TipoIntegracaoPagamento.TipNaoIntegrado);
        }
    }

    [Fact]
    public void CrediarioNaoRecebeGrupoCard()
    {
        // Crediário próprio não é operação de cartão bandeirado: o grupo card
        // não se aplica.
        Unico(PaymentMethod.Crediario).card.Should().BeNull();
    }

    [Fact]
    public void SemSegundoMeio_GeraUmUnicoPagamentoComOTotal()
    {
        var pags = NfceEmissionService.MontarDetPag(PaymentMethod.Pix, null, 0, 90m);

        pags.Should().ContainSingle();
        pags[0].vPag.Should().Be(90m);
    }

    [Fact]
    public void Split_DividePreservandoAExatidaoDoTotal()
    {
        // Pix (R$ 70) + cashback (R$ 30) — a soma dos detPag tem que bater o vNF
        // exatamente, senão a SEFAZ rejeita por diferença de pagamento.
        var pags = NfceEmissionService.MontarDetPag(
            PaymentMethod.Pix, PaymentMethod.Cashback, segundoValorCentavos: 3000, valorTotal: 100m);

        pags.Should().HaveCount(2);
        pags[0].tPag.Should().Be(PagamentoTipos.FormaPagamento.fpPagamentoInstantaneoPIXDinamico);
        pags[0].vPag.Should().Be(70m);
        pags[1].tPag.Should().Be(PagamentoTipos.FormaPagamento.fpProgramadefidelidade);
        pags[1].vPag.Should().Be(30m);
        pags.Sum(p => p.vPag).Should().Be(100m);
    }

    [Fact]
    public void Split_CrediarioMaisPix_UsaCodigosProprios()
    {
        var pags = NfceEmissionService.MontarDetPag(
            PaymentMethod.Crediario, PaymentMethod.Pix, segundoValorCentavos: 4000, valorTotal: 100m);

        pags[0].tPag.Should().Be(PagamentoTipos.FormaPagamento.fpCartaoDaLoja);
        pags[0].vPag.Should().Be(60m);
        pags[1].tPag.Should().Be(PagamentoTipos.FormaPagamento.fpPagamentoInstantaneoPIXDinamico);
        pags.Should().OnlyContain(p => string.IsNullOrEmpty(p.xPag),
            "nenhum meio do split cai mais no 99");
    }
}
