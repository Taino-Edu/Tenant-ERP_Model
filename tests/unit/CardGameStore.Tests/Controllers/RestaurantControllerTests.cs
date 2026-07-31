using System.Reflection;
using CardGameStore.Controllers;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        var controller = new RestaurantController(db, audit.Object)
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
}
