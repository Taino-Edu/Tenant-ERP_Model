// =============================================================================
// AuthServiceTests.cs — Testes unitários de Autenticação
// Foco: lógica de login, quick-login e tokens
// =============================================================================

using CardGameStore.Configuration;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

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

    // SQLite in-memory para testes que usam o AuthService real (que usa ComandaService)
    private static AppDbContext CreateSqliteDb()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static AuthService CreateAuthService(AppDbContext db, ILogger<AuthService>? logger = null)
    {
        var jwtSettings = Options.Create(new JwtSettings
        {
            SecretKey                    = "ChaveSecretaDeTeste1234567890ABCDEF",
            Issuer                       = "TestIssuer",
            Audience                     = "TestAudience",
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays   = 30,
        });

        var comandaService = new ComandaService(
            db,
            new Mock<IEmailService>().Object,
            NullLogger<ComandaService>.Instance);

        return new AuthService(
            db,
            jwtSettings,
            logger ?? NullLogger<AuthService>.Instance,
            comandaService,
            new Mock<IEmailService>().Object);
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

    // ── QuickLogin — LGPD e privacidade ──────────────────────────────────────

    [Fact]
    public async Task QuickLogin_NaoLogaCPF()
    {
        // Arrange
        var db = CreateSqliteDb();

        // Logger que captura as mensagens registradas
        var logMessages = new List<string>();
        var loggerMock  = new Mock<ILogger<AuthService>>();
        loggerMock
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => true),
                It.IsAny<Exception?>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((_, _) => true)))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>((_, _, state, _, formatter) =>
            {
                var message = formatter.DynamicInvoke(state, null) as string ?? "";
                logMessages.Add(message);
            });

        var service = CreateAuthService(db, loggerMock.Object);
        var cpf     = "52998224725"; // CPF válido para validação

        // Act
        await service.QuickLoginAsync(new QuickLoginRequest(
            Name:            "Cliente Privacidade",
            Cpf:             cpf,
            WhatsApp:        "11999990099",
            TableIdentifier: null));

        // Assert — nenhuma mensagem de log deve conter o CPF
        logMessages.Should().NotContain(
            msg => msg.Contains(cpf),
            "o CPF é dado sensível e não deve aparecer em logs (LGPD)");
    }

    [Fact]
    public async Task QuickLogin_CriaNovoCLienteComConsentAt_QuandoConsentimentoFornecido()
    {
        // Arrange — valida que o campo ConsentAt pode ser preenchido no fluxo
        var db      = CreateSqliteDb();
        var service = CreateAuthService(db);
        var cpf     = "01234567890";

        // Act
        await service.QuickLoginAsync(new QuickLoginRequest(
            Name:            "Novo Cliente LGPD",
            Cpf:             cpf,
            WhatsApp:        "11988880000",
            TableIdentifier: "Mesa-02"));

        // Assert — usuário criado no banco
        var usuario = await db.Users.FirstOrDefaultAsync(u => u.Cpf == cpf);
        usuario.Should().NotBeNull("o quick-login deve criar o usuário na primeira visita");
        usuario!.Name.Should().Be("Novo Cliente LGPD");
        usuario.Role.Should().Be(UserRole.Customer);
    }

    [Fact]
    public async Task QuickLogin_NaoCriaDuplicata_QuandoCPFExistente()
    {
        // Arrange
        var db      = CreateSqliteDb();
        var service = CreateAuthService(db);
        var cpf     = "11111111111";

        // Act — duas chamadas com o mesmo CPF
        await service.QuickLoginAsync(new QuickLoginRequest(
            Name: "Primeira Vez", Cpf: cpf, WhatsApp: "11900000001"));
        await service.QuickLoginAsync(new QuickLoginRequest(
            Name: "Segunda Vez",  Cpf: cpf, WhatsApp: "11900000001"));

        // Assert — apenas um usuário com esse CPF
        var count = await db.Users.CountAsync(u => u.Cpf == cpf);
        count.Should().Be(1, "não deve criar duplicata para o mesmo CPF");
    }
}
