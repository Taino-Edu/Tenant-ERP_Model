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
        var openTasks = await controller.ListOpenTasks(default);
        var taskList = ((OkObjectResult)openTasks.Result!).Value
            .Should().BeAssignableTo<IReadOnlyList<CrmTaskDto>>().Subject;
        taskList.Should().ContainSingle(task => task.LeadName == "Empresa");
        var completed = await controller.CompleteActivity(dto.Id,
            new CompleteCrmActivityRequest { Outcome = "Cliente aprovou" }, default);

        completed.Result.Should().BeOfType<OkObjectResult>();
        var activity = await catalog.CrmActivities.SingleAsync();
        activity.CompletedAt.Should().NotBeNull();
        activity.Outcome.Should().Be("Cliente aprovou");
    }

    [Fact]
    public async Task ContatoAtivo_ComLegitimoInteressePendente_FicaBloqueado()
    {
        await using var catalog = CreateCatalog();
        await using var users = CreateUsers();
        var lead = new Lead
        {
            Nome = "Empresa", Telefone = "14999990000",
            LegalBasis = LeadLegalBasis.LegitimoInteresse,
        };
        catalog.Leads.Add(lead);
        await catalog.SaveChangesAsync();

        var result = await new PlatformCrmController(catalog, users).CreateActivity(lead.Id,
            new CreateCrmActivityRequest { Type = "WhatsApp", Title = "Primeiro contato" }, default);

        result.Result.Should().BeOfType<ConflictObjectResult>();
        (await catalog.CrmActivities.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ContatoAtivo_AposAvaliacao_PodeSerRegistrado()
    {
        await using var catalog = CreateCatalog();
        await using var users = CreateUsers();
        var lead = new Lead
        {
            Nome = "Empresa", Telefone = "14999990000",
            LegalBasis = LeadLegalBasis.LegitimoInteresse,
            LegitimateInterestAssessedAt = DateTime.UtcNow,
        };
        catalog.Leads.Add(lead);
        await catalog.SaveChangesAsync();

        var result = await new PlatformCrmController(catalog, users).CreateActivity(lead.Id,
            new CreateCrmActivityRequest { Type = "WhatsApp", Title = "Primeiro contato" }, default);

        result.Result.Should().BeOfType<ObjectResult>();
        (await catalog.CrmActivities.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PrivacyAudit_EncadeiaEventosSemSobrescreverHistorico()
    {
        await using var catalog = CreateCatalog();
        var lead = new Lead { Nome = "Empresa", Telefone = "14999990000" };
        catalog.Leads.Add(lead);
        await catalog.SaveChangesAsync();

        var first = await LeadPrivacyAudit.AppendAsync(catalog, lead.Id, "Created", new { source = "landing" });
        await catalog.SaveChangesAsync();
        var second = await LeadPrivacyAudit.AppendAsync(catalog, lead.Id, "Opposition", new { reason = "pedido" });
        await catalog.SaveChangesAsync();

        second.PreviousHash.Should().Be(first.EventHash);
        second.EventHash.Should().NotBe(first.EventHash);
        (await catalog.LeadPrivacyEvents.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Analytics_ReconciliaConversaoPipelineERevisao()
    {
        await using var catalog = CreateCatalog();
        await using var users = CreateUsers();
        var converted = new Lead { Nome = "Ganho", Telefone = "1", Status = LeadStatus.Convertido, ConvertedTenantId = Guid.NewGuid(), ConvertedAt = DateTime.UtcNow };
        var open = new Lead { Nome = "Aberto", Telefone = "2", RetentionReviewAt = DateTime.UtcNow.AddDays(-1) };
        catalog.Leads.AddRange(converted, open);
        catalog.CrmOpportunities.Add(new CrmOpportunity { Lead = open, Value = 1000, Probability = 50, Stage = CrmOpportunityStage.Proposta });
        await catalog.SaveChangesAsync();

        var result = await new PlatformCrmController(catalog, users).Analytics(default);
        var dto = ((OkObjectResult)result.Result!).Value.Should().BeOfType<CrmAnalyticsDto>().Subject;

        dto.TotalLeads.Should().Be(2);
        dto.ConversionRate.Should().Be(50);
        dto.OpenPipeline.Should().Be(1000);
        dto.WeightedPipeline.Should().Be(500);
        dto.RetentionReviewsDue.Should().Be(1);
    }

    [Fact]
    public async Task ReviewRetention_AnonimizaDadosERegistraEvento()
    {
        await using var catalog = CreateCatalog();
        await using var users = CreateUsers();
        var lead = new Lead { Nome = "Pessoa", Telefone = "14999990000", Email = "pessoa@empresa.com" };
        catalog.Leads.Add(lead);
        await catalog.SaveChangesAsync();

        var result = await new PlatformCrmController(catalog, users).ReviewRetention(lead.Id,
            new ReviewLeadRetentionRequest { Action = "Anonymize", Reason = "Finalidade encerrada" }, default);

        result.Should().BeOfType<NoContentResult>();
        var saved = await catalog.Leads.SingleAsync();
        saved.Email.Should().BeNull();
        saved.Telefone.Should().BeEmpty();
        saved.AnonymizedAt.Should().NotBeNull();
        (await catalog.LeadPrivacyEvents.SingleAsync()).EventType.Should().Be("LeadAnonymized");
    }
}
