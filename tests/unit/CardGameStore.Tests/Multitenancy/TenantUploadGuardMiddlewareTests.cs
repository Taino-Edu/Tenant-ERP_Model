using CardGameStore.Controllers;
using CardGameStore.Multitenancy;
using Microsoft.AspNetCore.Http;

namespace CardGameStore.Tests.Multitenancy;

public class TenantUploadGuardMiddlewareTests
{
    [Fact]
    public async Task UploadDoMesmoTenant_SeguePipeline()
    {
        var tenantId = Guid.NewGuid();
        var tenant = Tenant(tenantId);
        var context = Context($"/uploads/t/{tenantId:N}/profiles/foto.webp");
        var nextChamado = false;
        var middleware = new TenantUploadGuardMiddleware(
            _ => { nextChamado = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, tenant);

        nextChamado.Should().BeTrue();
    }

    [Fact]
    public async Task UploadDeOutroTenant_Retorna404()
    {
        var tenant = Tenant(Guid.NewGuid());
        var context = Context($"/uploads/t/{Guid.NewGuid():N}/profiles/foto.webp");
        var nextChamado = false;
        var middleware = new TenantUploadGuardMiddleware(
            _ => { nextChamado = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, tenant);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        nextChamado.Should().BeFalse();
    }

    [Theory]
    [InlineData("/uploads/t/invalido/foto.webp")]
    [InlineData("/uploads/t/")]
    public async Task UploadParticionadoComTenantInvalido_Retorna404(string path)
    {
        var context = Context(path);
        var middleware = new TenantUploadGuardMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, Tenant(Guid.NewGuid()));

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UploadLegado_ContinuaFuncionandoDuranteMigracao()
    {
        var context = Context("/uploads/profiles/foto-antiga.webp");
        var nextChamado = false;
        var middleware = new TenantUploadGuardMiddleware(
            _ => { nextChamado = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, Tenant(Guid.NewGuid()));

        nextChamado.Should().BeTrue();
    }

    [Fact]
    public void DiretorioDeUpload_ContemTenantESubdiretorio()
    {
        var tenantId = Guid.NewGuid();

        var path = UploadController.TenantUploadDirectory(tenantId, "profiles");

        path.Should().Be(Path.Combine("uploads", "t", tenantId.ToString("N"), "profiles"));
    }

    private static DefaultHttpContext Context(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context;
    }

    private static ITenantContext Tenant(Guid tenantId)
    {
        var tenant = new TenantContext();
        tenant.Set(tenantId, $"tenant_{tenantId:N}", []);
        return tenant;
    }
}
