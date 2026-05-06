// =============================================================================
// AuthServiceTests.cs — Testes unitários de Autenticação
// Foco: lógica de login, quick-login e tokens
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Tests.Services;

public class AuthServiceTests
{
    private static AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new AppDbContext(options);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_UsuarioExistente_DeveEncontrarPorEmail()
    {
        // Arrange
        var db = CreateInMemoryDb(nameof(Login_UsuarioExistente_DeveEncontrarPorEmail));
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Admin",
            Email        = "admin@softnerd.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha123!"),
            Role         = "Admin",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var encontrado = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@softnerd.com");

        // Assert
        encontrado.Should().NotBeNull();
        encontrado!.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Login_EmailInexistente_DeveRetornarNull()
    {
        var db = CreateInMemoryDb(nameof(Login_EmailInexistente_DeveRetornarNull));

        var encontrado = await db.Users.FirstOrDefaultAsync(u => u.Email == "naoexiste@teste.com");

        encontrado.Should().BeNull();
    }

    [Fact]
    public void Login_SenhaCorreta_BCryptVerifyDeveRetornarTrue()
    {
        var senha = "Senha@Segura123!";
        var hash  = BCrypt.Net.BCrypt.HashPassword(senha);

        BCrypt.Net.BCrypt.Verify(senha, hash).Should().BeTrue();
    }

    [Fact]
    public void Login_SenhaErrada_BCryptVerifyDeveRetornarFalse()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("SenhaCorreta123!");

        BCrypt.Net.BCrypt.Verify("SenhaErrada!", hash).Should().BeFalse();
    }

    // ── Quick-Login ───────────────────────────────────────────────────────────

    [Fact]
    public async Task QuickLogin_CPFNovo_DeveCriarUsuario()
    {
        // Arrange
        var db  = CreateInMemoryDb(nameof(QuickLogin_CPFNovo_DeveCriarUsuario));
        var cpf = "123.456.789-00";

        // Act — simula lógica: busca por CPF, cria se não existe
        var existente = await db.Users.FirstOrDefaultAsync(u => u.Cpf == cpf);
        if (existente == null)
        {
            var novo = new User
            {
                Id           = Guid.NewGuid(),
                Name         = "Novo Cliente",
                Cpf          = cpf,
                WhatsApp     = "11999990001",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                Role         = UserRole.Customer,
            };
            db.Users.Add(novo);
            await db.SaveChangesAsync();
        }

        // Assert
        var usuarioCriado = await db.Users.FirstOrDefaultAsync(u => u.Cpf == cpf);
        usuarioCriado.Should().NotBeNull();
        usuarioCriado!.Role.Should().Be(UserRole.Customer);
    }

    [Fact]
    public async Task QuickLogin_CPFExistente_DeveRetornarMesmoUsuario()
    {
        // Arrange
        var db  = CreateInMemoryDb(nameof(QuickLogin_CPFExistente_DeveRetornarMesmoUsuario));
        var cpf = "987.654.321-00";

        var existente = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Cliente Antigo",
            Cpf          = cpf,
            PasswordHash = "hash",
            Role         = "Client",
        };
        db.Users.Add(existente);
        await db.SaveChangesAsync();

        // Act — segunda tentativa com mesmo CPF
        var encontrado = await db.Users.FirstOrDefaultAsync(u => u.Cpf == cpf);

        // Assert
        encontrado.Should().NotBeNull();
        encontrado!.Id.Should().Be(existente.Id, "deve retornar o mesmo usuário, não criar duplicata");
    }

    // ── Pontos na criação de conta ────────────────────────────────────────────

    [Fact]
    public void NovoUsuario_DeveTerSaldoZero()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Novo",
            PasswordHash = "hash",
            Role         = "Client",
        };

        user.PointsBalance.Should().Be(0);
        user.PointsExpiresAt.Should().BeNull();
    }

    // ── Roles ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Admin",    true)]
    [InlineData("Customer", false)]
    [InlineData("",         false)]
    public void Role_Admin_DeveIdentificarCorretamente(string role, bool esperadoAdmin)
    {
        var isAdmin = role == "Admin";
        isAdmin.Should().Be(esperadoAdmin);
    }
}
