using System.Runtime.CompilerServices;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Tests.Services;

public class SefazDistributionGuardTests
{
    private static AppDbContext CreateDb([CallerMemberName] string testName = "") =>
        TestDbFactory.Create($"{nameof(SefazDistributionGuardTests)}_{testName}");

    [Fact]
    public async Task CooldownImpedeNovaChamadaSemProrrogarOPrazo()
    {
        using var db = CreateDb();
        var guard = new SefazDistributionGuard(db);
        var state = await guard.GetOrCreateAsync("12345678000199", AmbienteFiscal.Producao, 10, default);
        var now = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

        var first = await guard.TryAcquireAsync(state.Id, now, default);
        await guard.ReleaseAsync(state.Id, first.LeaseId!.Value, now, default);
        var retry = await guard.TryAcquireAsync(state.Id, now.AddMinutes(5), default);
        var persisted = await guard.ReloadAsync(state.Id, default);

        first.Status.Should().Be(SefazLeaseStatus.Acquired);
        retry.Status.Should().Be(SefazLeaseStatus.Cooldown);
        persisted.ProximaConsultaEm.Should().Be(now.AddMinutes(65),
            "tentativa recusada não pode reiniciar a janela da SEFAZ");
    }

    [Fact]
    public async Task DuasInstanciasAdquiremOMesmoEstadoUmaUnicaVez()
    {
        using var db1 = CreateDb();
        var schema = await db1.Database.SqlQueryRaw<string>(
            "SELECT current_schema() AS \"Value\"").SingleAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestDbFactory.ConnectionString)
            .AddInterceptors(new TestSchemaInterceptor(schema))
            .Options;
        using var db2 = new AppDbContext(options);

        var guard1 = new SefazDistributionGuard(db1);
        var guard2 = new SefazDistributionGuard(db2);
        var state = await guard1.GetOrCreateAsync("12345678000199", AmbienteFiscal.Producao, 0, default);
        var now = DateTime.UtcNow;

        var results = await Task.WhenAll(
            guard1.TryAcquireAsync(state.Id, now, default),
            guard2.TryAcquireAsync(state.Id, now, default));

        results.Count(result => result.Status == SefazLeaseStatus.Acquired).Should().Be(1);
        results.Count(result => result.Status == SefazLeaseStatus.InProgress).Should().Be(1);
    }

    [Fact]
    public async Task TenantEmCooldownNaoBloqueiaOutroTenantComMesmoCnpj()
    {
        using var dbA = CreateDb();
        using var dbB = CreateDb();
        var guardA = new SefazDistributionGuard(dbA);
        var guardB = new SefazDistributionGuard(dbB);
        var stateA = await guardA.GetOrCreateAsync("12345678000199", AmbienteFiscal.Producao, 0, default);
        var stateB = await guardB.GetOrCreateAsync("12345678000199", AmbienteFiscal.Producao, 0, default);
        var now = DateTime.UtcNow;

        var leaseA = await guardA.TryAcquireAsync(stateA.Id, now, default);
        await guardA.ReleaseAsync(stateA.Id, leaseA.LeaseId!.Value, now, default);
        var leaseB = await guardB.TryAcquireAsync(stateB.Id, now.AddSeconds(1), default);

        leaseB.Status.Should().Be(SefazLeaseStatus.Acquired,
            "cada tenant possui sua própria tabela no schema isolado");
    }

    [Fact]
    public async Task QuotaPontualParaEmDezoitoConsultasNaMesmaHora()
    {
        using var db = CreateDb();
        var guard = new SefazDistributionGuard(db);
        var state = await guard.GetOrCreateAsync("12345678000199", AmbienteFiscal.Producao, 0, default);
        var now = DateTime.UtcNow;

        var reservations = new List<bool>();
        for (var i = 0; i < 20; i++)
            reservations.Add((await guard.TryReservePointQueryAsync(state.Id, now.AddSeconds(i), default)).Acquired);

        reservations.Count(value => value).Should().Be(SefazDistributionGuard.PointQueryLimit);
        reservations[^1].Should().BeFalse();
    }

    [Fact]
    public async Task UltimoNsuNuncaRetrocede()
    {
        using var db = CreateDb();
        var guard = new SefazDistributionGuard(db);
        var state = await guard.GetOrCreateAsync("12345678000199", AmbienteFiscal.Producao, 900, default);

        await guard.AdvanceNsuAsync(state.Id, 850, DateTime.UtcNow, default);
        (await guard.ReloadAsync(state.Id, default)).UltimoNsu.Should().Be(900);

        await guard.AdvanceNsuAsync(state.Id, 950, DateTime.UtcNow, default);
        (await guard.ReloadAsync(state.Id, default)).UltimoNsu.Should().Be(950);
    }

    [Fact]
    public async Task Bloqueio656PersisteMargemDeSeguranca()
    {
        using var db = CreateDb();
        var guard = new SefazDistributionGuard(db);
        var state = await guard.GetOrCreateAsync("12345678000199", AmbienteFiscal.Producao, 0, default);
        var now = DateTime.UtcNow;

        await guard.BlockAsync(state.Id, now, default);
        var persisted = await guard.ReloadAsync(state.Id, default);

        persisted.BloqueadoAte.Should().BeCloseTo(now.AddMinutes(65), TimeSpan.FromMilliseconds(1));
        (await guard.TryAcquireAsync(state.Id, now.AddHours(1), default)).Status
            .Should().Be(SefazLeaseStatus.Cooldown);
        (await guard.TryAcquireAsync(state.Id, now.AddMinutes(66), default)).Status
            .Should().Be(SefazLeaseStatus.Acquired);
    }

    // ── Backoff em bloqueio repetido ─────────────────────────────────────────

    [Fact]
    public async Task Bloqueio656Repetido_AfastaAsTentativas()
    {
        // O cooldown fixo não distinguia "barrou uma vez" de "barra sempre". Com
        // ultNSU parado em 0, o job reentrava no mesmo bloqueio a cada 65 minutos
        // indefinidamente — foi o que se observou em produção.
        using var db = CreateDb();
        var guard = new SefazDistributionGuard(db);
        var state = await guard.GetOrCreateAsync("12345678000199", AmbienteFiscal.Producao, 0, default);
        var now = DateTime.UtcNow;

        (await guard.BlockAsync(state.Id, now, default)).Should().Be(1);
        (await guard.BlockAsync(state.Id, now, default)).Should().Be(2);
        var terceiro = await guard.BlockAsync(state.Id, now, default);

        terceiro.Should().Be(3);
        var persisted = await guard.ReloadAsync(state.Id, default);
        persisted.BloqueadoAte.Should().BeCloseTo(
            now.Add(SefazDistributionGuard.BackoffPara(3)), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ConsultaAceita_ZeraOBackoff()
    {
        // 137 e 138 são resposta legítima: provam que o acesso está saudável, e
        // o próximo tropeço tem que recomeçar dos 65 minutos.
        using var db = CreateDb();
        var guard = new SefazDistributionGuard(db);
        var state = await guard.GetOrCreateAsync("12345678000199", AmbienteFiscal.Producao, 0, default);
        var now = DateTime.UtcNow;

        await guard.BlockAsync(state.Id, now, default);
        await guard.BlockAsync(state.Id, now, default);
        await guard.ClearBlockStreakAsync(state.Id, now, default);

        (await guard.ReloadAsync(state.Id, default)).BloqueiosConsecutivos.Should().Be(0);
        (await guard.BlockAsync(state.Id, now, default)).Should().Be(1);
    }

    [Theory]
    [InlineData(0, 65)]
    [InlineData(1, 65)]
    [InlineData(2, 130)]
    [InlineData(3, 260)]
    [InlineData(4, 520)]
    [InlineData(20, 24 * 60)]   // teto
    [InlineData(999, 24 * 60)]  // não estoura com contador alto
    public void Backoff_DobraAteOTetoDe24h(int bloqueios, int minutosEsperados) =>
        SefazDistributionGuard.BackoffPara(bloqueios)
            .Should().Be(TimeSpan.FromMinutes(minutosEsperados));
}
