using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CardGameStore.DTOs;

namespace CardGameStore.Tests.DTOs;

public sealed class IntegrationAccessDtosTests
{
    [Fact]
    public void TokenRequest_UsesConstructorValidationAndSnakeCaseJson()
    {
        var constructor = typeof(IntegrationTokenRequest).GetConstructors().Single();

        constructor.GetParameters().Should().OnlyContain(parameter =>
            parameter.GetCustomAttributes(typeof(RequiredAttribute), true).Length == 1);

        var request = JsonSerializer.Deserialize<IntegrationTokenRequest>(
            """{"grant_type":"client_credentials","client_id":"client","client_secret":"secret"}""");

        request.Should().NotBeNull();
        request!.GrantType.Should().Be("client_credentials");
        request.ClientId.Should().Be("client");
        request.ClientSecret.Should().Be("secret");
    }

    [Fact]
    public void CreateClientRequest_UsesConstructorValidation()
    {
        var parameters = typeof(CreateIntegrationClientRequest)
            .GetConstructors().Single().GetParameters();

        parameters.Should().OnlyContain(parameter =>
            parameter.GetCustomAttributes(typeof(RequiredAttribute), true).Length == 1);
    }
}
