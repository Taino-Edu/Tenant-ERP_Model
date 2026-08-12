using System.Data;
using System.Security.Cryptography;
using System.Text;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/referral-invitations")]
public class ReferralInvitationController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    private readonly IConfiguration _configuration;

    public ReferralInvitationController(CatalogDbContext catalog, IConfiguration configuration)
    {
        _catalog = catalog;
        _configuration = configuration;
    }

    [HttpGet("{token}")]
    public async Task<ActionResult<ReferralInvitationDto>> Get(string token)
    {
        var invitation = await FindValid(token);
        if (invitation is null) return NotFound(new { Message = "Convite inválido, expirado, revogado ou já utilizado." });
        return Ok(PublicDto(invitation));
    }

    [HttpPost("{token}/accept")]
    public async Task<IActionResult> Accept(string token, [FromBody] AcceptReferralInvitationRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!request.AcceptedTerms) return BadRequest(new { Message = "É necessário aceitar o regulamento." });
        var digits = new string(request.Document.Where(char.IsDigit).ToArray());
        if ((request.PersonType == "PF" && digits.Length != 11) || (request.PersonType == "PJ" && digits.Length != 14))
            return BadRequest(new { Message = "Informe um CPF ou CNPJ compatível com o tipo de pessoa." });

        await using var transaction = await _catalog.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invitation = await FindValid(token);
        if (invitation is null) return NotFound(new { Message = "Convite inválido, expirado, revogado ou já utilizado." });
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (invitation.Email is not null && !invitation.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { Message = "Use o mesmo e-mail para o qual o convite foi enviado." });
        if (await _catalog.ReferralPartners.AnyAsync(p => p.Document == digits))
            return Conflict(new { Message = "Já existe um parceiro cadastrado com este CPF/CNPJ." });

        var partner = new ReferralPartner
        {
            Name = request.Name.Trim(), Email = normalizedEmail, Document = digits,
            Phone = Clean(request.Phone), PixKey = Clean(request.PixKey), PersonType = request.PersonType,
            PartnerKind = invitation.PartnerKind, ProfessionalRegistration = Clean(request.ProfessionalRegistration),
            FiscalDocumentType = request.PersonType == "PJ" ? "NFS-e" : "RPA",
            SetupCommissionPercent = invitation.SetupCommissionPercent,
            MonthlyCommissionPercent = invitation.MonthlyCommissionPercent,
            PaymentGraceDays = invitation.PaymentGraceDays,
            ContractVersion = invitation.ContractVersion, ContractText = invitation.ContractText,
            ContractAcceptedAt = DateTime.UtcNow, ContractAcceptedIpHash = HashIp(),
            ContractAcceptedUserAgent = Truncate(Request.Headers.UserAgent.ToString(), 500), Active = true,
        };
        _catalog.ReferralPartners.Add(partner);
        invitation.AcceptedAt = partner.ContractAcceptedAt;
        invitation.AcceptedPartnerId = partner.Id;
        await _catalog.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { partner.Id, Message = "Cadastro e aceite registrados com sucesso." });
    }

    private async Task<ReferralPartnerInvitation?> FindValid(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200) return null;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = DateTime.UtcNow;
        return await _catalog.ReferralPartnerInvitations.FirstOrDefaultAsync(i =>
            i.TokenHash == hash && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now);
    }

    private ReferralInvitationDto PublicDto(ReferralPartnerInvitation i) => new()
    {
        Id = i.Id, Name = i.Name, Email = i.Email, PartnerKind = i.PartnerKind,
        SetupCommissionPercent = i.SetupCommissionPercent, MonthlyCommissionPercent = i.MonthlyCommissionPercent,
        PaymentGraceDays = i.PaymentGraceDays, ContractVersion = i.ContractVersion,
        ContractText = i.ContractText, ExpiresAt = i.ExpiresAt, Status = "Pendente",
    };

    private string HashIp()
    {
        var salt = _configuration["IP_HASH_SALT"] ?? _configuration["Security:IpHashSalt"] ?? string.Empty;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{ip}")));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
