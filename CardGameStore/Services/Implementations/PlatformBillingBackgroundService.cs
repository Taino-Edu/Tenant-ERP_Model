// =============================================================================
// PlatformBillingBackgroundService.cs — Roda a cobrança da plataforma sozinha
// (RB-01): emite o que está pendente no gateway e aplica a régua de suspensão.
//
// Diferente dos outros jobs do projeto, este NÃO itera tenants: tudo que ele
// toca vive no catálogo (schema "public"). Não há troca de schema aqui, e não
// deveria haver — é o financeiro da plataforma, não o de nenhuma loja.
// =============================================================================

using CardGameStore.Services.Interfaces;

namespace CardGameStore.Services.Implementations;

public class PlatformBillingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlatformBillingBackgroundService> _logger;

    public PlatformBillingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PlatformBillingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Mesma folga dos outros jobs: não competir com a criação de schema no
        // startup.
        await Task.Delay(TimeSpan.FromMinutes(2), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ExecutarRodadaAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na rodada de cobrança da plataforma");
            }

            // Doze horas: suspensão e reativação são decisões de dia, não de
            // minuto, mas rodar duas vezes ao dia encurta pela metade o tempo que
            // uma loja que acabou de pagar passa suspensa caso o webhook falhe.
            await Task.Delay(TimeSpan.FromHours(12), ct);
        }
    }

    private async Task ExecutarRodadaAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var billing = scope.ServiceProvider.GetRequiredService<IPlatformBillingService>();

        var emissao = await billing.EmitirCobrancasPendentesAsync(ct);

        if (emissao.Emitidas > 0)
            _logger.LogInformation("Cobrança da plataforma: {Emitidas} emitidas no gateway", emissao.Emitidas);

        foreach (var pendencia in emissao.Pendencias)
            _logger.LogWarning("Cobrança não emitida — {Pendencia}", pendencia);

        // A régua roda mesmo sem gateway configurado: suspender quem está
        // vencido não depende de emitir cobrança automática nenhuma, e é metade
        // do trabalho manual que o RB-01 existe pra matar.
        var regua = await billing.AplicarReguaDeCobrancaAsync(ct);

        foreach (var slug in regua.Suspensos)
            _logger.LogWarning("Loja {Slug} suspensa por inadimplência", slug);

        foreach (var slug in regua.Reativados)
            _logger.LogInformation("Loja {Slug} reativada após quitação", slug);
    }
}
