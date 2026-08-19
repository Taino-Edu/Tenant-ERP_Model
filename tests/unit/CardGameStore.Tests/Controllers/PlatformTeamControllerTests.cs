using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CardGameStore.Controllers;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Security;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Controllers;

/// <summary>
/// As travas do PATCH /api/platform/team/{id}. Todas existem pelo mesmo motivo:
/// a equipe da plataforma administra a si mesma, então é aqui que alguém
/// consegue se trancar do lado de fora ou alcançar a conta raiz.
/// </summary>
public sealed class PlatformTeamControllerTests
{
    [Fact]
    public async Task Update_RecusaTrocarOProprioPerfilDeAcesso()
    {
        await using var db = CreateDb();
        var socio = await SeedOwner(db, PlatformAccessProfiles.Partner);
        var controller = ControllerFor(db, socio.Id);

        var result = await controller.Update(socio.Id, new UpdatePlatformOwnerRequest
        {
            Name = socio.Name, ProfileKey = PlatformAccessProfiles.Auditor, IsActive = true,
        }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.Users.SingleAsync(u => u.Id == socio.Id)).PlatformAccessProfile
            .Should().Be(PlatformAccessProfiles.Partner);
    }

    [Fact]
    public async Task Update_PermiteEditarOProprioNomeSemTrocarDePerfil()
    {
        await using var db = CreateDb();
        var socio = await SeedOwner(db, PlatformAccessProfiles.Partner);
        var controller = ControllerFor(db, socio.Id);

        var result = await controller.Update(socio.Id, new UpdatePlatformOwnerRequest
        {
            Name = "Nome Corrigido", ProfileKey = PlatformAccessProfiles.Partner, IsActive = true,
        }, default);

        result.Should().BeOfType<OkObjectResult>();
        (await db.Users.SingleAsync(u => u.Id == socio.Id)).Name.Should().Be("Nome Corrigido");
    }

    [Fact]
    public async Task Update_PermiteTrocarOPerfilDeOutroIntegrante()
    {
        await using var db = CreateDb();
        var socio = await SeedOwner(db, PlatformAccessProfiles.Partner);
        var colega = await SeedOwner(db, PlatformAccessProfiles.Auditor, "colega@octus.com");
        var controller = ControllerFor(db, socio.Id);

        var result = await controller.Update(colega.Id, new UpdatePlatformOwnerRequest
        {
            Name = colega.Name, ProfileKey = PlatformAccessProfiles.Commercial, IsActive = true,
        }, default);

        result.Should().BeOfType<OkObjectResult>();
        (await db.Users.SingleAsync(u => u.Id == colega.Id)).PlatformAccessProfile
            .Should().Be(PlatformAccessProfiles.Commercial);
    }

    [Fact]
    public async Task Update_NaoAlcancaOProprietarioPrincipal()
    {
        await using var db = CreateDb();
        var socio = await SeedOwner(db, PlatformAccessProfiles.Partner);
        var raiz = await SeedOwner(db, PlatformAccessProfiles.Primary, "raiz@octus.com", primary: true);
        var controller = ControllerFor(db, socio.Id);

        var result = await controller.Update(raiz.Id, new UpdatePlatformOwnerRequest
        {
            Name = raiz.Name, ProfileKey = PlatformAccessProfiles.Auditor, IsActive = false,
        }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
        var atual = await db.Users.SingleAsync(u => u.Id == raiz.Id);
        atual.IsActive.Should().BeTrue();
        atual.PlatformAccessProfile.Should().Be(PlatformAccessProfiles.Primary);
    }

    [Fact]
    public async Task Update_RecusaDesativarAPropriaConta()
    {
        await using var db = CreateDb();
        var socio = await SeedOwner(db, PlatformAccessProfiles.Partner);
        var controller = ControllerFor(db, socio.Id);

        var result = await controller.Update(socio.Id, new UpdatePlatformOwnerRequest
        {
            Name = socio.Name, ProfileKey = PlatformAccessProfiles.Partner, IsActive = false,
        }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.Users.SingleAsync(u => u.Id == socio.Id)).IsActive.Should().BeTrue();
    }

    private static AppDbContext CreateDb() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"platform_team_{Guid.NewGuid():N}").Options);

    private static async Task<User> SeedOwner(
        AppDbContext db, string profileKey, string email = "socio@octus.com", bool primary = false)
    {
        var profile = PlatformAccessProfiles.All[profileKey];
        var owner = new User
        {
            Name = "Integrante", Email = email, PasswordHash = "hash",
            Role = UserRole.PlatformOwner,
            PlatformAccessProfile = profileKey,
            PlatformPermissionsJson = PlatformAccessProfiles.Serialize(profile.Permissions),
            IsPlatformPrimaryOwner = primary, SessionVersion = 1, IsActive = true,
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        return owner;
    }

    private static PlatformTeamController ControllerFor(AppDbContext db, Guid currentUserId)
    {
        var controller = new PlatformTeamController(
            db,
            new Mock<IEmailService>().Object,
            NullLogger<PlatformTeamController>.Instance);

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtRegisteredClaimNames.Sub, currentUserId.ToString())], "test")),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }
}
