using System.Reflection;
using CardGameStore.Controllers;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CardGameStore.Tests.Controllers;

public class RestaurantControllerTests
{
    private static (RestaurantController Controller, AppDbContext Db) CreateController()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"restaurant_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<HttpContext?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CardGameStore.Models.PostgreSQL.AuditSeverity>()))
            .Returns(Task.CompletedTask);

        var client = new Mock<IClientProxy>();
        client.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(value => value.Group(It.IsAny<string>())).Returns(client.Object);
        var hub = new Mock<IHubContext<CardGameStore.Hubs.ComandaHub>>();
        hub.Setup(value => value.Clients).Returns(clients.Object);
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(value => value.TenantId).Returns(Guid.NewGuid());

        var controller = new RestaurantController(db, audit.Object, hub.Object, tenant.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        return (controller, db);
    }

    [Fact]
    public void Module_IsOptInAndControllerIsProtected()
    {
        TenantProvisioningService.KnownModules.Should().Contain("restaurante");
        new Tenant().EnabledModules.Should().NotContain("restaurante");

        var attribute = typeof(RestaurantController).GetCustomAttribute<RequireModuleAttribute>();
        attribute.Should().NotBeNull("a API não pode depender apenas do menu escondido");

        var moduleField = typeof(RequireModuleAttribute).GetField("_module", BindingFlags.Instance | BindingFlags.NonPublic);
        moduleField!.GetValue(attribute).Should().Be("restaurante");

        // Comanda é plano base: não pode ganhar gate de módulo de novo. Quando
        // tinha, a tela sumia pra quem não contratou "restaurante" — inclusive
        // pro plano Mar, o único que não inclui o módulo.
        typeof(ComandaController).GetCustomAttribute<RequireModuleAttribute>()
            .Should().BeNull("comanda faz parte do plano base, não do módulo Restaurante");
    }

    [Fact]
    public async Task ProductionArea_CrudIsAdditiveAndDeactivatePreservesRow()
    {
        var (controller, db) = CreateController();
        await using var _ = db;

        var createdResult = await controller.CreateProductionArea(new SaveRestaurantProductionAreaRequest
        {
            Name = " Cozinha ",
            Description = " Pratos quentes ",
            Color = "#aabbcc",
            DisplayOrder = 1,
        });

        var created = createdResult.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = created.Value.Should().BeOfType<RestaurantProductionAreaDto>().Subject;
        dto.Name.Should().Be("Cozinha");
        dto.Description.Should().Be("Pratos quentes");
        dto.Color.Should().Be("#AABBCC");

        (await controller.ListProductionAreas()).Result.Should().BeOfType<OkObjectResult>();
        (await controller.DeactivateProductionArea(dto.Id)).Should().BeOfType<NoContentResult>();

        (await controller.ListProductionAreas()).Value.Should().BeNull();
        var activeResult = (await controller.ListProductionAreas()).Result.Should().BeOfType<OkObjectResult>().Subject;
        activeResult.Value.Should().BeAssignableTo<IEnumerable<RestaurantProductionAreaDto>>()
            .Which.Should().BeEmpty();

        var allResult = (await controller.ListProductionAreas(includeInactive: true)).Result
            .Should().BeOfType<OkObjectResult>().Subject;
        allResult.Value.Should().BeAssignableTo<IEnumerable<RestaurantProductionAreaDto>>()
            .Which.Should().ContainSingle(item => item.Id == dto.Id && !item.IsActive);

        var reactivated = await controller.ReactivateProductionArea(dto.Id);
        reactivated.Result.Should().BeOfType<OkObjectResult>();
        (await db.RestaurantProductionAreas.FindAsync(dto.Id))!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ProductionArea_DuplicateNameIgnoringCaseReturnsConflict()
    {
        var (controller, db) = CreateController();
        await using var _ = db;

        await controller.CreateProductionArea(new SaveRestaurantProductionAreaRequest
        {
            Name = "Bar", Color = "#112233",
        });
        var duplicate = await controller.CreateProductionArea(new SaveRestaurantProductionAreaRequest
        {
            Name = "bar", Color = "#445566",
        });

        duplicate.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ProductMapping_AssignsOnlyActiveArea()
    {
        var (controller, db) = CreateController();
        await using var _ = db;
        var product = new Product { Name = "Hambúrguer", Category = "Lanche", IsActive = true };
        var area = new RestaurantProductionArea { Name = "Cozinha", IsActive = true };
        db.AddRange(product, area);
        await db.SaveChangesAsync();

        var result = await controller.AssignProductProductionArea(product.Id,
            new AssignProductProductionAreaRequest { ProductionAreaId = area.Id });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Products.FindAsync(product.Id))!.RestaurantProductionAreaId.Should().Be(area.Id);
    }

    [Fact]
    public async Task ProductionStatus_AdvancesInOrderAndRejectsJump()
    {
        var (controller, db) = CreateController();
        await using var _ = db;
        var user = new User { Name = "Cliente", Role = UserRole.Customer, PasswordHash = "hash" };
        var area = new RestaurantProductionArea { Name = "Cozinha" };
        var comanda = new Comanda { User = user, UserId = user.Id, Status = ComandaStatus.EmAndamento };
        var item = new ComandaItem
        {
            Comanda = comanda,
            ComandaId = comanda.Id,
            ItemNameSnapshot = "Prato",
            Quantity = 1,
            ProductionAreaId = area.Id,
            ProductionAreaNameSnapshot = area.Name,
            ProductionStatus = RestaurantProductionStatus.Recebido,
        };
        db.AddRange(user, area, comanda, item);
        await db.SaveChangesAsync();

        var jump = await controller.UpdateProductionStatus(comanda.Id, item.Id,
            new UpdateProductionStatusRequest { Status = "Pronto" });
        jump.Result.Should().BeOfType<ConflictObjectResult>();

        var preparing = await controller.UpdateProductionStatus(comanda.Id, item.Id,
            new UpdateProductionStatusRequest { Status = "Preparando" });
        preparing.Result.Should().BeOfType<OkObjectResult>();
        (await db.ComandaItems.FindAsync(item.Id))!.ProductionStartedAt.Should().NotBeNull();
    }
}
