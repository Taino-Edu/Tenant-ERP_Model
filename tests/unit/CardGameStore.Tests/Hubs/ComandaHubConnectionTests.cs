// =============================================================================
// ComandaHubConnectionTests.cs — Em QUAL grupo o hub coloca quem conecta.
//
// O teste que já existia (ComandaHubTenantGroupTests) só compara as strings que a
// função de nome de grupo devolve. Ele passa mesmo com o hub colocando todo mundo
// no grupo errado, que foi exatamente o que aconteceu: admin de loja real entrava
// no grupo da tenant-zero e nunca mais recebia evento nenhum, sem erro no log.
//
// Aqui a asserção é sobre o comportamento: dado um tenant resolvido, o hub tem que
// inscrever a conexão no grupo DAQUELE tenant.
// =============================================================================

using System.Security.Claims;
using CardGameStore.Hubs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CardGameStore.Tests.Hubs;

public class ComandaHubConnectionTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");

    [Fact]
    public async Task Admin_EntraNoGrupoDoProprioTenant()
    {
        var (hub, grupos, connectionId) = Montar(TenantA, "loja_a", UserRole.Admin, Guid.NewGuid());

        await hub.OnConnectedAsync();

        grupos.Verify(g => g.AddToGroupAsync(
            connectionId, ComandaHub.GetAdminGroup(TenantA), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_NaoEntraNoGrupoDaTenantZero()
    {
        // A regressão real: com o tenant perdido, todo admin caía neste grupo e os
        // eventos da loja iam pra outro. Conexão viva, nenhum erro, zero entrega.
        var (hub, grupos, _) = Montar(TenantA, "loja_a", UserRole.Admin, Guid.NewGuid());

        await hub.OnConnectedAsync();

        grupos.Verify(g => g.AddToGroupAsync(
            It.IsAny<string>(),
            ComandaHub.GetAdminGroup(TenantConstants.TenantZeroId),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Operator_RecebeOMesmoGrupoDoAdmin()
    {
        var (hub, grupos, connectionId) = Montar(TenantA, "loja_a", UserRole.Operator, Guid.NewGuid());

        await hub.OnConnectedAsync();

        grupos.Verify(g => g.AddToGroupAsync(
            connectionId, ComandaHub.GetAdminGroup(TenantA), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cliente_EntraNoGrupoPessoalDoProprioTenant()
    {
        var userId = Guid.NewGuid();
        var (hub, grupos, connectionId) = Montar(TenantA, "loja_a", UserRole.Customer, userId);

        await hub.OnConnectedAsync();

        grupos.Verify(g => g.AddToGroupAsync(
            connectionId, ComandaHub.GetUserGroup(TenantA, userId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MesmoUsuarioEmTenantsDiferentes_CaiEmGruposDiferentes()
    {
        // O isolamento cross-tenant que motivou escopar os grupos por tenant
        // continua de pé depois da correção.
        var userId = Guid.NewGuid();
        var (hubA, gruposA, connA) = Montar(TenantA, "loja_a", UserRole.Customer, userId);
        var (hubB, gruposB, connB) = Montar(TenantB, "loja_b", UserRole.Customer, userId);

        await hubA.OnConnectedAsync();
        await hubB.OnConnectedAsync();

        gruposA.Verify(g => g.AddToGroupAsync(
            connA, ComandaHub.GetUserGroup(TenantA, userId), It.IsAny<CancellationToken>()), Times.Once);
        gruposB.Verify(g => g.AddToGroupAsync(
            connB, ComandaHub.GetUserGroup(TenantB, userId), It.IsAny<CancellationToken>()), Times.Once);
        gruposA.Verify(g => g.AddToGroupAsync(
            It.IsAny<string>(), ComandaHub.GetUserGroup(TenantB, userId), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Andaimes ──────────────────────────────────────────────────────────────

    private static (ComandaHub hub, Mock<IGroupManager> grupos, string connectionId) Montar(
        Guid tenantId, string schema, string role, Guid userId)
    {
        var tenant = new TenantContext();
        // O TenantHubFilter faz exatamente isto antes de o método do hub rodar.
        tenant.Set(tenantId, schema, ["fiscal"]);

        var service = new Mock<IComandaService>();
        service.Setup(s => s.GetActiveComandaIdByUserAsync(It.IsAny<Guid>()))
               .ReturnsAsync((Guid?)null);

        var grupos = new Mock<IGroupManager>();
        var caller = new FakeHubCallerContext(role, userId);

        var hub = new ComandaHub(service.Object, tenant, NullLogger<ComandaHub>.Instance)
        {
            Context = caller,
            Groups  = grupos.Object,
        };

        return (hub, grupos, caller.ConnectionId);
    }

    private sealed class FakeHubCallerContext : HubCallerContext
    {
        public FakeHubCallerContext(string role, Guid userId)
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.Role, role),
            ], authenticationType: "Test"));
        }

        public override string ConnectionId { get; } = Guid.NewGuid().ToString("N");
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}
