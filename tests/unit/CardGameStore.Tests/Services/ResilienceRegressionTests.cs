using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.Mcp;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CardGameStore.Tests.Services;

public class ResilienceRegressionTests
{
    [Fact]
    public async Task McpRevenue_CountsAllSalesWithinBrazilianDay()
    {
        using var db = TestDbFactory.Create(nameof(McpRevenue_CountsAllSalesWithinBrazilianDay));
        var (start, end) = BrazilTime.Dia();
        db.VendasAvulsas.AddRange(Enumerable.Range(0, 205).Select(_ => new VendaAvulsa {
            SoldAt = start.AddHours(1), TotalInCents = 100, SoldByAdminName = "Admin"
        }));
        db.VendasAvulsas.AddRange(new VendaAvulsa { SoldAt = start.AddTicks(-10), TotalInCents = 10000, SoldByAdminName = "Admin" },
            new VendaAvulsa { SoldAt = end, TotalInCents = 10000, SoldByAdminName = "Admin" },
            new VendaAvulsa { SoldAt = start.AddHours(1), TotalInCents = 10000, CanceladoEm = start.AddHours(2), SoldByAdminName = "Admin" });
        await db.SaveChangesAsync();
        var text = await ErpTools.ConsultarFaturamentoHoje(db, Mock.Of<IVendaAvulsaService>());
        text.Should().Contain("205 venda(s)").And.Contain("205,00");
    }

    [Fact]
    public async Task Outbox_FailedDeliveryIsRetriedAndSuccessIsNotSentAgain()
    {
        using var db = TestDbFactory.Create(nameof(Outbox_FailedDeliveryIsRetriedAndSuccessIsNotSentAgain));
        var entry = new CrediarioEmailOutbox { Id = Guid.NewGuid(), ToEmail = "customer@example.test", ToName = "Customer",
            Valor = 10, Vencimento = DateTime.UtcNow.AddDays(30) };
        db.CrediarioEmailOutbox.Add(entry);
        await db.SaveChangesAsync();
        var email = new Mock<IEmailService>();
        email.SetupSequence(e => e.SendCrediarioAbertoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), true))
            .ThrowsAsync(new IOException("SMTP unavailable")).Returns(Task.CompletedTask);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton(email.Object);
        using var sp = services.BuildServiceProvider();
        await CrediarioEmailBackgroundService.ProcessTenantAsync(sp, default);
        await db.Entry(entry).ReloadAsync();
        entry.SentAt.Should().BeNull();
        entry.Attempts.Should().Be(1);
        entry.NextAttemptAt.Should().BeAfter(DateTime.UtcNow);
        entry.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        await CrediarioEmailBackgroundService.ProcessTenantAsync(sp, default);
        await CrediarioEmailBackgroundService.ProcessTenantAsync(sp, default);
        await db.Entry(entry).ReloadAsync();
        entry.SentAt.Should().NotBeNull();
        email.Verify(e => e.SendCrediarioAbertoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), true), Times.Exactly(2));
    }

    /// <summary>
    /// Sem teto, um endereço que nunca aceita seria retentado para sempre: a
    /// linha jamais sairia da fila e a tabela só cresceria. A linha continua no
    /// banco de propósito (carta morta, para reenvio manual), mas o worker para.
    /// </summary>
    [Fact]
    public async Task Outbox_GivesUpAfterMaxAttemptsInsteadOfRetryingForever()
    {
        using var db = TestDbFactory.Create(nameof(Outbox_GivesUpAfterMaxAttemptsInsteadOfRetryingForever));
        var entry = new CrediarioEmailOutbox { Id = Guid.NewGuid(), ToEmail = "inexistente@example.test", ToName = "Customer",
            Valor = 10, Vencimento = DateTime.UtcNow.AddDays(30) };
        db.CrediarioEmailOutbox.Add(entry);
        await db.SaveChangesAsync();
        var email = new Mock<IEmailService>();
        email.Setup(e => e.SendCrediarioAbertoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), true))
            .ThrowsAsync(new IOException("caixa de entrada inexistente"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton(email.Object);
        using var sp = services.BuildServiceProvider();

        // Roda mais vezes do que o teto, sempre com a espera do backoff vencida —
        // se não houvesse desistência, cada rodada geraria mais uma tentativa.
        for (var i = 0; i < CrediarioEmailBackgroundService.MaxAttempts + 3; i++)
        {
            await CrediarioEmailBackgroundService.ProcessTenantAsync(sp, default);
            await db.Entry(entry).ReloadAsync();
            entry.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        await db.Entry(entry).ReloadAsync();
        entry.SentAt.Should().BeNull();
        entry.Attempts.Should().Be(CrediarioEmailBackgroundService.MaxAttempts);
        email.Verify(e => e.SendCrediarioAbertoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), true),
            Times.Exactly(CrediarioEmailBackgroundService.MaxAttempts));
    }

    /// <summary>
    /// A falha precisa reagendar pelo backoff, não deixar a reserva de 5 minutos
    /// valendo como se fosse o intervalo de retentativa — são coisas diferentes:
    /// a reserva protege contra dois workers, o backoff decide quando insistir.
    /// </summary>
    [Fact]
    public async Task Outbox_FailureSchedulesByBackoffNotByTheLease()
    {
        using var db = TestDbFactory.Create(nameof(Outbox_FailureSchedulesByBackoffNotByTheLease));
        var entry = new CrediarioEmailOutbox { Id = Guid.NewGuid(), ToEmail = "customer@example.test", ToName = "Customer",
            Valor = 10, Vencimento = DateTime.UtcNow.AddDays(30) };
        db.CrediarioEmailOutbox.Add(entry);
        await db.SaveChangesAsync();
        var email = new Mock<IEmailService>();
        email.Setup(e => e.SendCrediarioAbertoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), true))
            .ThrowsAsync(new IOException("SMTP reiniciando"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton(email.Object);
        using var sp = services.BuildServiceProvider();

        await CrediarioEmailBackgroundService.ProcessTenantAsync(sp, default);
        await db.Entry(entry).ReloadAsync();

        entry.Attempts.Should().Be(1);
        entry.NextAttemptAt.Should().BeAfter(DateTime.UtcNow);
        // Backoff(1) é 1 minuto; a reserva é de 5. Ficar além de 3 minutos
        // significaria que o reagendamento não aconteceu e a lease sobrou.
        entry.NextAttemptAt.Should().BeBefore(DateTime.UtcNow.AddMinutes(3));
    }
}
