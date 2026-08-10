using CardGameStore.Hubs;

namespace CardGameStore.Tests.Hubs;

public class ComandaHubTenantGroupTests
{
    [Fact]
    public void GruposComMesmoIdDeEntidade_SaoDiferentesEntreTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var entidadeId = Guid.NewGuid();

        ComandaHub.GetAdminGroup(tenantA)
            .Should().NotBe(ComandaHub.GetAdminGroup(tenantB));
        ComandaHub.GetUserGroup(tenantA, entidadeId)
            .Should().NotBe(ComandaHub.GetUserGroup(tenantB, entidadeId));
        ComandaHub.GetComandaGroup(tenantA, entidadeId)
            .Should().NotBe(ComandaHub.GetComandaGroup(tenantB, entidadeId));
    }

    [Fact]
    public void TiposDeGrupoDoMesmoTenant_NaoColidem()
    {
        var tenantId = Guid.NewGuid();
        var entidadeId = Guid.NewGuid();

        var grupos = new[]
        {
            ComandaHub.GetAdminGroup(tenantId),
            ComandaHub.GetUserGroup(tenantId, entidadeId),
            ComandaHub.GetComandaGroup(tenantId, entidadeId),
        };

        grupos.Should().OnlyHaveUniqueItems();
    }
}
