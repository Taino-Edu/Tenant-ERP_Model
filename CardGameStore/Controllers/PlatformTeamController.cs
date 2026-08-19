using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Security;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/platform/team")]
[Authorize(Policy = "PlatformOwnerOnly")]
[RequirePlatformPermission(PlatformPermission.Team)]
public sealed class PlatformTeamController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<PlatformTeamController> _logger;

    public PlatformTeamController(AppDbContext db, IEmailService email, ILogger<PlatformTeamController> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlatformTeamMemberDto>>> List(CancellationToken cancellationToken)
    {
        var owners = await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.PlatformOwner)
            .OrderByDescending(u => u.IsPlatformPrimaryOwner)
            .ThenBy(u => u.Name)
            .ToListAsync(cancellationToken);
        return Ok(owners.Select(ToDto));
    }

    [HttpGet("profiles")]
    public ActionResult<IReadOnlyList<PlatformAccessProfileDto>> Profiles() => Ok(
        PlatformAccessProfiles.All.Values
            .Where(profile => profile.Selectable)
            .Select(profile => new PlatformAccessProfileDto
            {
                Key = profile.Key,
                Name = profile.Name,
                Description = profile.Description,
                Permissions = profile.Permissions,
            }));

    [HttpPost("invitations")]
    public async Task<IActionResult> Invite([FromBody] InvitePlatformOwnerRequest request, CancellationToken cancellationToken)
    {
        PlatformProfileDefinition profile;
        try { profile = PlatformAccessProfiles.GetRequired(request.ProfileKey); }
        catch (ArgumentException exception) { return BadRequest(new { Message = exception.Message }); }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            return Conflict(new { Message = "Já existe uma conta com este e-mail." });

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var owner = new User
        {
            Name = request.Name.Trim(), Email = email, PasswordHash = null,
            Role = UserRole.PlatformOwner,
            PlatformAccessProfile = profile.Key,
            PlatformPermissionsJson = PlatformAccessProfiles.Serialize(profile.Permissions),
            IsPlatformPrimaryOwner = false, SessionVersion = 1, IsActive = true,
            PasswordResetToken = AuthService.HashOpaqueToken(rawToken),
            PasswordResetTokenExpiry = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(owner);
        await _db.SaveChangesAsync(cancellationToken);

        var emailSent = true;
        try { await _email.SendPlatformOwnerInviteAsync(email, owner.Name, profile.Name, rawToken); }
        catch (Exception exception)
        {
            emailSent = false;
            _logger.LogError(exception, "Conta da equipe criada, mas convite não enviado para {OwnerId}", owner.Id);
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            Member = ToDto(owner), EmailSent = emailSent,
            Message = emailSent ? "Convite enviado." : "Conta criada, mas o e-mail não foi enviado. Use reenviar convite.",
        });
    }

    [HttpPost("{id:guid}/resend-invite")]
    public async Task<IActionResult> ResendInvite(Guid id, CancellationToken cancellationToken)
    {
        var owner = await _db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Role == UserRole.PlatformOwner, cancellationToken);
        if (owner is null) return NotFound();
        if (owner.IsPlatformPrimaryOwner || owner.PasswordHash is not null)
            return BadRequest(new { Message = "Esta conta já concluiu o cadastro." });

        PlatformProfileDefinition profile;
        try { profile = PlatformAccessProfiles.GetRequired(owner.PlatformAccessProfile ?? string.Empty); }
        catch (ArgumentException exception) { return BadRequest(new { Message = exception.Message }); }

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        owner.PasswordResetToken = AuthService.HashOpaqueToken(rawToken);
        owner.PasswordResetTokenExpiry = DateTime.UtcNow.AddDays(7);
        owner.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _email.SendPlatformOwnerInviteAsync(owner.Email!, owner.Name, profile.Name, rawToken);
        return Ok(new { Message = "Convite reenviado." });
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlatformOwnerRequest request, CancellationToken cancellationToken)
    {
        var owner = await _db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Role == UserRole.PlatformOwner, cancellationToken);
        if (owner is null) return NotFound();
        if (owner.IsPlatformPrimaryOwner)
            return BadRequest(new { Message = "O proprietário principal não pode ser alterado por esta tela." });
        if (owner.Id == CurrentUserId() && !request.IsActive)
            return BadRequest(new { Message = "Você não pode desativar a própria conta." });

        PlatformProfileDefinition profile;
        try { profile = PlatformAccessProfiles.GetRequired(request.ProfileKey); }
        catch (ArgumentException exception) { return BadRequest(new { Message = exception.Message }); }

        owner.Name = request.Name.Trim();
        owner.PlatformAccessProfile = profile.Key;
        owner.PlatformPermissionsJson = PlatformAccessProfiles.Serialize(profile.Permissions);
        owner.IsActive = request.IsActive;
        owner.RefreshToken = null;
        owner.RefreshTokenExpiry = null;
        owner.SessionVersion++;
        owner.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(owner));
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    private static PlatformTeamMemberDto ToDto(User owner)
    {
        var profileKey = owner.IsPlatformPrimaryOwner ? PlatformAccessProfiles.Primary : owner.PlatformAccessProfile ?? PlatformAccessProfiles.Auditor;
        PlatformAccessProfiles.All.TryGetValue(profileKey, out var profile);
        return new PlatformTeamMemberDto
        {
            Id = owner.Id, Name = owner.Name, Email = owner.Email ?? string.Empty,
            ProfileKey = profileKey, ProfileName = profile?.Name ?? profileKey,
            Permissions = PlatformAccessProfiles.EffectivePermissions(
                owner.IsPlatformPrimaryOwner, profileKey, owner.PlatformPermissionsJson),
            IsPrimaryOwner = owner.IsPlatformPrimaryOwner, IsActive = owner.IsActive,
            InvitationPending = owner.PasswordHash is null, LastLoginAt = owner.LastLoginAt, CreatedAt = owner.CreatedAt,
        };
    }
}
