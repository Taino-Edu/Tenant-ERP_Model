// =============================================================================
// ChampionshipServiceTests.cs — Testes unitários do ChampionshipService
// Executar: dotnet test  (na pasta tests/unit/CardGameStore.Tests)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Tests.Services;

public class ChampionshipServiceTests
{
    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    private static ChampionshipService CreateService(AppDbContext db) => new(db);

    private static Championship MakeChampionship(string name = "Torneio", ChampionshipStatus status = ChampionshipStatus.Inscricoes) =>
        new()
        {
            Id              = Guid.NewGuid(),
            Name            = name,
            Game            = "MTG",
            StartDate       = DateTime.UtcNow.AddDays(7),
            EntryFeeInCents = 1500,
            Status          = status,
            CreatedByAdminId = Guid.NewGuid(),
        };

    private static User MakeUser(string name = "Jogador") =>
        new()
        {
            Id           = Guid.NewGuid(),
            Name         = name,
            PasswordHash = "hash",
            Role         = UserRole.Customer,
            IsActive     = true,
        };

    // ── Criar campeonato ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_DevePersistirCampeonato()
    {
        var db      = CreateDb(nameof(Create_DevePersistirCampeonato));
        var service = CreateService(db);
        var ch      = MakeChampionship("Copa MTG");

        await service.CreateAsync(ch);

        (await db.Championships.FindAsync(ch.Id)).Should().NotBeNull();
    }

    // ── Listar próximos ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetUpcoming_DeveRetornarPlanejatoEInscricoes()
    {
        var db      = CreateDb(nameof(GetUpcoming_DeveRetornarPlanejatoEInscricoes));
        var service = CreateService(db);

        db.Championships.AddRange(
            MakeChampionship("Planejado",   ChampionshipStatus.Planejado),
            MakeChampionship("Inscricoes",  ChampionshipStatus.Inscricoes),
            MakeChampionship("EmAndamento", ChampionshipStatus.EmAndamento),
            MakeChampionship("Finalizado",  ChampionshipStatus.Finalizado)
        );
        await db.SaveChangesAsync();

        var result = await service.GetUpcomingAsync();

        result.Should().HaveCount(2);
        result.Select(c => c.Name).Should().Contain(new[] { "Planejado", "Inscricoes" });
    }

    [Fact]
    public async Task GetUpcoming_DeveOrdenarPorDataInicio()
    {
        var db      = CreateDb(nameof(GetUpcoming_DeveOrdenarPorDataInicio));
        var service = CreateService(db);

        var ch1 = MakeChampionship("Longe");
        ch1.StartDate = DateTime.UtcNow.AddDays(30);
        var ch2 = MakeChampionship("Perto");
        ch2.StartDate = DateTime.UtcNow.AddDays(3);

        db.Championships.AddRange(ch1, ch2);
        await db.SaveChangesAsync();

        var result = (await service.GetUpcomingAsync()).ToList();

        result[0].Name.Should().Be("Perto");
        result[1].Name.Should().Be("Longe");
    }

    // ── Atualizar status ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_DeveAlterarStatusDoCampeonato()
    {
        var db      = CreateDb(nameof(UpdateStatus_DeveAlterarStatusDoCampeonato));
        var service = CreateService(db);
        var ch      = MakeChampionship(status: ChampionshipStatus.Inscricoes);
        db.Championships.Add(ch);
        await db.SaveChangesAsync();

        await service.UpdateStatusAsync(ch.Id, ChampionshipStatus.EmAndamento);

        (await db.Championships.FindAsync(ch.Id))!.Status.Should().Be(ChampionshipStatus.EmAndamento);
    }

    [Fact]
    public async Task UpdateStatus_CampeonatoInexistente_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(UpdateStatus_CampeonatoInexistente_DeveLancarExcecao));
        var service = CreateService(db);

        var act = async () => await service.UpdateStatusAsync(Guid.NewGuid(), ChampionshipStatus.EmAndamento);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado*");
    }

    // ── Inscrever participante ────────────────────────────────────────────────

    [Fact]
    public async Task RegisterParticipant_DeveInscrevertUsuario()
    {
        var db      = CreateDb(nameof(RegisterParticipant_DeveInscrevertUsuario));
        var service = CreateService(db);
        var ch      = MakeChampionship();
        var user    = MakeUser("Herói");
        db.Championships.Add(ch);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var participant = await service.RegisterParticipantAsync(ch.Id, user.Id, "Deck Dragões");

        participant.Should().NotBeNull();
        participant.DeckName.Should().Be("Deck Dragões");
        participant.PlayerNumber.Should().Be(1);
    }

    [Fact]
    public async Task RegisterParticipant_NumerosSequenciais_DeveIncrementar()
    {
        var db      = CreateDb(nameof(RegisterParticipant_NumerosSequenciais_DeveIncrementar));
        var service = CreateService(db);
        var ch      = MakeChampionship();
        var u1      = MakeUser("P1");
        var u2      = MakeUser("P2");
        db.Championships.Add(ch);
        db.Users.AddRange(u1, u2);
        await db.SaveChangesAsync();

        var p1 = await service.RegisterParticipantAsync(ch.Id, u1.Id);
        var p2 = await service.RegisterParticipantAsync(ch.Id, u2.Id);

        p1.PlayerNumber.Should().Be(1);
        p2.PlayerNumber.Should().Be(2);
    }

    [Fact]
    public async Task RegisterParticipant_Duplicata_DeveLancarExcecao()
    {
        var db      = CreateDb(nameof(RegisterParticipant_Duplicata_DeveLancarExcecao));
        var service = CreateService(db);
        var ch      = MakeChampionship();
        var user    = MakeUser();
        db.Championships.Add(ch);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await service.RegisterParticipantAsync(ch.Id, user.Id);
        var act = async () => await service.RegisterParticipantAsync(ch.Id, user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já está inscrito*");
    }

    // ── Colocação final ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetPlacement_DeveRegistrarColocacaoDoJogador()
    {
        var db      = CreateDb(nameof(SetPlacement_DeveRegistrarColocacaoDoJogador));
        var service = CreateService(db);
        var ch      = MakeChampionship();
        var user    = MakeUser();
        db.Championships.Add(ch);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var participant = await service.RegisterParticipantAsync(ch.Id, user.Id);
        await service.SetPlacementAsync(participant.Id, 1);

        var atualizado = await db.ChampionshipParticipants.FindAsync(participant.Id);
        atualizado!.Placement.Should().Be(1);
    }

    // ── Listar participantes ──────────────────────────────────────────────────

    [Fact]
    public async Task GetParticipants_DeveRetornarOrdenadoPorNumero()
    {
        var db      = CreateDb(nameof(GetParticipants_DeveRetornarOrdenadoPorNumero));
        var service = CreateService(db);
        var ch      = MakeChampionship();
        var u1      = MakeUser("Bruno");
        var u2      = MakeUser("Alice");
        db.Championships.Add(ch);
        db.Users.AddRange(u1, u2);
        await db.SaveChangesAsync();

        await service.RegisterParticipantAsync(ch.Id, u1.Id); // #1
        await service.RegisterParticipantAsync(ch.Id, u2.Id); // #2

        var participants = (await service.GetParticipantsAsync(ch.Id)).ToList();

        participants[0].PlayerNumber.Should().Be(1);
        participants[1].PlayerNumber.Should().Be(2);
    }
}
