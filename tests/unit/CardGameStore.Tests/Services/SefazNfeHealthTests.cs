using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Services;

public class SefazNfeHealthTests
{
    [Fact]
    public async Task TestarStatusSemConfiguracaoNaoTocaNoEstadoDeDistribuicao()
    {
        using var db = TestDbFactory.Create(nameof(TestarStatusSemConfiguracaoNaoTocaNoEstadoDeDistribuicao));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tenant = CreateTenant();
        var service = CreateService(db, tenant, cache);

        var result = await service.TestarStatusAsync();

        result.Configured.Should().BeFalse();
        result.Online.Should().BeFalse();
        db.SefazDistributionStates.Should().BeEmpty(
            "teste de saúde não pode consumir NSU, quota ou criar cooldown");
    }

    [Fact]
    public async Task IndicadorAutomaticoReusaCacheCurtoPorTenantUfEAmbiente()
    {
        using var db = TestDbFactory.Create(nameof(IndicadorAutomaticoReusaCacheCurtoPorTenantUfEAmbiente));
        var tenant = CreateTenant();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        db.FiscalConfigs.Add(new FiscalConfig
        {
            Cnpj = "12.345.678/0001-99",
            Uf = "SP",
            Ambiente = AmbienteFiscal.Homologacao,
            CertificadoPfxEncrypted = "valor-não-usado-porque-está-em-cache",
        });
        await db.SaveChangesAsync();

        var expected = new SefazHealthResult(
            true, true, 107, "Serviço em Operação", DateTime.UtcNow, 42, "Homologacao", "SP");
        cache.Set($"sefaz-health:{tenant.TenantId:N}:SP:Homologacao", expected);

        var result = await CreateService(db, tenant, cache).TestarStatusAsync(forceRefresh: false);

        result.Should().Be(expected);
        db.SefazDistributionStates.Should().BeEmpty();
    }

    private static TenantContext CreateTenant()
    {
        var tenant = new TenantContext();
        tenant.Set(Guid.NewGuid(), "public", ["fiscal"]);
        return tenant;
    }

    private static SefazNfeService CreateService(
        AppDbContext db, TenantContext tenant, IMemoryCache cache)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Development");
        var encryption = new EncryptionService(new ConfigurationBuilder().Build(), environment.Object);
        return new SefazNfeService(
            db,
            encryption,
            new SefazDistributionGuard(db),
            tenant,
            cache,
            NullLogger<SefazNfeService>.Instance);
    }
}
