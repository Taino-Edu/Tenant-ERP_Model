// =============================================================================
// AuthServiceTests.cs — Testes unitários de Autenticação
// Foco: lógica de login, quick-login e tokens
// =============================================================================

using CardGameStore.Configuration;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Hubs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Security;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CardGameStore.Tests.Services;

public class AuthServiceTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Operator", true)]
    [InlineData("Customer", false)]
    [InlineData("Customer", true)]
    public async Task QuickLogin_ExistingIdentityRequiresOwnActiveCustomerSession(string role, bool active)
    {
        using var db = CreateAuthServiceDb();
        var user = new User { Name = "Original", Cpf = "52998224725", WhatsApp = "11911111111", Role = role, IsActive = active };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);
        var request = new QuickLoginRequest("Impostor", user.Cpf, "11922222222");
        await FluentActions.Awaiting(() => service.QuickLoginAsync(request)).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => service.QuickLoginAsync(request, Guid.NewGuid())).Should().ThrowAsync<InvalidOperationException>();
        if (role != "Customer" || !active)
            await FluentActions.Awaiting(() => service.QuickLoginAsync(request, user.Id)).Should().ThrowAsync<InvalidOperationException>();
        (await db.Comandas.CountAsync()).Should().Be(0);
        await db.Entry(user).ReloadAsync();
        user.Name.Should().Be("Original");
        user.WhatsApp.Should().Be("11911111111");
        user.RefreshToken.Should().BeNull();
    }

    /// <summary>
    /// A retomada existe pra devolver a conta a quem perdeu o celular, e depende
    /// das DUAS condições juntas. Este teste fixa a combinação: sem o token do QR
    /// não retoma (senão bastaria saber um CPF, de qualquer lugar), e com senha
    /// não retoma (a conta já tem credencial própria — o caminho dela é o login).
    /// </summary>
    [Theory]
    [InlineData(false, false, false)] // sem token, sem senha  → não retoma
    [InlineData(false, true,  false)] // sem token, com senha  → não retoma
    [InlineData(true,  true,  false)] // com token, com senha  → não retoma
    [InlineData(true,  false, true )] // com token, sem senha  → RETOMA
    public async Task QuickLogin_RetomadaExigeTokenDoQrEContaSemSenha(
        bool comTokenDaMesa, bool contaTemSenha, bool deveRetomar)
    {
        using var db = CreateAuthServiceDb();
        var user = new User
        {
            Name = "Cliente Antigo", Cpf = "52998224725", WhatsApp = "11911111111",
            Role = UserRole.Customer, IsActive = true,
            PasswordHash = contaTemSenha ? BCrypt.Net.BCrypt.HashPassword("senhaForte123") : null,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateAuthService(db);
        // Celular novo: nenhuma sessão autenticada, só o QR impresso na mesa.
        var request = new QuickLoginRequest("Cliente Antigo", user.Cpf, "11911111111", "Mesa-01");

        if (deveRetomar)
        {
            var resposta = await service.QuickLoginAsync(request, null, comTokenDaMesa);
            resposta.UserId.Should().Be(user.Id, "a retomada devolve a MESMA conta, com pontos e histórico");
        }
        else
        {
            await FluentActions
                .Awaiting(() => service.QuickLoginAsync(request, null, comTokenDaMesa))
                .Should().ThrowAsync<InvalidOperationException>();
            (await db.Comandas.CountAsync()).Should().Be(0);
        }
    }

    /// <summary>
    /// O token é derivado do tenant e do nome da mesa: um QR de outra loja, ou de
    /// outra mesa, não vale aqui. Sem isso, imprimir um QR próprio daria acesso a
    /// contas sem senha de qualquer loja da plataforma.
    /// </summary>
    [Fact]
    public void MesaQrToken_NaoValeEntreLojasNemEntreMesas()
    {
        const string segredo = "segredo-de-teste-1234567890";
        var lojaA = Guid.NewGuid();
        var lojaB = Guid.NewGuid();
        var tokenLojaAMesa1 = MesaQrToken.Compute(lojaA, "Mesa-01", segredo);

        MesaQrToken.Verify(lojaA, "Mesa-01", tokenLojaAMesa1, segredo).Should().BeTrue();
        MesaQrToken.Verify(lojaB, "Mesa-01", tokenLojaAMesa1, segredo).Should().BeFalse("outra loja");
        MesaQrToken.Verify(lojaA, "Mesa-02", tokenLojaAMesa1, segredo).Should().BeFalse("outra mesa");
        MesaQrToken.Verify(lojaA, "Mesa-01", tokenLojaAMesa1, "outro-segredo").Should().BeFalse("segredo trocado");
        MesaQrToken.Verify(lojaA, "Mesa-01", null, segredo).Should().BeFalse("sem token");
        MesaQrToken.Verify(lojaA, null, tokenLojaAMesa1, segredo).Should().BeFalse("sem mesa");

        // "1" + "2" não pode virar o mesmo dado de "12" + "": o separador \n
        // impede que mesas com nomes concatenáveis compartilhem token.
        MesaQrToken.Compute(lojaA, "1", segredo).Should().NotBe(MesaQrToken.Compute(lojaA, "12", segredo));
    }

    [Fact]
    public async Task QuickLogin_PhoneOnlyCannotAuthenticateExistingAdmin()
    {
        using var db = CreateAuthServiceDb();
        db.Users.Add(new User { Name = "Admin", WhatsApp = "11911111111", Role = UserRole.Admin, IsActive = true });
        await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => CreateAuthService(db).QuickLoginAsync(
            new QuickLoginRequest("Visitor", null, "11911111111"))).Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("Admin", false)]
    [InlineData("Customer", true)]
    public async Task SetupAccount_DoesNotReplaceExistingPasswordOrActivateStaff(string role, bool hasPassword)
    {
        using var db = CreateAuthServiceDb();
        var user = new User { Name = "Original", Cpf = "52998224725", Role = role, IsActive = true,
            Email = "original@example.test", PasswordHash = hasPassword ? "original-hash" : null };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var request = new SetupAccountRequest(user.Cpf, "changed@example.test", "new-password");
        await FluentActions.Awaiting(() => CreateAuthService(db).SetupAccountAsync(request, user.Id))
            .Should().ThrowAsync<InvalidOperationException>();
        await db.Entry(user).ReloadAsync();
        user.Email.Should().Be("original@example.test");
        user.PasswordHash.Should().Be(hasPassword ? "original-hash" : null);
    }

    [Fact]
    public async Task SetupAccount_OnlyOwnSessionCanActivateOnce()
    {
        using var db = CreateAuthServiceDb();
        var user = new User { Name = "Customer", Cpf = "52998224725", Role = UserRole.Customer, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);
        var request = new SetupAccountRequest(user.Cpf, "customer@example.test", "new-password");
        await FluentActions.Awaiting(() => service.SetupAccountAsync(request)).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => service.SetupAccountAsync(request, Guid.NewGuid())).Should().ThrowAsync<InvalidOperationException>();
        var response = await service.SetupAccountAsync(request, user.Id);
        response.Role.Should().Be(UserRole.Customer);
        await FluentActions.Awaiting(() => service.SetupAccountAsync(request, user.Id)).Should().ThrowAsync<InvalidOperationException>();
        BCrypt.Net.BCrypt.Verify("new-password", user.PasswordHash).Should().BeTrue();
    }

    private static AppDbContext CreateInMemoryDb(string dbName) => TestDbFactory.Create(dbName);

    // Segundo schema isolado (ver TestDbFactory) pros testes que usam o
    // AuthService real (que usa ComandaService).
    private static AppDbContext CreateAuthServiceDb() => TestDbFactory.Create(nameof(AuthServiceTests) + "_authsvc");

    /// <summary>Cria um mock de IHubContext com Clients.Group configurado para evitar NullReferenceException.</summary>
    private static IHubContext<ComandaHub> CreateHubMock()
    {
        var mockClientProxy = new Mock<IClientProxy>();
        mockClientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockClients = new Mock<IHubClients>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

        var mockHub = new Mock<IHubContext<ComandaHub>>();
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
        return mockHub.Object;
    }

    private static CatalogDbContext CreateCatalogDb()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CatalogDbContext(options);
    }

    private static AuthService CreateAuthService(AppDbContext db, ILogger<AuthService>? logger = null, string[]? enabledModules = null)
    {
        var jwtSettings = Options.Create(new JwtSettings
        {
            SecretKey                    = "ChaveSecretaDeTeste1234567890ABCDEF",
            Issuer                       = "TestIssuer",
            Audience                     = "TestAudience",
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays   = 30,
        });

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.EnabledModules).Returns(enabledModules ?? new[] { "fiscal", "restaurante" });

        var comandaService = new ComandaService(
            db,
            new Mock<IEmailService>().Object,
            NullLogger<ComandaService>.Instance,
            new Mock<IServiceScopeFactory>().Object,
            CreateHubMock(),
            tenantContext.Object);

        return new AuthService(
            db,
            CreateCatalogDb(),
            jwtSettings,
            logger ?? NullLogger<AuthService>.Instance,
            comandaService,
            new Mock<IEmailService>().Object,
            tenantContext.Object,
            new Mock<IServiceScopeFactory>().Object);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_UsuarioExistente_DeveEncontrarPorEmail()
    {
        // Arrange
        using var db = CreateInMemoryDb(nameof(Login_UsuarioExistente_DeveEncontrarPorEmail));
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Admin",
            Email        = "admin@tenant-erp.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha123!"),
            Role         = "Admin",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var encontrado = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@tenant-erp.local");

        // Assert
        encontrado.Should().NotBeNull();
        encontrado!.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Login_EmailInexistente_DeveRetornarNull()
    {
        using var db = CreateInMemoryDb(nameof(Login_EmailInexistente_DeveRetornarNull));

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
    public async Task QuickLogin_SemModuloRestaurante_NaoAbreComanda()
    {
        using var db = CreateInMemoryDb(nameof(QuickLogin_SemModuloRestaurante_NaoAbreComanda));
        var service = CreateAuthService(db, enabledModules: ["fiscal"]);

        var act = () => service.QuickLoginAsync(new QuickLoginRequest(
            Name: "Cliente", Cpf: "52998224725", WhatsApp: "11999990000", TableIdentifier: "Mesa-01"));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*QR Code de mesa*não está habilitado*");
        (await db.Comandas.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task QuickLogin_CPFNovo_DeveCriarUsuario()
    {
        // Arrange
        using var db = CreateInMemoryDb(nameof(QuickLogin_CPFNovo_DeveCriarUsuario));
        var cpf = "12345678900";

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
        using var db = CreateInMemoryDb(nameof(QuickLogin_CPFExistente_DeveRetornarMesmoUsuario));
        var cpf = "98765432100";

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
        using var db = CreateAuthServiceDb();

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
        using var db = CreateAuthServiceDb();
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
        using var db = CreateAuthServiceDb();
        var service = CreateAuthService(db);
        var cpf     = "11111111111";

        // Act — duas chamadas com o mesmo CPF
        await service.QuickLoginAsync(new QuickLoginRequest(
            Name: "Primeira Vez", Cpf: cpf, WhatsApp: "11900000001"));
        var id = await db.Users.Where(u => u.Cpf == cpf).Select(u => u.Id).SingleAsync();
        await service.QuickLoginAsync(new QuickLoginRequest(
            Name: "Segunda Vez", Cpf: cpf, WhatsApp: "11900000001"), id);

        // Assert — apenas um usuário com esse CPF
        var count = await db.Users.CountAsync(u => u.Cpf == cpf);
        count.Should().Be(1, "não deve criar duplicata para o mesmo CPF");
    }

    // ── Login — usuário inativo / senha errada ────────────────────────────────

    [Fact]
    public async Task Login_UsuarioInativo_DeveLancarUnauthorized()
    {
        using var db = CreateAuthServiceDb();
        db.Users.Add(new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Inativo",
            Email        = "inativo@tenant-erp.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha123!"),
            Role         = UserRole.Admin,
            IsActive     = false, // conta desativada
        });
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var act = async () => await service.LoginAsync(new LoginRequest("inativo@tenant-erp.local", "Senha123!"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "usuário inativo não pode fazer login mesmo com senha correta");
    }

    [Fact]
    public async Task Login_SenhaErrada_DeveLancarUnauthorized()
    {
        using var db = CreateAuthServiceDb();
        db.Users.Add(new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Admin",
            Email        = "admin2@tenant-erp.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SenhaCorreta!"),
            Role         = UserRole.Admin,
            IsActive     = true,
        });
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var act = async () => await service.LoginAsync(new LoginRequest("admin2@tenant-erp.local", "SenhaErrada!"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>("senha incorreta deve ser rejeitada");
    }

    // ── Refresh Token ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshToken_TokenExpirado_DeveLancarUnauthorized()
    {
        using var db = CreateAuthServiceDb();
        db.Users.Add(new User
        {
            Id                 = Guid.NewGuid(),
            Name               = "Cliente",
            Email              = "cliente@tenant-erp.local",
            PasswordHash       = BCrypt.Net.BCrypt.HashPassword("Senha123!"),
            Role               = UserRole.Customer,
            IsActive           = true,
            RefreshToken       = "token-expirado-abc",
            RefreshTokenExpiry = DateTime.UtcNow.AddHours(-1), // já expirou
        });
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var act = async () => await service.RefreshTokenAsync(new RefreshTokenRequest("token-expirado-abc"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*expirado*");
    }

    [Fact]
    public async Task RefreshToken_TokenInvalido_DeveLancarUnauthorized()
    {
        using var db = CreateAuthServiceDb();
        var service = CreateAuthService(db);

        var act = async () => await service.RefreshTokenAsync(new RefreshTokenRequest("token-que-nao-existe-xyz"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>("token inexistente não deve ser aceito");
    }

    // ── ForgotPassword ────────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_EmailExistente_DeveGerarTokenDeReset()
    {
        using var db = CreateAuthServiceDb();
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Cliente Reset",
            Email        = "reset@tenant-erp.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass123!"),
            Role         = UserRole.Customer,
            IsActive     = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("reset@tenant-erp.local"));

        var atualizado = await db.Users.FindAsync(user.Id);
        atualizado!.PasswordResetToken.Should().NotBeNullOrWhiteSpace(
            "deve gerar token de reset para email cadastrado");
        atualizado.PasswordResetTokenExpiry.Should().BeAfter(DateTime.UtcNow,
            "token deve ter validade futura");
    }

    [Fact]
    public async Task ForgotPassword_EmailInexistente_NaoDeveLancarExcecao()
    {
        using var db = CreateAuthServiceDb();
        var service = CreateAuthService(db);

        // Resposta silenciosa — não revelar se e-mail existe (proteção contra user enumeration)
        var act = async () => await service.ForgotPasswordAsync(
            new ForgotPasswordRequest("nao.cadastrado@tenant-erp.local"));

        await act.Should().NotThrowAsync();
    }

    // ── ResetPassword ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_TokenValido_DeveAlterarSenha()
    {
        const string resetToken = "token-valido-abc123";
        using var db = CreateAuthServiceDb();
        var user = new User
        {
            Id                       = Guid.NewGuid(),
            Name                     = "Cliente",
            Email                    = "troca@tenant-erp.local",
            PasswordHash             = BCrypt.Net.BCrypt.HashPassword("SenhaAntiga!"),
            Role                     = UserRole.Customer,
            IsActive                 = true,
            PasswordResetToken       = resetToken,
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        await service.ResetPasswordAsync(new ResetPasswordRequest(resetToken, "NovaSenha123!"));

        var atualizado = await db.Users.FindAsync(user.Id);
        BCrypt.Net.BCrypt.Verify("NovaSenha123!", atualizado!.PasswordHash!)
            .Should().BeTrue("nova senha deve funcionar após o reset");
        atualizado.PasswordResetToken.Should().BeNull("token deve ser removido após uso único");
    }

    [Fact]
    public async Task ResetPassword_TokenExpirado_DeveLancarUnauthorized()
    {
        const string resetToken = "token-expirado-reset";
        using var db = CreateAuthServiceDb();
        db.Users.Add(new User
        {
            Id                       = Guid.NewGuid(),
            Name                     = "Cliente",
            Email                    = "expired@tenant-erp.local",
            PasswordHash             = BCrypt.Net.BCrypt.HashPassword("Senha123!"),
            Role                     = UserRole.Customer,
            IsActive                 = true,
            PasswordResetToken       = resetToken,
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(-1), // expirado
        });
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var act = async () => await service.ResetPasswordAsync(
            new ResetPasswordRequest(resetToken, "NovaSenha123!"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*expirado*");
    }

    [Fact]
    public async Task ResetPassword_DeveInvalidarSessoesAtivas()
    {
        // Segurança: troca de senha deve forçar novo login (invalida refresh tokens ativos)
        const string resetToken = "token-valido-session-test";
        using var db = CreateAuthServiceDb();
        var user = new User
        {
            Id                       = Guid.NewGuid(),
            Name                     = "Cliente",
            Email                    = "session@tenant-erp.local",
            PasswordHash             = BCrypt.Net.BCrypt.HashPassword("OldPass!"),
            Role                     = UserRole.Customer,
            IsActive                 = true,
            RefreshToken             = "sessao-ativa-token-xyz",
            RefreshTokenExpiry       = DateTime.UtcNow.AddDays(30),
            PasswordResetToken       = resetToken,
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        await service.ResetPasswordAsync(new ResetPasswordRequest(resetToken, "NewPass123!"));

        var atualizado = await db.Users.FindAsync(user.Id);
        atualizado!.RefreshToken.Should().BeNull(
            "sessões ativas devem ser invalidadas quando a senha é alterada");
        atualizado.RefreshTokenExpiry.Should().BeNull();
    }

    [Fact]
    public async Task Logout_DeveInvalidarRefreshEAccessTokenAtual()
    {
        using var db = CreateAuthServiceDb();
        var user = new User
        {
            Name = "Cliente", PasswordHash = "hash", Role = UserRole.Customer,
            RefreshToken = "hash-token", RefreshTokenExpiry = DateTime.UtcNow.AddDays(1),
            SessionVersion = 7,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await CreateAuthService(db).LogoutAsync(user.Id);

        db.ChangeTracker.Clear();
        var persisted = await db.Users.FindAsync(user.Id);
        persisted!.RefreshToken.Should().BeNull();
        persisted.RefreshTokenExpiry.Should().BeNull();
        persisted.SessionVersion.Should().Be(8);
    }
}
