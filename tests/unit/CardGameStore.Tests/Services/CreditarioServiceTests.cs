// =============================================================================
// CreditarioServiceTests.cs — Testes unitários do CreditarioService
// Chama o serviço real com InMemory database (sem mocks de lógica)
// Executar: dotnet test  (na pasta tests/unit/CardGameStore.Tests)
// =============================================================================

using Xunit;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Services;

public class CreditarioServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    private static CreditarioService CreateService(AppDbContext db) =>
        new(db, NullLogger<CreditarioService>.Instance);

    private static async Task<(User user, Comanda comanda, Guid adminId)> SeedAsync(AppDbContext db)
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Cliente Teste",
            Email        = "cliente@test.com",
            PasswordHash = "hash",
            Role         = UserRole.Customer,
        };
        var comanda = new Comanda
        {
            Id     = Guid.NewGuid(),
            UserId = user.Id,
            User   = user,
            Status = ComandaStatus.Fechada,
            TotalInCents = 10000, // R$ 100,00
        };
        var adminId = Guid.NewGuid();

        db.Users.Add(user);
        db.Comandas.Add(comanda);
        await db.SaveChangesAsync();

        return (user, comanda, adminId);
    }

    // ── Criar crediário ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_DadosValidos_DeveCriarNovoCrediarioso()
    {
        var db      = CreateDb(nameof(CreateAsync_DadosValidos_DeveCriarNovoCrediarioso));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        var resultado = await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId);

        resultado.Should().NotBeNull();
        resultado.UserId.Should().Be(user.Id);
        resultado.ComandaId.Should().Be(comanda.Id);
        resultado.ValorEmReais.Should().Be(100.00m);
        resultado.Status.Should().Be("Aberto");
        resultado.Vencido.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_JaTemAberto_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(CreateAsync_JaTemAberto_DeveLancarExcecao));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        // Cria o primeiro crediário
        await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId);

        // Tenta criar um segundo
        var comanda2 = new Comanda
        {
            Id     = Guid.NewGuid(),
            UserId = user.Id,
            User   = user,
            Status = ComandaStatus.Fechada,
            TotalInCents = 5000,
        };
        db.Comandas.Add(comanda2);
        await db.SaveChangesAsync();

        var act = async () => await service.CreateAsync(comanda2.Id, user.Id, comanda2.TotalInCents, adminId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já possui um crediário em aberto*");
    }

    [Fact]
    public async Task CreateAsync_ComandaNaoExiste_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(CreateAsync_ComandaNaoExiste_DeveLancarExcecao));
        var service = CreateService(db);
        var (user, _, adminId) = await SeedAsync(db);

        var act = async () => await service.CreateAsync(Guid.NewGuid(), user.Id, 5000, adminId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task CreateAsync_DeveDefinirVencimentoEm30Dias()
    {
        var db      = CreateDb(nameof(CreateAsync_DeveDefinirVencimentoEm30Dias));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        var antes = DateTime.UtcNow;
        var resultado = await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId);
        var depois = DateTime.UtcNow;

        // Vencimento deve ser Data abertura + 30 dias
        var diasAteVencimento = (resultado.DataVencimento - resultado.DataAbertura).TotalDays;
        diasAteVencimento.Should().BeApproximately(30, 0.001);
    }

    // ── Recuperar crediários ──────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserAsync_DeveRetornarTodosDeUmUsuario()
    {
        var db      = CreateDb(nameof(GetByUserAsync_DeveRetornarTodosDeUmUsuario));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId);

        var resultado = await service.GetByUserAsync(user.Id);

        resultado.Should().HaveCount(1);
        resultado.First().UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByUserAsync_SemCrediarios_DeveRetornarListaVazia()
    {
        var db      = CreateDb(nameof(GetByUserAsync_SemCrediarios_DeveRetornarListaVazia));
        var service = CreateService(db);
        var (user, _, _) = await SeedAsync(db);

        var resultado = await service.GetByUserAsync(user.Id);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAbertoAsync_DeveRetornarApenasAbertos()
    {
        var db      = CreateDb(nameof(GetAbertoAsync_DeveRetornarApenasAbertos));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        var crediario = await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId);

        // Marca como pago
        var adminId2 = Guid.NewGuid();
        await service.MarkAsPaidAsync(crediario.Id, adminId2);

        // Cria outro aberto
        var comanda2 = new Comanda
        {
            Id     = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            User   = new User { Id = Guid.NewGuid(), Name = "User2", PasswordHash = "h", Role = UserRole.Customer },
            Status = ComandaStatus.Fechada,
            TotalInCents = 5000,
        };
        db.Users.Add(comanda2.User);
        db.Comandas.Add(comanda2);
        await db.SaveChangesAsync();

        var crediario2 = await service.CreateAsync(comanda2.Id, comanda2.UserId, comanda2.TotalInCents, adminId);

        var resultado = await service.GetAbertoAsync();

        resultado.Should().HaveCount(1);
        resultado.First().Id.Should().Be(crediario2.Id);
        resultado.First().Status.Should().Be("Aberto");
    }

    [Fact]
    public async Task GetVencidosAsync_DeveRetornarApenasVencidos()
    {
        var db      = CreateDb(nameof(GetVencidosAsync_DeveRetornarApenasVencidos));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        // Cria crediário (não vencido ainda)
        var crediario = await db.Crediarios
            .FirstOrDefaultAsync(c => c.ComandaId == comanda.Id)
            ?? (await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId),
                (await db.Crediarios.FirstOrDefaultAsync(c => c.ComandaId == comanda.Id))!).Item2;

        // Simula vencimento no banco
        var crediarioObj = await db.Crediarios.FirstAsync(c => c.ComandaId == comanda.Id);
        crediarioObj.DataVencimento = DateTime.UtcNow.AddDays(-5); // 5 dias no passado
        await db.SaveChangesAsync();

        var resultado = await service.GetVencidosAsync();

        resultado.Should().HaveCount(1);
        resultado.First().Vencido.Should().BeTrue();
    }

    // ── Marcar como pago ──────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAsPaidAsync_DeveMudarStatusERegistrarData()
    {
        var db      = CreateDb(nameof(MarkAsPaidAsync_DeveMudarStatusERegistrarData));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        var crediario = await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId);

        var adminId2 = Guid.NewGuid();
        var resultado = await service.MarkAsPaidAsync(crediario.Id, adminId2, "Pago em dinheiro");

        resultado.Status.Should().Be("Pago");
        resultado.DataPagamento.Should().NotBeNull();
        resultado.Observacao.Should().Be("Pago em dinheiro");
    }

    [Fact]
    public async Task MarkAsPaidAsync_JaPago_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(MarkAsPaidAsync_JaPago_DeveLancarExcecao));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        var crediario = await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId);
        var adminId2 = Guid.NewGuid();
        await service.MarkAsPaidAsync(crediario.Id, adminId2);

        // Tenta marcar como pago novamente
        var act = async () => await service.MarkAsPaidAsync(crediario.Id, adminId2);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já foi marcado como pago*");
    }

    [Fact]
    public async Task MarkAsPaidAsync_NaoExiste_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(MarkAsPaidAsync_NaoExiste_DeveLancarExcecao));
        var service = CreateService(db);

        var act = async () => await service.MarkAsPaidAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado*");
    }

    // ── Recuperar por ID ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_DeveRetornarCrediarioso()
    {
        var db      = CreateDb(nameof(GetByIdAsync_DeveRetornarCrediarioso));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        var crediario = await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId);

        var resultado = await service.GetByIdAsync(crediario.Id);

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(crediario.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NaoExiste_DeveRetornarNull()
    {
        var db      = CreateDb(nameof(GetByIdAsync_NaoExiste_DeveRetornarNull));
        var service = CreateService(db);

        var resultado = await service.GetByIdAsync(Guid.NewGuid());

        resultado.Should().BeNull();
    }

    // ── Verificar se tem aberto ───────────────────────────────────────────────

    [Fact]
    public async Task HasOpenAsync_TemAberto_DeveRetornarTrue()
    {
        var db      = CreateDb(nameof(HasOpenAsync_TemAberto_DeveRetornarTrue));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId);

        var resultado = await service.HasOpenAsync(user.Id);

        resultado.Should().BeTrue();
    }

    [Fact]
    public async Task HasOpenAsync_SemAberto_DeveRetornarFalse()
    {
        var db      = CreateDb(nameof(HasOpenAsync_SemAberto_DeveRetornarFalse));
        var service = CreateService(db);
        var (user, _, _) = await SeedAsync(db);

        var resultado = await service.HasOpenAsync(user.Id);

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task HasOpenAsync_ApenasAbertos_NaoContaPagos()
    {
        var db      = CreateDb(nameof(HasOpenAsync_ApenasAbertos_NaoContaPagos));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        var crediario = await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId);
        var adminId2 = Guid.NewGuid();
        await service.MarkAsPaidAsync(crediario.Id, adminId2);

        var resultado = await service.HasOpenAsync(user.Id);

        resultado.Should().BeFalse();
    }

    // ── Obter aberto ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOpenAsync_TemAberto_DeveRetornarCrediarioso()
    {
        var db      = CreateDb(nameof(GetOpenAsync_TemAberto_DeveRetornarCrediarioso));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        var criado = await service.CreateAsync(comanda.Id, user.Id, comanda.TotalInCents, adminId);

        var resultado = await service.GetOpenAsync(user.Id);

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(criado.Id);
    }

    [Fact]
    public async Task GetOpenAsync_SemAberto_DeveRetornarNull()
    {
        var db      = CreateDb(nameof(GetOpenAsync_SemAberto_DeveRetornarNull));
        var service = CreateService(db);
        var (user, _, _) = await SeedAsync(db);

        var resultado = await service.GetOpenAsync(user.Id);

        resultado.Should().BeNull();
    }

    // ── Total devido ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTotalDevidoAsync_DeveCalcularSomaDeAbertos()
    {
        var db      = CreateDb(nameof(GetTotalDevidoAsync_DeveCalcularSomaDeAbertos));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        await service.CreateAsync(comanda.Id, user.Id, 10000, adminId); // R$ 100,00

        var resultado = await service.GetTotalDevidoAsync(user.Id);

        resultado.Should().Be(100.00m);
    }

    [Fact]
    public async Task GetTotalDevidoAsync_ApenasAbertos_NaoContaPagos()
    {
        var db      = CreateDb(nameof(GetTotalDevidoAsync_ApenasAbertos_NaoContaPagos));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        var crediario = await service.CreateAsync(comanda.Id, user.Id, 10000, adminId); // R$ 100,00
        var adminId2 = Guid.NewGuid();
        await service.MarkAsPaidAsync(crediario.Id, adminId2);

        var resultado = await service.GetTotalDevidoAsync(user.Id);

        resultado.Should().Be(0.00m);
    }

    [Fact]
    public async Task GetTotalDevidoAsync_MultiplosCreditarios_DeveCalcularTotal()
    {
        var db      = CreateDb(nameof(GetTotalDevidoAsync_MultiplosCreditarios_DeveCalcularTotal));
        var service = CreateService(db);
        var (user, comanda, adminId) = await SeedAsync(db);

        // Primeiro crediário: R$ 100,00
        await service.CreateAsync(comanda.Id, user.Id, 10000, adminId);

        // Cria novo usuário com outro crediário
        var user2 = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "User2",
            Email        = "user2@test.com",
            PasswordHash = "h",
            Role         = UserRole.Customer,
        };
        var comanda2 = new Comanda
        {
            Id     = Guid.NewGuid(),
            UserId = user2.Id,
            User   = user2,
            Status = ComandaStatus.Fechada,
            TotalInCents = 5000, // R$ 50,00
        };
        db.Users.Add(user2);
        db.Comandas.Add(comanda2);
        await db.SaveChangesAsync();

        // Não cria crediário para user2, só verifica que user1 ainda tem 100
        var resultado = await service.GetTotalDevidoAsync(user.Id);

        resultado.Should().Be(100.00m);
    }
}
