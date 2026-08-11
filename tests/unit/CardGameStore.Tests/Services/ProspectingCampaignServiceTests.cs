using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CardGameStore.Tests.Services;

public class ProspectingCampaignServiceTests
{
    private static CatalogDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task CreateAsync_PersisteCampanhaAtivaEVencidaParaPrimeiraExecucao()
    {
        using var db = CreateDb();
        var service = new ProspectingCampaignService(db);

        var result = await service.CreateAsync(new CreateProspectingCampaignRequest
        {
            Name = " Restaurantes semanais ", Categoria = " restaurante ",
            Cidade = " Ribeirão Preto, SP ", IntervalHours = 168,
        });

        result.Status.Should().Be("Active");
        result.Name.Should().Be("Restaurantes semanais");
        result.Categoria.Should().Be("restaurante");
        result.NextRunAt.Should().BeOnOrBefore(DateTime.UtcNow);
        (await db.ProspectingCampaigns.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_EhIdempotenteEAgendaProximoCiclo()
    {
        using var db = CreateDb();
        var campaign = new ProspectingCampaign
        {
            Name = "Lojas", Category = "roupas", City = "Franca, SP", IntervalHours = 24,
        };
        db.Add(campaign); await db.SaveChangesAsync();
        var service = new ProspectingCampaignService(db);

        var first = await service.EnqueueAsync(campaign.Id);
        var second = await service.EnqueueAsync(campaign.Id);

        second!.Id.Should().Be(first!.Id);
        (await db.ProspectingCampaignRuns.CountAsync()).Should().Be(1);
        campaign.NextRunAt.Should().BeAfter(DateTime.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task SetActiveAsync_PausaEReativaCampanha()
    {
        using var db = CreateDb();
        var campaign = new ProspectingCampaign { Name = "Teste", Category = "bar", City = "Bauru, SP" };
        db.Add(campaign); await db.SaveChangesAsync();
        var service = new ProspectingCampaignService(db);

        (await service.SetActiveAsync(campaign.Id, false)).Should().BeTrue();
        campaign.Status.Should().Be(ProspectingCampaignStatus.Paused);
        (await service.SetActiveAsync(campaign.Id, true)).Should().BeTrue();
        campaign.Status.Should().Be(ProspectingCampaignStatus.Active);
    }

    [Fact]
    public async Task ReviewQueue_RetornaSomenteNovosEDeduplicaFonte()
    {
        using var db = CreateDb();
        var campaign = new ProspectingCampaign { Name = "Teste", Category = "bar", City = "Bauru, SP" };
        var firstSearch = new ProspectingSearch { Category = "bar", City = "Bauru, SP", CacheKey = "bauru|bar" };
        var secondSearch = new ProspectingSearch { Category = "todos", City = "Bauru, SP", CacheKey = "bauru|todos" };
        db.AddRange(campaign, firstSearch, secondSearch);
        db.ProspectingCampaignRuns.AddRange(
            new ProspectingCampaignRun { Campaign = campaign, SearchId = firstSearch.Id, Status = ProspectingCampaignRunStatus.Completed },
            new ProspectingCampaignRun { Campaign = campaign, SearchId = secondSearch.Id, Status = ProspectingCampaignRunStatus.Completed });
        db.ProspectCandidates.AddRange(
            new ProspectCandidate { Search = firstSearch, SourceId = "node/1", Name = "A", OpportunityScore = 80 },
            new ProspectCandidate { Search = secondSearch, SourceId = "node/1", Name = "A atualizado", OpportunityScore = 90, LastSeenAt = DateTime.UtcNow.AddMinutes(1) },
            new ProspectCandidate { Search = firstSearch, SourceId = "node/2", Name = "Já lead", Status = ProspectCandidateStatus.Lead });
        await db.SaveChangesAsync();

        var result = await new ProspectingCampaignService(db).ListReviewQueueAsync();

        result.Should().ContainSingle();
        result[0].Nome.Should().Be("A atualizado");
    }
}
