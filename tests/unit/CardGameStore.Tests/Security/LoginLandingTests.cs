using CardGameStore.Security;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.Security;

public class LoginLandingTests
{
    [Theory]
    [InlineData("/admin/primeiros-passos", true)]
    [InlineData("/admin/estoque", true)]
    [InlineData("/admin/financeiro/insights", true)]
    [InlineData("/admin", false)]
    [InlineData("/admin/", false)]
    [InlineData("//evil.com/admin/x", false)]
    [InlineData("https://evil.com/admin/x", false)]
    [InlineData("/admin/../plataforma", false)]
    [InlineData("/admin/primeiros-passos?x=1", false)]
    [InlineData("/plataforma/tenants", false)]
    [InlineData("/Admin/Estoque", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SoAceitaCaminhoInternoDoPainel(string? caminho, bool esperado) =>
        LoginLanding.EhCaminhoSeguroDoPainel(caminho).Should().Be(esperado);
}
