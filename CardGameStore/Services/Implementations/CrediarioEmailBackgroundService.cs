using CardGameStore.Data;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

/// <summary>Entrega ao menos uma vez. SMTP não permite garantir ausência de duplicata após ACK perdido.</summary>
public sealed class CrediarioEmailBackgroundService(IServiceScopeFactory scopes,
    ILogger<CrediarioEmailBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Instalações legadas podem operar no tenant-zero fora do catálogo.
                using (var zero = scopes.CreateScope())
                {
                    zero.ServiceProvider.GetRequiredService<ITenantContext>().Set(
                        TenantConstants.TenantZeroId, TenantConstants.TenantZeroSchema, Array.Empty<string>());
                    try { await ProcessTenantAsync(zero.ServiceProvider, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) { logger.LogWarning(ex, "Fila de email do tenant-zero indisponível."); }
                }
                await scopes.ForEachActiveTenantAsync(logger, ProcessTenantAsync, stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar fila de emails de crediário.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Teto de tentativas. Sem ele, um endereço inválido é retentado a cada
    /// intervalo para sempre: o worker nunca desiste, a linha nunca sai da fila
    /// e a tabela só cresce. Passando daqui a linha vira carta morta —
    /// continua no banco (com SentAt nulo e Attempts no teto) para inspeção e
    /// reenvio manual, mas o worker para de tocá-la.
    /// </summary>
    internal const int MaxAttempts = 8;

    /// <summary>
    /// Espera antes da próxima tentativa, pelo número de tentativas já feitas.
    /// Escalonada, e não fixa, porque as duas causas de falha pedem coisas
    /// opostas: hipo de rede ou SMTP reiniciando resolve em minutos, e insistir
    /// rápido é o certo; caixa cheia ou domínio fora do ar dura horas, e
    /// insistir rápido só queima reputação do remetente. Mesma escada do
    /// backoff da prospecção. Somando tudo, as 8 tentativas cobrem ~9h.
    /// </summary>
    internal static TimeSpan Backoff(int attempts) => attempts switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        2    => TimeSpan.FromMinutes(5),
        3    => TimeSpan.FromMinutes(15),
        4    => TimeSpan.FromMinutes(45),
        _    => TimeSpan.FromHours(2),
    };

    internal static async Task ProcessTenantAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var logger = sp.GetRequiredService<ILogger<CrediarioEmailBackgroundService>>();
        var now = DateTime.UtcNow;
        var pending = await db.CrediarioEmailOutbox.AsNoTracking()
            .Where(e => e.SentAt == null && e.Attempts < MaxAttempts && e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt).Take(20).ToListAsync(ct);
        foreach (var item in pending)
        {
            ct.ThrowIfCancellationRequested();
            // Reserva persistente: dois workers não iniciam a mesma entrega juntos.
            var lease = DateTime.UtcNow.AddMinutes(5);
            var claimed = await db.CrediarioEmailOutbox
                .Where(e => e.Id == item.Id && e.SentAt == null && e.NextAttemptAt <= now)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.NextAttemptAt, lease)
                    .SetProperty(e => e.Attempts, e => e.Attempts + 1), ct);
            if (claimed != 1) continue;
            var attempt = item.Attempts + 1;
            try
            {
                await sp.GetRequiredService<IEmailService>().SendCrediarioAbertoAsync(
                    item.ToEmail, item.ToName, item.Valor, item.Vencimento, requireDelivery: true);
                await db.CrediarioEmailOutbox.Where(e => e.Id == item.Id && e.NextAttemptAt == lease)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.SentAt, DateTime.UtcNow), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                if (attempt >= MaxAttempts)
                {
                    // Erro, não aviso: daqui ninguém mais tenta, e um crediário
                    // aberto sem o cliente saber é problema de cobrança depois.
                    logger.LogError(ex,
                        "Entrega de crediário {EventId} abandonada após {Attempt} tentativas — " +
                        "a linha continua em crediario_email_outbox com SentAt nulo para reenvio manual.",
                        item.Id, attempt);
                    continue;
                }

                // Só reagenda se a reserva ainda for nossa: se a lease expirou e
                // outro worker assumiu, o NextAttemptAt dele não pode ser
                // sobrescrito por este.
                var proximaTentativa = DateTime.UtcNow.Add(Backoff(attempt));
                await db.CrediarioEmailOutbox.Where(e => e.Id == item.Id && e.NextAttemptAt == lease)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.NextAttemptAt, proximaTentativa), ct);
                logger.LogWarning(ex,
                    "Entrega de crediário {EventId} pendente; tentativa {Attempt} de {Max}, próxima em {Delay}.",
                    item.Id, attempt, MaxAttempts, Backoff(attempt));
            }
        }
    }
}
