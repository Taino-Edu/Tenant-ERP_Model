using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public enum SefazLeaseStatus
{
    Acquired,
    Cooldown,
    InProgress,
}

public sealed record SefazLeaseResult(
    SefazLeaseStatus Status,
    SefazDistributionState State,
    Guid? LeaseId = null);

/// <summary>
/// Fonte de verdade persistente para cooldown, quota e exclusão distribuída do
/// NFeDistribuicaoDFe. Não usa estado estático: duas instâncias da API enxergam
/// a mesma linha no PostgreSQL do tenant.
/// </summary>
public class SefazDistributionGuard
{
    public static readonly TimeSpan SafetyCooldown = TimeSpan.FromMinutes(65);
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);
    public const int PointQueryLimit = 18;

    private readonly AppDbContext _db;

    public SefazDistributionGuard(AppDbContext db) => _db = db;

    public async Task<SefazDistributionState> GetOrCreateAsync(
        string cnpj, AmbienteFiscal ambiente, long initialNsu, CancellationToken ct)
    {
        var existing = await _db.SefazDistributionStates
            .SingleOrDefaultAsync(s => s.Cnpj == cnpj && s.Ambiente == ambiente, ct);
        if (existing is not null) return existing;

        var now = DateTime.UtcNow;
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO sefaz_distribution_state
                (id, cnpj, ambiente, ultimo_nsu, consulta_pontual_quantidade, created_at, updated_at)
            VALUES
                ({Guid.NewGuid()}, {cnpj}, {ambiente.ToString()}, {initialNsu}, 0, {now}, {now})
            ON CONFLICT (cnpj, ambiente) DO NOTHING", ct);

        return await _db.SefazDistributionStates
            .SingleAsync(s => s.Cnpj == cnpj && s.Ambiente == ambiente, ct);
    }

    public async Task<SefazLeaseResult> TryAcquireAsync(
        Guid stateId, DateTime now, CancellationToken ct)
    {
        var leaseId = Guid.NewGuid();
        var affected = await _db.SefazDistributionStates
            .Where(s => s.Id == stateId
                && (s.SyncLockAte == null || s.SyncLockAte <= now)
                && (s.ProximaConsultaEm == null || s.ProximaConsultaEm <= now)
                && (s.BloqueadoAte == null || s.BloqueadoAte <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.SyncLockId, leaseId)
                .SetProperty(s => s.SyncLockAte, now.Add(LeaseDuration))
                // Reserva antes da primeira chamada. Se o processo morrer, outra
                // instância não repete a consulta e provoca consumo indevido.
                .SetProperty(s => s.ProximaConsultaEm, now.Add(SafetyCooldown))
                .SetProperty(s => s.UpdatedAt, now), ct);

        var state = await ReloadAsync(stateId, ct);
        if (affected == 1)
            return new(SefazLeaseStatus.Acquired, state, leaseId);
        if (state.SyncLockAte > now)
            return new(SefazLeaseStatus.InProgress, state);
        return new(SefazLeaseStatus.Cooldown, state);
    }

    public Task ReleaseAsync(Guid stateId, Guid leaseId, DateTime now, CancellationToken ct) =>
        _db.SefazDistributionStates
            .Where(s => s.Id == stateId && s.SyncLockId == leaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.SyncLockId, (Guid?)null)
                .SetProperty(s => s.SyncLockAte, (DateTime?)null)
                .SetProperty(s => s.UpdatedAt, now), ct);

    public Task RenewLeaseAsync(Guid stateId, Guid leaseId, DateTime now, CancellationToken ct) =>
        _db.SefazDistributionStates
            .Where(s => s.Id == stateId && s.SyncLockId == leaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.SyncLockAte, now.Add(LeaseDuration))
                .SetProperty(s => s.UpdatedAt, now), ct);

    public async Task AdvanceNsuAsync(Guid stateId, long returnedNsu, DateTime now, CancellationToken ct)
    {
        if (returnedNsu < 0) return;
        await _db.SefazDistributionStates
            .Where(s => s.Id == stateId && s.UltimoNsu < returnedNsu)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.UltimoNsu, returnedNsu)
                .SetProperty(s => s.UpdatedAt, now), ct);
    }

    public Task BlockAsync(Guid stateId, DateTime now, CancellationToken ct)
    {
        var until = now.Add(SafetyCooldown);
        return _db.SefazDistributionStates
            .Where(s => s.Id == stateId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.BloqueadoAte, until)
                .SetProperty(s => s.ProximaConsultaEm, until)
                .SetProperty(s => s.UpdatedAt, now), ct);
    }

    public async Task<(bool Acquired, DateTime? RetryAt)> TryReservePointQueryAsync(
        Guid stateId, DateTime now, CancellationToken ct)
    {
        var windowLimit = now.AddHours(-1);
        var reset = await _db.SefazDistributionStates
            .Where(s => s.Id == stateId &&
                (s.ConsultaPontualJanelaInicio == null || s.ConsultaPontualJanelaInicio <= windowLimit))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.ConsultaPontualJanelaInicio, now)
                .SetProperty(s => s.ConsultaPontualQuantidade, 1)
                .SetProperty(s => s.UpdatedAt, now), ct);
        if (reset == 1) return (true, null);

        var incremented = await _db.SefazDistributionStates
            .Where(s => s.Id == stateId && s.ConsultaPontualJanelaInicio > windowLimit &&
                        s.ConsultaPontualQuantidade < PointQueryLimit)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.ConsultaPontualQuantidade, s => s.ConsultaPontualQuantidade + 1)
                .SetProperty(s => s.UpdatedAt, now), ct);
        if (incremented == 1) return (true, null);

        var state = await ReloadAsync(stateId, ct);
        return (false, state.ConsultaPontualJanelaInicio?.AddHours(1));
    }

    public Task<SefazDistributionState> ReloadAsync(Guid stateId, CancellationToken ct) =>
        _db.SefazDistributionStates.AsNoTracking().SingleAsync(s => s.Id == stateId, ct);
}
