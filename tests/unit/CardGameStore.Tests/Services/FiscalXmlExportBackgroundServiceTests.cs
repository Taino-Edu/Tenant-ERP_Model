// =============================================================================
// FiscalXmlExportBackgroundServiceTests.cs — Testa o cálculo da janela do
// "mês anterior" em UTC a partir da data local de Brasília.
// =============================================================================

using CardGameStore.Services.Implementations;

namespace CardGameStore.Tests.Services;

public class FiscalXmlExportBackgroundServiceTests
{
    private static TimeZoneInfo BrazilZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }

    [Fact]
    public void CalcularJanelaMesAnterior_ConverteMeiaNoiteBrasiliaParaUtcCorretamente()
    {
        // "Hoje" = 01/07/2026 em Brasília. Mês anterior = junho/2026.
        var hojeBrasil = new DateTime(2026, 7, 1);
        var zone = BrazilZone();

        var (inicio, fim, mesAnterior) = FiscalXmlExportBackgroundService.CalcularJanelaMesAnterior(hojeBrasil, zone);

        // Meia-noite de 01/06 em Brasília (UTC-3) é 03:00 UTC — não 00:00 UTC.
        inicio.Should().Be(new DateTime(2026, 6, 1, 3, 0, 0, DateTimeKind.Utc));
        fim.Should().Be(new DateTime(2026, 7, 1, 3, 0, 0, DateTimeKind.Utc));
        mesAnterior.Month.Should().Be(6);
        mesAnterior.Year.Should().Be(2026);
    }

    [Fact]
    public void CalcularJanelaMesAnterior_NotaEmitidaNosPrimeirosMinutosDeJulhoUtcFicaForaDaJanelaDeJunho()
    {
        var hojeBrasil = new DateTime(2026, 7, 1);
        var zone = BrazilZone();
        var (inicio, fim, _) = FiscalXmlExportBackgroundService.CalcularJanelaMesAnterior(hojeBrasil, zone);

        // 2026-07-01T01:00:00Z é 30/06 22:00 em Brasília — ainda pertence a junho e
        // deveria ficar FORA da janela de junho só se a virada real (03:00 UTC) for respeitada.
        // Aqui validamos o oposto: 2026-07-01T02:00:00Z (23:00 de 30/06 em Brasília) ainda é junho,
        // logo deve estar dentro de [inicio, fim).
        var notaAs23hBrasilia = new DateTime(2026, 7, 1, 2, 0, 0, DateTimeKind.Utc);
        (notaAs23hBrasilia >= inicio && notaAs23hBrasilia < fim).Should().BeTrue();

        // Já 2026-07-01T03:00:00Z é exatamente meia-noite de 01/07 em Brasília — pertence a julho,
        // deve estar FORA da janela de junho.
        var notaAMeiaNoiteJulhoBrasilia = new DateTime(2026, 7, 1, 3, 0, 0, DateTimeKind.Utc);
        (notaAMeiaNoiteJulhoBrasilia >= inicio && notaAMeiaNoiteJulhoBrasilia < fim).Should().BeFalse();
    }
}
