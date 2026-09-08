using CardGameStore.Configuration;
using CardGameStore.Security;
using FluentAssertions;

namespace CardGameStore.Tests.Security;

public class ProductionConfigurationGuardTests
{
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("SUBSTITUA_ESTA_CHAVE_POR_UMA_SEGURA_COM_32_CHARS")]
    public void ProductionRejectsUnsafeJwt(string secret)
    {
        var settings = new JwtSettings { SecretKey = secret, Issuer = "issuer", Audience = "audience" };
        FluentActions.Invoking(() => ProductionConfigurationGuard.ValidateJwt(settings, true))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DevelopmentAllowsExampleJwt() =>
        ProductionConfigurationGuard.ValidateJwt(new JwtSettings(), false);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SenhaForte@123")]
    public void ProductionRejectsUnsafeSeedPassword(string? password) =>
        FluentActions.Invoking(() => ProductionConfigurationGuard.SeedPassword(password, true, "SEED_PASSWORD"))
            .Should().Throw<InvalidOperationException>();
}
