// =============================================================================
// BrazilTime.cs — Fuso horário de Brasília (UTC-3 fixo; BR não usa horário de
// verão desde 2019), centralizado. Antes duplicado em ~8 arquivos (Controllers
// e Services), cada um com sua própria cópia do lookup + fallback Windows/Linux.
// =============================================================================

namespace CardGameStore.Common;

public static class BrazilTime
{
    /// <summary>TimeZoneInfo de Brasília — funciona em Linux (IANA) e Windows (ID legado).</summary>
    public static readonly TimeZoneInfo Zone = GetZone();

    private static TimeZoneInfo GetZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }

    /// <summary>Converte uma data local de Brasília no início UTC daquele dia.
    /// Ex.: 29/05 BR (UTC-3) → 29/05 03:00:00 UTC</summary>
    public static DateTime DateToUtcStart(DateTime brDate) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(brDate.Date, DateTimeKind.Unspecified), Zone);

    /// <summary>Converte um DateTime (com hora, não só a data) interpretado como horário local
    /// de Brasília pro UTC correspondente — para janelas arbitrárias vindas de query string
    /// (model binding do ASP.NET Core gera Kind=Unspecified, e <c>.ToUniversalTime()</c> nesse
    /// caso assume o fuso do SERVIDOR, não o de Brasília: em container rodando em UTC isso vira
    /// no-op e desloca a janela em 3h — bug real de exportação fiscal encontrado nesta auditoria,
    /// F11).</summary>
    public static DateTime ToUtcFromLocal(DateTime brLocal) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(brLocal, DateTimeKind.Unspecified), Zone);

    /// <summary>Retorna o intervalo UTC correspondente a um dia no fuso de Brasília.
    /// Ex.: dia 29/05 BR → [29/05 03:00 UTC, 30/05 03:00 UTC). Sem argumento, usa hoje.</summary>
    public static (DateTime InicioUtc, DateTime FimUtc) Dia(DateTime? dia = null)
    {
        var agora  = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
        var dataBr = dia.HasValue ? dia.Value.Date : agora.Date;
        var inicioUtc = DateToUtcStart(dataBr);
        return (inicioUtc, inicioUtc.AddDays(1));
    }

    /// <summary>Agora, convertido pro horário local de Brasília (Kind=Unspecified).</summary>
    public static DateTime NowBr() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
}
