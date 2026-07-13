// =============================================================================
// FechamentoBackgroundService.cs — Fecha formalmente dia/semana/mês de cada
// tenant ativo (snapshot congelado em FechamentoPeriodo).
//
// PRIMEIRO background service deste projeto que precisa iterar por todos os
// tenants: os outros (FiscalAlertBackgroundService etc.) só abrem um scope e
// operam implicitamente no tenant-zero/schema "public", porque
// TenantResolutionMiddleware (que resolve o tenant certo) só roda no
// pipeline HTTP, nunca pra um hosted service. Aqui, pra cada tenant Active
// lido do catálogo, abrimos um scope NOVO e chamamos ITenantContext.Set(...)
// manualmente antes de resolver o AppDbContext daquele scope — mesmo padrão
// já usado em ContadorPortalController/TenantProvisioningService, só que
// alternando o tenant "de request em request" vira "de tenant em tenant"
// dentro do mesmo tick do job.
//
// Idempotência mora em FinanceiroCalculoService.FecharJanelaAsync (upsert por
// Tipo/DataInicio/DataFim, com índice único como rede de segurança de
// verdade) — aqui só decidimos QUANDO fechar, checando se a janela já foi
// fechada antes de chamar, pra não gerar ruído de log toda hora.
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class FechamentoBackgroundService : BackgroundService
{
    // Fuso horário de Brasília — funciona em Linux (IANA) e Windows (ID legado).
    private static readonly TimeZoneInfo BrazilZone = GetBrazilZone();
    private static TimeZoneInfo GetBrazilZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FechamentoBackgroundService> _logger;

    public FechamentoBackgroundService(IServiceScopeFactory scopeFactory, ILogger<FechamentoBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // aguarda 2 min após startup para não competir com EnsureCreated
        await Task.Delay(TimeSpan.FromMinutes(2), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await FecharPendentesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no ciclo de fechamento financeiro");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), ct);
        }
    }

    private async Task FecharPendentesAsync()
    {
        // Kind=Utc carimbado na marra: ConvertTimeFromUtc devolve Kind=Unspecified,
        // mas todo DateTime derivado daqui (ontem/segundaAnterior/etc.) vira
        // parâmetro de query contra FechamentoPeriodo.DataInicio/DataFim, que é
        // timestamptz — Npgsql rejeita Kind=Unspecified nessa coluna. Sem isso,
        // TODO ciclo deste job (a cada 30 min, pra cada tenant) lançava
        // ArgumentException logo no primeiro AnyAsync check, era engolido pelo
        // catch em FecharPendentesAsync/FecharTenantAsync e nunca fechava
        // nenhum período automaticamente — só ficava logando erro em silêncio.
        var agoraBr = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilZone), DateTimeKind.Utc);

        using var catalogScope = _scopeFactory.CreateScope();
        var catalog = catalogScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var tenants = await catalog.Tenants
            .Where(t => t.Status == TenantStatus.Active)
            .Select(t => new { t.Id, t.Slug, t.SchemaName, t.EnabledModules })
            .ToListAsync();

        foreach (var tenant in tenants)
        {
            try
            {
                await FecharTenantAsync(tenant.Id, tenant.SchemaName, tenant.EnabledModules, agoraBr);
            }
            catch (Exception ex)
            {
                // Um tenant com schema quebrado/migração pendente não pode
                // travar o fechamento dos outros — loga e segue o loop.
                _logger.LogError(ex, "Falha ao fechar período financeiro do tenant {TenantId} ({Slug})", tenant.Id, tenant.Slug);
            }
        }
    }

    private async Task FecharTenantAsync(Guid tenantId, string schemaName, string[] enabledModules, DateTime agoraBr)
    {
        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(tenantId, schemaName, enabledModules);

        var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var calc = scope.ServiceProvider.GetRequiredService<IFinanceiroCalculoService>();

        // ── Dia: fecha ontem, só depois das 00:10 BR (dá uma folga pro fechamento
        // de comandas/vendas do fim do dia anterior terminarem de gravar) ────────
        if (agoraBr.TimeOfDay >= TimeSpan.FromMinutes(10))
        {
            var ontem = agoraBr.Date.AddDays(-1);
            var jaFechado = await db.FechamentosPeriodo.AnyAsync(f =>
                f.Tipo == TipoFechamento.Dia && f.DataInicio == ontem && f.DataFim == ontem);
            if (!jaFechado)
            {
                await calc.FecharJanelaAsync(TipoFechamento.Dia, ontem, ontem);
                _logger.LogInformation("Fechamento Dia gravado — tenant {TenantId}, {Data:yyyy-MM-dd}", tenantId, ontem);
            }
        }

        // ── Semana: fecha a semana anterior (segunda a domingo), só nas segundas ─
        if (agoraBr.DayOfWeek == DayOfWeek.Monday)
        {
            var domingoAnterior  = agoraBr.Date.AddDays(-1);
            var segundaAnterior  = domingoAnterior.AddDays(-6);
            var jaFechado = await db.FechamentosPeriodo.AnyAsync(f =>
                f.Tipo == TipoFechamento.Semana && f.DataInicio == segundaAnterior && f.DataFim == domingoAnterior);
            if (!jaFechado)
            {
                await calc.FecharJanelaAsync(TipoFechamento.Semana, segundaAnterior, domingoAnterior);
                _logger.LogInformation("Fechamento Semana gravado — tenant {TenantId}, {Ini:yyyy-MM-dd} a {Fim:yyyy-MM-dd}",
                    tenantId, segundaAnterior, domingoAnterior);
            }
        }

        // ── Mes: fecha o mês anterior por completo, só no dia 1 ──────────────────
        if (agoraBr.Day == 1)
        {
            var ultimoDiaMesAnterior   = agoraBr.Date.AddDays(-1);
            var primeiroDiaMesAnterior = new DateTime(ultimoDiaMesAnterior.Year, ultimoDiaMesAnterior.Month, 1);
            var jaFechado = await db.FechamentosPeriodo.AnyAsync(f =>
                f.Tipo == TipoFechamento.Mes && f.DataInicio == primeiroDiaMesAnterior && f.DataFim == ultimoDiaMesAnterior);
            if (!jaFechado)
            {
                await calc.FecharJanelaAsync(TipoFechamento.Mes, primeiroDiaMesAnterior, ultimoDiaMesAnterior);
                _logger.LogInformation("Fechamento Mes gravado — tenant {TenantId}, {Ini:yyyy-MM-dd} a {Fim:yyyy-MM-dd}",
                    tenantId, primeiroDiaMesAnterior, ultimoDiaMesAnterior);
            }
        }
    }
}
