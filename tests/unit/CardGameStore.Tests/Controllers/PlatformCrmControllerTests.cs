using CardGameStore.Controllers;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CardGameStore.Tests.Controllers;

public sealed class PlatformCrmControllerTests
{
    private static CatalogDbContext CreateCatalog() => new(
        new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AppDbContext CreateUsers() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task SaveOpportunity_CriaFunilEHistoricoImutavel()
    {
        await using var catalog = CreateCatalog();
        await using var users = CreateUsers();
        var lead = new Lead { Nome = "Empresa", Telefone = "14999990000" };
        catalog.Leads.Add(lead);
        await catalog.SaveChangesAsync();
        var controller = new PlatformCrmController(catalog, users);

        var first = await controller.SaveOpportunity(lead.Id, new SaveCrmOpportunityRequest
        {
            Stage = "Qualificacao", Probability = 20, Value = 1500,
        }, default);
        var second = await controller.SaveOpportunity(lead.Id, new SaveCrmOpportunityRequest
        {
            Stage = "Proposta", Probability = 55, Value = 1500,
        }, default);

        first.Result.Should().BeOfType<OkObjectResult>();
        second.Result.Should().BeOfType<OkObjectResult>();
        (await catalog.CrmOpportunities.SingleAsync()).Stage.Should().Be(CrmOpportunityStage.Proposta);
        var history = await catalog.CrmActivities.OrderBy(a => a.CreatedAt).ToListAsync();
        history.Should().HaveCount(2);
        history.Should().OnlyContain(a => a.Type == CrmActivityType.MudancaEtapa);
        history[0].Title.Should().Contain("criada");
        history[1].Title.Should().Contain("Qualificacao → Proposta");
    }

    [Fact]
    public async Task SaveOpportunity_PerdidaExigeMotivo()
    {
        await using var catalog = CreateCatalog();
        await using var users = CreateUsers();
        var lead = new Lead { Nome = "Empresa", Telefone = "14999990000" };
        catalog.Leads.Add(lead);
        await catalog.SaveChangesAsync();

        var result = await new PlatformCrmController(catalog, users).SaveOpportunity(
            lead.Id, new SaveCrmOpportunityRequest { Stage = "Perdido", Probability = 0 }, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await catalog.CrmOpportunities.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TaskFlow_RegistraEConcluiAtividade()
    {
        await using var catalog = CreateCatalog();
        await using var users = CreateUsers();
        var lead = new Lead { Nome = "Empresa", Telefone = "14999990000" };
        catalog.Leads.Add(lead);
        await catalog.SaveChangesAsync();
        var controller = new PlatformCrmController(catalog, users);

        var created = await controller.CreateActivity(lead.Id, new CreateCrmActivityRequest
        {
            Type = "Tarefa", Title = "Retornar proposta", DueAt = DateTime.UtcNow.AddDays(1),
        }, default);
        var createdResult = created.Result.Should().BeOfType<ObjectResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<CrmActivityDto>().Subject;
        var completed = await controller.CompleteActivity(dto.Id,
            new CompleteCrmActivityRequest { Outcome = "Cliente aprovou" }, default);

        completed.Result.Should().BeOfType<OkObjectResult>();
        var activity = await catalog.CrmActivities.SingleAsync();
        activity.CompletedAt.Should().NotBeNull();
        activity.Outcome.Should().Be("Cliente aprovou");
    }
}
