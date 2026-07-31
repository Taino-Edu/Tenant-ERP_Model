using CardGameStore.Multitenancy;

namespace CardGameStore.Tests.Multitenancy;

public class TenantSchemaNameTests
{
    [Theory]
    [InlineData("public")]
    [InlineData("tenant_0123456789abcdef")]
    [InlineData("_tenant")]
    [InlineData("Tenant_A1")]
    public void Validate_AcceptsSafePostgresIdentifiers(string schema)
    {
        TenantSchemaName.Validate(schema).Should().Be(schema);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1tenant")]
    [InlineData("tenant-name")]
    [InlineData("tenant\"; DROP SCHEMA public CASCADE;--")]
    public void Validate_RejectsUnsafeIdentifiers(string schema)
    {
        var action = () => TenantSchemaName.Validate(schema);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Validate_RejectsIdentifiersLongerThanPostgresLimit()
    {
        var action = () => TenantSchemaName.Validate(new string('a', 64));

        action.Should().Throw<InvalidOperationException>();
    }
}
