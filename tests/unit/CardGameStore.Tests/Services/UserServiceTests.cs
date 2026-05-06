// =============================================================================
// UserServiceTests.cs — Testes unitários do UserService
// Executar: dotnet test  (na pasta tests/unit/CardGameStore.Tests)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Services;

public class UserServiceTests
{
    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    private static UserService CreateService(AppDbContext db) =>
        new(db, NullLogger<UserService>.Instance);

    private static User MakeCustomer(string name = "Cliente", int points = 0, DateTime? expiresAt = null) =>
        new()
        {
            Id              = Guid.NewGuid(),
            Name            = name,
            PasswordHash    = "hash",
            Role            = UserRole.Customer,
            PointsBalance   = points,
            PointsExpiresAt = expiresAt,
            IsActive        = true,
        };

    // ── Listagem ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_DeveRetornarApenasCustomers()
    {
        var db      = CreateDb(nameof(GetAll_DeveRetornarApenasCustomers));
        var service = CreateService(db);

        var admin = new User { Id = Guid.NewGuid(), Name = "Admin", PasswordHash = "h",
                               Role = UserRole.Admin, IsActive = true };
        var cliente = MakeCustomer("Ana");
        db.Users.AddRange(admin, cliente);
        await db.SaveChangesAsync();

        var result = await service.GetAllAsync();

        result.Should().ContainSingle()
              .Which.Name.Should().Be("Ana");
    }

    [Fact]
    public async Task GetAll_BuscaPorNome_DeveFiltrarCorretamente()
    {
        var db      = CreateDb(nameof(GetAll_BuscaPorNome_DeveFiltrarCorretamente));
        var service = CreateService(db);

        db.Users.AddRange(MakeCustomer("João"), MakeCustomer("Maria"), MakeCustomer("José"));
        await db.SaveChangesAsync();

        var result = await service.GetAllAsync("jo");

        result.Should().HaveCount(2);
        result.Select(u => u.Name).Should().Contain(new[] { "João", "José" });
    }

    [Fact]
    public async Task GetAll_NaoDeveRetornarUsuariosInativos()
    {
        var db      = CreateDb(nameof(GetAll_NaoDeveRetornarUsuariosInativos));
        var service = CreateService(db);

        var ativo   = MakeCustomer("Ativo");
        var inativo = MakeCustomer("Inativo");
        inativo.IsActive = false;
        db.Users.AddRange(ativo, inativo);
        await db.SaveChangesAsync();

        var result = await service.GetAllAsync();

        result.Should().ContainSingle()
              .Which.Name.Should().Be("Ativo");
    }

    // ── Perfil ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_PontosExpirados_DeveRetornarSaldoZero()
    {
        var db      = CreateDb(nameof(GetProfile_PontosExpirados_DeveRetornarSaldoZero));
        var service = CreateService(db);

        var user = MakeCustomer("Pedro", points: 100, expiresAt: DateTime.UtcNow.AddDays(-1));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var profile = await service.GetProfileAsync(user.Id);

        profile!.PointsBalance.Should().Be(0, "pontos expirados devem mostrar zero");
        profile.PointsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task GetProfile_PontosValidos_DeveRetornarSaldoReal()
    {
        var db      = CreateDb(nameof(GetProfile_PontosValidos_DeveRetornarSaldoReal));
        var service = CreateService(db);

        var user = MakeCustomer("Carla", points: 75, expiresAt: DateTime.UtcNow.AddDays(15));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var profile = await service.GetProfileAsync(user.Id);

        profile!.PointsBalance.Should().Be(75);
        profile.PointsExpired.Should().BeFalse();
    }

    // ── Adicionar pontos ──────────────────────────────────────────────────────

    [Fact]
    public async Task AddPoints_DeveSomarAoSaldoEResetarValidade()
    {
        var db      = CreateDb(nameof(AddPoints_DeveSomarAoSaldoEResetarValidade));
        var service = CreateService(db);
        var adminId = Guid.NewGuid();

        var user = MakeCustomer("Luis", points: 50, expiresAt: DateTime.UtcNow.AddDays(5));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await service.AddPointsAsync(user.Id, new AddPointsRequest { Points = 30, Reason = "Campeonato" }, adminId);

        result.PointsBalance.Should().Be(80);
        // validade deve ser ~30 dias a partir de agora
        var userAtualizado = await db.Users.FindAsync(user.Id);
        userAtualizado!.PointsExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AddPoints_PontosExpirados_DeveZerarAntesDeSomar()
    {
        var db      = CreateDb(nameof(AddPoints_PontosExpirados_DeveZerarAntesDeSomar));
        var service = CreateService(db);
        var adminId = Guid.NewGuid();

        var user = MakeCustomer("Bia", points: 100, expiresAt: DateTime.UtcNow.AddDays(-1));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await service.AddPointsAsync(user.Id, new AddPointsRequest { Points = 20 }, adminId);

        result.PointsBalance.Should().Be(20, "pontos expirados são zerados antes de somar os novos");
    }

    [Fact]
    public async Task AddPoints_UsuarioInexistente_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(AddPoints_UsuarioInexistente_DeveLancarExcecao));
        var service = CreateService(db);

        var act = async () => await service.AddPointsAsync(Guid.NewGuid(), new AddPointsRequest { Points = 10 }, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado*");
    }

    // ── Deduzir pontos ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeductPoints_DeveSubtrairDoSaldo()
    {
        var db      = CreateDb(nameof(DeductPoints_DeveSubtrairDoSaldo));
        var service = CreateService(db);

        var user = MakeCustomer("Teo", points: 80, expiresAt: DateTime.UtcNow.AddDays(10));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await service.DeductPointsAsync(user.Id, 30);

        (await db.Users.FindAsync(user.Id))!.PointsBalance.Should().Be(50);
    }

    [Fact]
    public async Task DeductPoints_SaldoInsuficiente_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(DeductPoints_SaldoInsuficiente_DeveLancarExcecao));
        var service = CreateService(db);

        var user = MakeCustomer("Rafa", points: 10, expiresAt: DateTime.UtcNow.AddDays(10));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var act = async () => await service.DeductPointsAsync(user.Id, 50);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Saldo insuficiente*");
    }

    [Fact]
    public async Task DeductPoints_PontosExpirados_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(DeductPoints_PontosExpirados_DeveLancarExcecao));
        var service = CreateService(db);

        var user = MakeCustomer("Gabi", points: 100, expiresAt: DateTime.UtcNow.AddDays(-5));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var act = async () => await service.DeductPointsAsync(user.Id, 10);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expirados*");
    }
}
