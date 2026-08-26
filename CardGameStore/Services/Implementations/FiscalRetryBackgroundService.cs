// =============================================================================
// FiscalRetryBackgroundService.cs — Contingência: reprocessa periodicamente
// notas PendenteEmissao (ex: SEFAZ estava fora do ar na tentativa original),
// resultado incerto (consulta a chave) e contingência (retransmite), e ao fim
// de cada ciclo sincroniza o painel de pendências fiscais (CON-002).
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class FiscalRetryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FiscalRetryBackgroundService> _logger;

    public FiscalRetryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<FiscalRetryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _scopeFactory.ForEachActiveTenantAsync(
                    _logger, ReprocessarPendentesAsync, ct,
                    requiredModule: "fiscal", includeExternal: true);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no reprocessamento automático de notas fiscais pendentes");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), ct);
        }
    }

    private async Task ReprocessarPendentesAsync(IServiceProvider sp, CancellationToken ct)
    {
        // F15: módulo fiscal desativado (mas com notas residuais no schema) não deve
        // continuar retransmitindo pra SEFAZ.
        if (!sp.GetRequiredService<ITenantContext>().EnabledModules.Contains("fiscal", StringComparer.OrdinalIgnoreCase))
            return;

        var db      = sp.GetRequiredService<AppDbContext>();
        var emissao = sp.GetRequiredService<INfceEmissionService>();
        var alertas = sp.GetRequiredService<IAlertaFiscalService>();

        // ResultadoIncerto entra aqui porque é o estado que MAIS precisa de novo
        // contato com a SEFAZ: cada ciclo é uma nova chance de consultar a chave e
        // descobrir se aquele documento foi autorizado (RES-001).
        var pendentesIds = await db.NotasFiscaisEmitidas
            .Where(n => n.Status == NotaFiscalStatus.PendenteEmissao ||
                        n.Status == NotaFiscalStatus.AutorizadaContingencia ||
                        n.Status == NotaFiscalStatus.ResultadoIncerto)
            .OrderBy(n => n.CreatedAt)
            .Take(50) // não tenta reprocessar milhares de uma vez
            .Select(n => n.Id)
            .ToListAsync(ct);

        var estornosPendentesIds = await db.NotasFiscaisEmitidas
            .Where(n => n.Status == NotaFiscalStatus.Cancelada && n.ErpEstornadoEm == null)
            .OrderBy(n => n.CanceladoEm)
            .Take(50)
            .Select(n => n.Id)
            .ToListAsync(ct);

        int autorizadas = 0;
        foreach (var id in pendentesIds)
        {
            var nota = await emissao.ReprocessarAsync(id);
            if (nota.Status == NotaFiscalStatus.Autorizada) autorizadas++;
        }

        foreach (var id in estornosPendentesIds)
            await emissao.ReprocessarEstornoErpAsync(id);

        // CON-002: sincroniza DEPOIS das tentativas, para o painel refletir o
        // estado pós-retry — sem isso, uma nota autorizada neste ciclo ainda
        // apareceria como pendência por mais 15 minutos. Roda mesmo quando não
        // houve nada a reprocessar: venda sem documento, lacuna de numeração e
        // exportação atrasada não têm nota pendente para disparar a varredura.
        await SincronizarAlertasAsync(alertas, ct);

        if (pendentesIds.Count == 0 && estornosPendentesIds.Count == 0) return;

        _logger.LogInformation(
            "Reprocessamento automático: {Total} pendente(s) verificada(s), {Autorizadas} autorizada(s) agora.",
            pendentesIds.Count, autorizadas);
    }

    /// <summary>
    /// Falha ao montar o painel de alertas não pode derrubar o reprocessamento —
    /// transmitir documento fiscal é mais importante do que listar pendência.
    /// </summary>
    private async Task SincronizarAlertasAsync(IAlertaFiscalService alertas, CancellationToken ct)
    {
        try
        {
            await alertas.SincronizarAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao sincronizar os alertas fiscais deste tenant.");
        }
    }
}
