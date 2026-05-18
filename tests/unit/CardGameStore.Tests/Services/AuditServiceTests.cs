// =============================================================================
// AuditServiceTests.cs — Testes unitários do AuditService
// Cobre: hash de IP, funcionamento sem HttpContext, extração de claim userId
// Executar: dotnet test  (na pasta tests/unit/CardGameStore.Tests)
// =============================================================================

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CardGameStore.Tests.Services;

public class AuditServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Salt padrão usado nos testes — deve bater com o IConfiguration mockado.</summary>
    private const string TestSalt = "test-salt";

    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Cria AuditService com IConfiguration mockado contendo o salt de teste.
    /// </summary>
    private static AuditService CreateService(
        AppDbContext         db,
        IHttpContextAccessor? accessor = null,
        string               salt     = TestSalt)
    {
        accessor ??= new Mock<IHttpContextAccessor>().Object;

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Security:IpHashSalt"]).Returns(salt);

        return new AuditService(db, accessor, NullLogger<AuditService>.Instance, configMock.Object);
    }

    /// <summary>Reproduz o algoritmo de hash do AuditService (SHA-256 com salt).</summary>
    private static string ComputeHash(string ip, string salt = TestSalt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + ip));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Constrói um IHttpContextAccessor mockado com o IP remoto fornecido.
    /// </summary>
    private static IHttpContextAccessor MockAccessorWithIp(string ip)
    {
        var connectionMock = new Mock<ConnectionInfo>();
        connectionMock.SetupGet(c => c.RemoteIpAddress)
                      .Returns(System.Net.IPAddress.Parse(ip));

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.SetupGet(ctx => ctx.Connection)
                       .Returns(connectionMock.Object);
        httpContextMock.SetupGet(ctx => ctx.User)
                       .Returns(new ClaimsPrincipal());
        // Headers sem X-Forwarded-For
        httpContextMock.SetupGet(ctx => ctx.Request)
                       .Returns(MockRequestWithNoForwardedFor());

        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.SetupGet(a => a.HttpContext)
                    .Returns(httpContextMock.Object);

        return accessorMock.Object;
    }

    private static HttpRequest MockRequestWithNoForwardedFor()
    {
        var requestMock = new Mock<HttpRequest>();
        var headers     = new HeaderDictionary();
        requestMock.SetupGet(r => r.Headers).Returns(headers);
        return requestMock.Object;
    }

    /// <summary>
    /// Constrói um IHttpContextAccessor com claims JWT simulando usuário autenticado.
    /// </summary>
    private static IHttpContextAccessor MockAccessorWithClaims(string userId, string userName)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName),
        };
        var identity  = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var connectionMock = new Mock<ConnectionInfo>();
        connectionMock.SetupGet(c => c.RemoteIpAddress)
                      .Returns(System.Net.IPAddress.Loopback);

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.SetupGet(ctx => ctx.User)
                       .Returns(principal);
        httpContextMock.SetupGet(ctx => ctx.Connection)
                       .Returns(connectionMock.Object);
        httpContextMock.SetupGet(ctx => ctx.Request)
                       .Returns(MockRequestWithNoForwardedFor());

        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.SetupGet(a => a.HttpContext)
                    .Returns(httpContextMock.Object);

        return accessorMock.Object;
    }

    // ── Testes ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_SalvaLogComHashDeIP()
    {
        // Arrange
        const string ip = "192.168.1.100";
        var db       = CreateDb(nameof(LogAsync_SalvaLogComHashDeIP));
        var accessor = MockAccessorWithIp(ip);
        var service  = CreateService(db, accessor);

        // Act
        await service.LogAsync("Visualizou", "User", "123");

        // Assert
        var log = await db.AuditLogs.FirstOrDefaultAsync();
        log.Should().NotBeNull("o log deve ter sido persistido no banco");
        log!.IpHash.Should().NotBe(ip,
            "o IP nunca deve ser salvo em texto puro — deve ser um hash");
        log.IpHash.Length.Should().Be(64,
            "SHA-256 em hexadecimal sempre tem 64 caracteres");
        log.IpHash.Should().MatchRegex("^[0-9a-f]{64}$",
            "deve ser hex em lowercase");
    }

    [Fact]
    public async Task LogAsync_IpHashEDeterministico()
    {
        // Arrange — mesmo IP sempre gera o mesmo hash
        const string ip = "10.0.0.1";
        var db1 = CreateDb(nameof(LogAsync_IpHashEDeterministico) + "_1");
        var db2 = CreateDb(nameof(LogAsync_IpHashEDeterministico) + "_2");

        var service1 = CreateService(db1, MockAccessorWithIp(ip));
        var service2 = CreateService(db2, MockAccessorWithIp(ip));

        // Act
        await service1.LogAsync("A", "User", "1");
        await service2.LogAsync("A", "User", "1");

        // Assert
        var hash1 = (await db1.AuditLogs.FirstAsync()).IpHash;
        var hash2 = (await db2.AuditLogs.FirstAsync()).IpHash;
        hash1.Should().Be(hash2, "o mesmo IP deve gerar sempre o mesmo hash SHA-256");
    }

    [Fact]
    public async Task LogAsync_NaoLancaExcecaoSemHttpContext()
    {
        // Arrange — accessor retorna null (sem request ativo)
        var db             = CreateDb(nameof(LogAsync_NaoLancaExcecaoSemHttpContext));
        var accessorMock   = new Mock<IHttpContextAccessor>();
        accessorMock.SetupGet(a => a.HttpContext).Returns((HttpContext?)null);
        var service = CreateService(db, accessorMock.Object);

        // Act — não deve lançar exceção
        var act = async () => await service.LogAsync("Acao", "Entidade", "id-1");

        await act.Should().NotThrowAsync(
            "falha no contexto HTTP não deve derrubar o fluxo principal");

        // Assert — log salvo mesmo sem contexto
        var log = await db.AuditLogs.FirstOrDefaultAsync();
        log.Should().NotBeNull();
        log!.IpHash.Should().NotBeEmpty("o hash deve usar 'unknown' como fallback");
    }

    [Fact]
    public async Task LogAsync_ExtractorDeActorUserId_QuandoClaimPresente()
    {
        // Arrange
        const string userId   = "user-123";
        const string userName = "Maikon Admin";
        var db       = CreateDb(nameof(LogAsync_ExtractorDeActorUserId_QuandoClaimPresente));
        var accessor = MockAccessorWithClaims(userId, userName);
        var service  = CreateService(db, accessor);

        // Act
        await service.LogAsync("Editou", "User", userId);

        // Assert
        var log = await db.AuditLogs.FirstOrDefaultAsync();
        log.Should().NotBeNull();
        log!.ActorUserId.Should().Be(userId,
            "o ID do ator deve vir do claim NameIdentifier");
        log.ActorUserName.Should().Be(userName,
            "o nome do ator deve vir do claim Name");
    }

    [Fact]
    public async Task LogAsync_ActorUserId_NuloQuandoNaoAutenticado()
    {
        // Arrange — contexto sem claims (usuário anônimo)
        var db       = CreateDb(nameof(LogAsync_ActorUserId_NuloQuandoNaoAutenticado));
        var accessor = MockAccessorWithIp("127.0.0.1"); // sem claims
        var service  = CreateService(db, accessor);

        // Act
        await service.LogAsync("Acessou", "LgpdRequest", "proto-001");

        // Assert
        var log = await db.AuditLogs.FirstOrDefaultAsync();
        log.Should().NotBeNull();
        log!.ActorUserId.Should().BeNull(
            "sem claims o ActorUserId deve ser null (ação anônima)");
    }

    [Fact]
    public async Task LogAsync_SalvaCamposCorretamente()
    {
        // Arrange
        var db      = CreateDb(nameof(LogAsync_SalvaCamposCorretamente));
        var service = CreateService(db, MockAccessorWithIp("172.16.0.1"));

        // Act
        await service.LogAsync(
            action:     "Exportou",
            entityType: "User",
            entityId:   "abc-456",
            details:    "{\"campo\": \"valor\"}");

        // Assert
        var log = await db.AuditLogs.FirstOrDefaultAsync();
        log.Should().NotBeNull();
        log!.Action.Should().Be("Exportou");
        log.EntityType.Should().Be("User");
        log.EntityId.Should().Be("abc-456");
        log.Details.Should().Contain("campo");
        log.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LogAsync_HttpContextExplicito_TemPrioridadeSobreAccessor()
    {
        // Arrange — accessor com IP diferente do contexto explícito
        var db           = CreateDb(nameof(LogAsync_HttpContextExplicito_TemPrioridadeSobreAccessor));
        var accessorIp   = MockAccessorWithIp("1.1.1.1");
        var service      = CreateService(db, accessorIp);

        // Contexto explícito com IP diferente
        var connectionMock = new Mock<ConnectionInfo>();
        connectionMock.SetupGet(c => c.RemoteIpAddress)
                      .Returns(System.Net.IPAddress.Parse("9.9.9.9"));

        var explicitContextMock = new Mock<HttpContext>();
        explicitContextMock.SetupGet(ctx => ctx.Connection).Returns(connectionMock.Object);
        explicitContextMock.SetupGet(ctx => ctx.User).Returns(new ClaimsPrincipal());
        explicitContextMock.SetupGet(ctx => ctx.Request).Returns(MockRequestWithNoForwardedFor());

        // Act — passa o contexto explícito
        await service.LogAsync("Acao", "Tipo", null, null, explicitContextMock.Object);

        // Assert — hash deve corresponder ao IP do contexto explícito (9.9.9.9)
        // O AuditService aplica SHA-256(salt + ip), então o hash esperado deve incluir o salt.
        var hashEsperado = ComputeHash("9.9.9.9");

        var log = await db.AuditLogs.FirstOrDefaultAsync();
        log!.IpHash.Should().Be(hashEsperado,
            "o contexto explícito tem prioridade sobre o IHttpContextAccessor");
    }
}
