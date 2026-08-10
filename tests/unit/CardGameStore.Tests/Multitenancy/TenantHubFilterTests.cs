// =============================================================================
// TenantHubFilterTests.cs — O filtro que carrega o tenant do handshake pra dentro
// do escopo de DI que o SignalR cria por invocação de hub.
//
// O bug que motivou tudo isto não era detectável pelo teste que existia: ele só
// comparava strings de nome de grupo, nunca exercitava o caminho onde o tenant se
// perdia. Aqui o alvo é justamente esse caminho.
// =============================================================================

using System.Security.Claims;
using CardGameStore.Multitenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
// IHttpContextFeature aqui é a do SignalR (Http.Connections.Features), não a
// homônima de Microsoft.AspNetCore.Http.Features — é essa que o
// HubCallerContext.GetHttpContext() consulta.
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardGameStore.Tests.Multitenancy;

public class TenantHubFilterTests
{
    private static readonly Guid TenantReal = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task OnConnected_ComTenantNoHandshake_PopulaOContextoDoEscopoDoHub()
    {
        var (caller, services, tenant) = Cenario(
            new TenantSnapshot(TenantReal, "loja_teste", ["fiscal", "estoque"]));

        await new TenantHubFilter().OnConnectedAsync(Lifetime(caller, services), _ => Task.CompletedTask);

        tenant.IsExplicitlySet.Should().BeTrue(
            "sem Set() no escopo do hub, o TenantConnectionInterceptor derruba toda " +
            "conexão de cliente — foi exatamente o erro visto em produção");
        tenant.TenantId.Should().Be(TenantReal);
        tenant.SchemaName.Should().Be("loja_teste");
        tenant.EnabledModules.Should().BeEquivalentTo(["fiscal", "estoque"]);
    }

    [Fact]
    public async Task OnConnected_SemTenantNoHandshake_Falha()
    {
        // Cair no default (tenant-zero) seria o pior desfecho possível: a conexão
        // sobe, ninguém vê erro, e o admin fica num grupo que nunca recebe evento.
        var (caller, services, _) = Cenario(snapshot: null);

        var act = () => new TenantHubFilter().OnConnectedAsync(Lifetime(caller, services), _ => Task.CompletedTask);

        await act.Should().ThrowAsync<HubException>();
    }

    [Fact]
    public async Task InvocacaoDeMetodo_ReaproveitaOTenantSemRelerOHttpContext()
    {
        var (caller, services, _) = Cenario(new TenantSnapshot(TenantReal, "loja_teste", ["fiscal"]));
        var filtro = new TenantHubFilter();

        await filtro.OnConnectedAsync(Lifetime(caller, services), _ => Task.CompletedTask);

        // O HttpContext é objeto do pipeline HTTP e não foi feito pra ser lido a
        // cada mensagem de WebSocket. Depois do connect, o valor tem que vir dos
        // Items da conexão — que é o que este teste força, removendo o feature.
        caller.Features.Set<IHttpContextFeature>(null);

        var escopoNovo = ServicesComTenantContext();
        await filtro.InvokeMethodAsync(
            new HubInvocationContext(caller, escopoNovo, new HubDeTeste(), MetodoQualquer(), []),
            _ => ValueTask.FromResult<object?>(null));

        var tenantDaInvocacao = escopoNovo.GetRequiredService<ITenantContext>();
        tenantDaInvocacao.TenantId.Should().Be(TenantReal);
        tenantDaInvocacao.SchemaName.Should().Be("loja_teste");
    }

    [Fact]
    public async Task TenantsDiferentes_NaoCompartilhamContexto()
    {
        var outro = Guid.Parse("99999999-8888-7777-6666-555555555555");
        var (callerA, servicesA, tenantA) = Cenario(new TenantSnapshot(TenantReal, "loja_a", ["fiscal"]));
        var (callerB, servicesB, tenantB) = Cenario(new TenantSnapshot(outro, "loja_b", ["fiscal"]));
        var filtro = new TenantHubFilter();

        await filtro.OnConnectedAsync(Lifetime(callerA, servicesA), _ => Task.CompletedTask);
        await filtro.OnConnectedAsync(Lifetime(callerB, servicesB), _ => Task.CompletedTask);

        tenantA.TenantId.Should().Be(TenantReal);
        tenantB.TenantId.Should().Be(outro);
        tenantA.SchemaName.Should().NotBe(tenantB.SchemaName);
    }

    // ── Andaimes ──────────────────────────────────────────────────────────────

    private static (FakeHubCallerContext caller, IServiceProvider services, ITenantContext tenant) Cenario(
        TenantSnapshot? snapshot)
    {
        var http = new DefaultHttpContext();
        if (snapshot is not null)
            http.Items[TenantHubFilter.HttpContextItemKey] = snapshot;

        var caller = new FakeHubCallerContext();
        caller.Features.Set<IHttpContextFeature>(new HttpContextFeature { HttpContext = http });

        var services = ServicesComTenantContext();
        return (caller, services, services.GetRequiredService<ITenantContext>());
    }

    private static IServiceProvider ServicesComTenantContext() =>
        new ServiceCollection().AddScoped<ITenantContext, TenantContext>().BuildServiceProvider();

    private static HubLifetimeContext Lifetime(HubCallerContext caller, IServiceProvider services) =>
        new(caller, services, new HubDeTeste());

    private static System.Reflection.MethodInfo MetodoQualquer() =>
        typeof(HubDeTeste).GetMethod(nameof(HubDeTeste.Ping))!;

    private sealed class HubDeTeste : Hub
    {
        public void Ping() { }
    }

    private sealed class HttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class FakeHubCallerContext : HubCallerContext
    {
        public override string ConnectionId { get; } = Guid.NewGuid().ToString("N");
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User { get; } = new();
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}
