// =============================================================================
// FiscalXmlExportBackgroundService.cs — Todo dia 1 (fuso de Brasília), gera e
// envia por email ao contador o ZIP com os XMLs do mês anterior.
// =============================================================================

using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class FiscalXmlExportBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FiscalXmlExportBackgroundService> _logger;

    public FiscalXmlExportBackgroundService(IServiceScopeFactory scopeFactory, ILogger<FiscalXmlExportBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(3), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Filtro de data no nível do job (não por tenant): só no dia 1 há
                // trabalho a fazer — evita abrir scope de catálogo à toa nos outros dias.
                var hojeBrasil = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilTime.Zone).Date;
                if (hojeBrasil.Day == 1)
                    await _scopeFactory.ForEachActiveTenantAsync(_logger, CheckAsync, ct, requiredModule: "fiscal");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no envio mensal automático de XMLs fiscais");
            }

            await Task.Delay(TimeSpan.FromHours(12), ct);
        }
    }

    private async Task CheckAsync(IServiceProvider sp, CancellationToken ct)
    {
        // F15: módulo fiscal desativado não deve continuar mandando ZIP mensal ao contador.
        if (!sp.GetRequiredService<ITenantContext>().EnabledModules.Contains("fiscal", StringComparer.OrdinalIgnoreCase))
            return;

        var hojeBrasil = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilTime.Zone).Date;

        var db     = sp.GetRequiredService<AppDbContext>();
        var email  = sp.GetRequiredService<IEmailService>();
        var export = sp.GetRequiredService<FiscalXmlExportService>();

        var cfg = await db.FiscalConfigs.FindAsync(new object?[] { FiscalConfig.SingletonId }, ct);
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.EmailContador)) return;

        var jaEnviouEsseMes = cfg.UltimoEnvioMensalXmls.HasValue
            && cfg.UltimoEnvioMensalXmls.Value.Year  == hojeBrasil.Year
            && cfg.UltimoEnvioMensalXmls.Value.Month == hojeBrasil.Month;
        if (jaEnviouEsseMes) return;

        var (inicio, fim, mesAnterior) = CalcularJanelaMesAnterior(hojeBrasil, BrazilTime.Zone);

        var zipBytes = await export.GerarZipAsync(inicio, fim);
        var mesRef   = mesAnterior.ToString("MM/yyyy");
        var fileName = $"xmls-fiscais-{mesAnterior:yyyy-MM}.zip";

        await email.SendXmlsMensalContadorAsync(cfg.EmailContador, mesRef, zipBytes, fileName);

        cfg.UltimoEnvioMensalXmls = DateTime.UtcNow;
        cfg.UpdatedAt             = DateTime.UtcNow;
        await db.SaveChangesAsync();

        _logger.LogInformation("ZIP mensal de XMLs fiscais ({Mes}) enviado para {Email}", mesRef, cfg.EmailContador);
    }

    /// <summary>
    /// Constrói o início/fim (em UTC) do mês anterior a partir da data local de Brasília,
    /// convertendo corretamente pelo fuso em vez de marcar o valor local como se já fosse UTC
    /// (isso deslocava a janela em 3h e misclassificava notas perto da virada do mês).
    /// </summary>
    public static (DateTime InicioUtc, DateTime FimUtc, DateTime MesAnterior) CalcularJanelaMesAnterior(
        DateTime hojeBrasil, TimeZoneInfo brazilZone)
    {
        var mesAnterior = hojeBrasil.AddMonths(-1);
        var inicioLocal = new DateTime(mesAnterior.Year, mesAnterior.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var inicioUtc   = TimeZoneInfo.ConvertTimeToUtc(inicioLocal, brazilZone);
        var fimUtc      = TimeZoneInfo.ConvertTimeToUtc(inicioLocal.AddMonths(1), brazilZone);
        return (inicioUtc, fimUtc, mesAnterior);
    }
}
