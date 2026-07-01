// =============================================================================
// FiscalAlertCalculatorTests.cs — Testes da lógica de quando disparar o
// próximo alerta de vencimento do certificado (30/15/7/1 dias).
// =============================================================================

using CardGameStore.Services.Implementations;

namespace CardGameStore.Tests.Services;

public class FiscalAlertCalculatorTests
{
    [Theory]
    [InlineData(45, null, null)]   // ainda longe do vencimento — nenhum alerta
    [InlineData(30, null, 30)]     // bateu exatamente o limiar de 30 dias
    [InlineData(15, 30, 15)]       // já alertou 30, agora cruza o de 15
    [InlineData(20, 15, null)]     // já alertou 15, 20 dias não cruza nenhum limiar novo
    [InlineData(5, null, 7)]       // primeiro alerta já cai direto no limiar de 7
    [InlineData(5, 7, null)]       // já alertou 7, 5 dias não cruza o limiar de 1 ainda
    [InlineData(0, 7, 1)]          // vence hoje — cruza o limiar de 1
    [InlineData(-3, 1, null)]      // vencido e já alertado no limiar máximo — não repete
    public void ProximoLimiarParaAlertar_RetornaLimiarEsperado(int diasRestantes, int? ultimoAlertaLimiar, int? esperado)
    {
        var resultado = FiscalAlertCalculator.ProximoLimiarParaAlertar(diasRestantes, ultimoAlertaLimiar);
        resultado.Should().Be(esperado);
    }
}
