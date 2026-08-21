// =============================================================================
// PlatformReadPermissionTests.cs — o par leitura/escrita das permissões.
//
// Leads, Support e Team eram permissão única: ver e mexer no mesmo token. Não
// existia "ver sem poder mexer", e por isso a Auditoria era cega justamente nas
// três áreas — a alternativa seria dar a ela o poder de editar lead, executar
// ação de LGPD e responder chamado.
//
// A separação criou uma armadilha nova: as duas permissões NÃO se implicam no
// middleware (a checagem é igualdade + curinga). Um perfil com `platform.leads`
// e sem `platform.leads.read` passa a escrever sem conseguir ler — e o sintoma
// seria a tela abrir vazia ou dar 403 no GET, com o botão de salvar
// funcionando. É o tipo de bug que só aparece em produção, com quem tem o
// perfil errado. Daí a trava abaixo, derivada por reflexão: par novo criado
// depois já nasce coberto.
// =============================================================================

using System.Reflection;
using CardGameStore.Controllers;
using CardGameStore.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CardGameStore.Tests.Security;

public sealed class PlatformReadPermissionTests
{
    /// <summary>Todo valor declarado em PlatformPermission.</summary>
    private static readonly string[] Todas = typeof(PlatformPermission)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    /// <summary>Para cada permissão `x.read`, as permissões de escrita da mesma
    /// área: `x`, `x.manage` e `x.delete`, quando existirem.</summary>
    private static IEnumerable<(string Leitura, string Escrita)> Pares()
    {
        foreach (var leitura in Todas.Where(p => p.EndsWith(".read", StringComparison.Ordinal)))
        {
            var basePermissao = leitura[..^".read".Length];
            foreach (var candidata in new[] { basePermissao, $"{basePermissao}.manage", $"{basePermissao}.delete" })
                if (Todas.Contains(candidata, StringComparer.Ordinal))
                    yield return (leitura, candidata);
        }
    }

    [Fact]
    public void Pares_ExistemDeVerdade()
    {
        // Se o parser não achar nada (renomearam o padrão `.read`, por exemplo),
        // o teste abaixo passaria sem verificar coisa alguma.
        Pares().Should().NotBeEmpty();
    }

    [Fact]
    public void PerfilComEscrita_SempreTemALeituraDaMesmaArea()
    {
        var perfis = PlatformAccessProfiles.All.Values.Where(p => p.Selectable);

        foreach (var perfil in perfis)
        {
            if (perfil.Permissions.Contains(PlatformPermission.All, StringComparer.OrdinalIgnoreCase))
                continue; // curinga cobre tudo

            foreach (var (leitura, escrita) in Pares())
            {
                if (!perfil.Permissions.Contains(escrita, StringComparer.OrdinalIgnoreCase)) continue;

                perfil.Permissions.Should().Contain(leitura,
                    $"o perfil \"{perfil.Key}\" tem {escrita} e precisa de {leitura} para conseguir ABRIR a tela que ele pode editar");
            }
        }
    }

    [Fact]
    public void Auditoria_VeTudoEmLeituraEEscreveNada()
    {
        var auditoria = PlatformAccessProfiles.All[PlatformAccessProfiles.Auditor];

        // Vê: toda permissão `.read` declarada, mais as duas áreas que só têm
        // leitura (indicadores e logs não têm par de escrita).
        foreach (var leitura in Todas.Where(p => p.EndsWith(".read", StringComparison.Ordinal)))
            auditoria.Permissions.Should().Contain(leitura,
                $"auditoria precisa enxergar {leitura} — é o perfil que confere o resto");

        auditoria.Permissions.Should().Contain(PlatformPermission.Dashboard);
        auditoria.Permissions.Should().Contain(PlatformPermission.Logs);

        // Não escreve nada, não tem curinga.
        auditoria.Permissions.Should().NotContain(PlatformPermission.All);
        foreach (var (_, escrita) in Pares())
            auditoria.Permissions.Should().NotContain(escrita,
                $"auditoria é somente leitura — {escrita} deixaria o auditor agir sobre o que ele fiscaliza");

        // Impersonate fica de fora por decisão, não por esquecimento: ela não
        // mostra informação da plataforma, ela assume a identidade de outra
        // pessoa dentro da loja do cliente.
        auditoria.Permissions.Should().NotContain(PlatformPermission.Impersonate);
    }

    /// <summary>O desenho "piso na classe + escrita no método" depende de o
    /// ASP.NET Core juntar os DOIS atributos no metadata do endpoint, e de o
    /// middleware exigir todos (requirements.All). Se qualquer uma das duas
    /// coisas mudar, a escrita passaria a exigir só `.read` — e a Auditoria
    /// ganharia poder de gravar sem ninguém perceber. Este teste monta as rotas
    /// de verdade e confere no endpoint construído.</summary>
    [Theory]
    // O RawText traz a restrição de rota junto ({leadId:guid}), não só o nome.
    [InlineData("api/platform/crm/leads/{leadId:guid}/opportunity", PlatformPermission.LeadsRead, PlatformPermission.Leads)]
    [InlineData("api/platform/prospecting/search", PlatformPermission.LeadsRead, PlatformPermission.Leads)]
    [InlineData("api/platform/team/invitations", PlatformPermission.TeamRead, PlatformPermission.Team)]
    public async Task RotaDeEscrita_ExigeLeituraEEscrita(string rota, string leitura, string escrita)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(PlatformController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .SingleOrDefault(e => e.RoutePattern.RawText == rota);

        endpoint.Should().NotBeNull($"a rota {rota} precisa existir — se ela foi renomeada, este teste perdeu o alvo");

        var exigidas = endpoint!.Metadata
            .GetOrderedMetadata<RequirePlatformPermissionAttribute>()
            .Select(a => a.Permission)
            .ToArray();

        exigidas.Should().Contain(leitura);
        exigidas.Should().Contain(escrita);
    }

    /// <summary>E o espelho: a rota de leitura NÃO pode exigir a de escrita,
    /// senão a Auditoria continuaria cega — que é o problema que tudo isto veio
    /// resolver.</summary>
    [Theory]
    [InlineData("api/platform/leads", PlatformPermission.Leads)]
    [InlineData("api/platform/support-tickets", PlatformPermission.Support)]
    [InlineData("api/platform/team", PlatformPermission.Team)]
    public async Task RotaDeLeitura_NaoExigeEscrita(string rota, string escrita)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(PlatformController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .FirstOrDefault(e => e.RoutePattern.RawText == rota &&
                e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("GET"));

        endpoint.Should().NotBeNull($"a rota GET {rota} precisa existir");

        endpoint!.Metadata
            .GetOrderedMetadata<RequirePlatformPermissionAttribute>()
            .Select(a => a.Permission)
            .Should().NotContain(escrita, $"GET {rota} é o que a Auditoria abre — exigir {escrita} a deixaria de fora");
    }
}
