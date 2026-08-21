// =============================================================================
// PublicSitemapEndpointTests.cs — trava a superfície do endpoint de sitemap.
//
// GET /api/public/sitemap é anônimo e, por natureza, enumerável: ele devolve a
// lista inteira do catálogo público de uma loja. Isso é aceitável enquanto ele
// devolver só o que um sitemap precisa (id e data). O risco não é o endpoint
// como está — é o próximo campo. "Já que estamos aqui, devolve o nome também"
// transforma um sitemap num raspador de catálogo pronto, e a mudança passaria
// despercebida numa revisão porque parece inofensiva.
//
// Daí um teste sobre a FORMA do DTO, não sobre o comportamento: é a forma que
// alguém mudaria sem perceber o que está abrindo.
// =============================================================================

using System.Reflection;
using CardGameStore.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace CardGameStore.Tests.Controllers;

public class PublicSitemapEndpointTests
{
    [Fact]
    public void SitemapDto_SoExpoeIdEData()
    {
        var propriedades = typeof(PublicSitemapEntryDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToArray();

        propriedades.Should().BeEquivalentTo(
            new[] { "Id", "UpdatedAt" },
            "o endpoint é anônimo e devolve o catálogo inteiro — cada campo a mais é raspagem facilitada");
    }

    [Fact]
    public void ControllerPublico_ContinuaAnonimoEDeclarado()
    {
        // Se um dia alguém tirar o [AllowAnonymous] daqui, o sitemap de toda
        // loja para de listar produtos em silêncio: o Next.js trata QUALQUER
        // falha como lista vazia, de propósito, então o sintoma não seria um
        // erro — seria o catálogo sumindo do Google sem ninguém notar.
        typeof(PublicDirectoryController)
            .GetCustomAttribute<AllowAnonymousAttribute>()
            .Should().NotBeNull();

        typeof(PublicDirectoryController)
            .GetMethod(nameof(PublicDirectoryController.GetPublicSitemap))
            .Should().NotBeNull("app/sitemap.ts depende deste nome de rota");
    }
}
