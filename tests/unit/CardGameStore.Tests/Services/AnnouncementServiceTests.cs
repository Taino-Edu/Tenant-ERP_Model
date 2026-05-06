using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class AnnouncementServiceTests
{
    private static AppDbContext MakeDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static AnnouncementService MakeSvc(AppDbContext db)
        => new(db, NullLogger<AnnouncementService>.Instance);

    // ── GetVisibleAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetVisible_RetornaApenasAtivos()
    {
        var db = MakeDb();
        db.Announcements.AddRange(
            new Announcement { Title = "Ativo",   IsActive = true },
            new Announcement { Title = "Inativo", IsActive = false }
        );
        await db.SaveChangesAsync();

        var result = (await MakeSvc(db).GetVisibleAsync()).ToList();

        Assert.Single(result);
        Assert.Equal("Ativo", result[0].Title);
    }

    [Fact]
    public async Task GetVisible_ExcluidoSeExpirado()
    {
        var db = MakeDb();
        db.Announcements.AddRange(
            new Announcement { Title = "Valido",   IsActive = true, ExpiresAt = DateTime.UtcNow.AddDays(1) },
            new Announcement { Title = "Expirado", IsActive = true, ExpiresAt = DateTime.UtcNow.AddDays(-1) }
        );
        await db.SaveChangesAsync();

        var result = (await MakeSvc(db).GetVisibleAsync()).ToList();

        Assert.Single(result);
        Assert.Equal("Valido", result[0].Title);
    }

    [Fact]
    public async Task GetVisible_SemExpiracaoSempreVisivel()
    {
        var db = MakeDb();
        db.Announcements.Add(new Announcement { Title = "Permanente", IsActive = true, ExpiresAt = null });
        await db.SaveChangesAsync();

        var result = await MakeSvc(db).GetVisibleAsync();

        Assert.Single(result);
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersisteCamposCorretamente()
    {
        var db      = MakeDb();
        var adminId = Guid.NewGuid();
        var request = new CreateAnnouncementRequest
        {
            Title     = "Torneio Pokémon",
            Body      = "Sábado às 14h",
            Type      = AnnouncementType.Aviso,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        var dto = await MakeSvc(db).CreateAsync(request, adminId);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Torneio Pokémon", dto.Title);
        Assert.Equal("Aviso", dto.Type);
        Assert.NotNull(dto.ExpiresAt);

        var saved = await db.Announcements.FindAsync(dto.Id);
        Assert.NotNull(saved);
        Assert.Equal(adminId, saved!.CreatedByAdminId);
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_AlteraCamposEMantemOutros()
    {
        var db = MakeDb();
        var ann = new Announcement { Title = "Antigo", Body = "Texto", IsActive = true };
        db.Announcements.Add(ann);
        await db.SaveChangesAsync();

        var svc = MakeSvc(db);
        var dto = await svc.UpdateAsync(ann.Id, new UpdateAnnouncementRequest
        {
            Title    = "Novo",
            IsActive = false,
        });

        Assert.Equal("Novo",  dto.Title);
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task Update_LancaExcecaoSeNaoEncontrado()
    {
        var db  = MakeDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MakeSvc(db).UpdateAsync(Guid.NewGuid(), new UpdateAnnouncementRequest { Title = "X" }));
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemoveDosBanco()
    {
        var db = MakeDb();
        var ann = new Announcement { Title = "A remover", IsActive = true };
        db.Announcements.Add(ann);
        await db.SaveChangesAsync();

        await MakeSvc(db).DeleteAsync(ann.Id);

        Assert.Equal(0, await db.Announcements.CountAsync());
    }

    [Fact]
    public async Task Delete_LancaExcecaoSeNaoEncontrado()
    {
        var db = MakeDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MakeSvc(db).DeleteAsync(Guid.NewGuid()));
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_RetornaTodosInclusiveInativos()
    {
        var db = MakeDb();
        db.Announcements.AddRange(
            new Announcement { Title = "A", IsActive = true  },
            new Announcement { Title = "B", IsActive = false }
        );
        await db.SaveChangesAsync();

        var result = await MakeSvc(db).GetAllAsync();

        Assert.Equal(2, result.Count());
    }
}
