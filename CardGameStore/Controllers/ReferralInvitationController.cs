using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/referral-invitations")]
public class ReferralInvitationController : ControllerBase
{
    private const int CodeLifetimeMinutes = 10;
    private const int MaxCodeAttempts = 5;
    private const int MaxCodeSends = 5;
    private readonly CatalogDbContext _catalog;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _email;
    private readonly IReferralContractPdfService _pdf;

    public ReferralInvitationController(CatalogDbContext catalog, IConfiguration configuration,
        IEmailService email, IReferralContractPdfService pdf)
    {
        _catalog = catalog;
        _configuration = configuration;
        _email = email;
        _pdf = pdf;
    }

    [HttpGet("{token}")]
    public async Task<ActionResult<ReferralInvitationDto>> Get(string token)
    {
        var invitation = await FindByToken(token, tracking: false);
        if (invitation is null || invitation.RevokedAt.HasValue || (!invitation.AcceptedAt.HasValue && invitation.ExpiresAt <= DateTime.UtcNow))
            return NotFound(new { Message = "Convite inválido, expirado ou revogado." });
        return Ok(PublicDto(invitation));
    }

    [HttpPost("{token}/request-signature")]
    public async Task<IActionResult> RequestSignature(string token, [FromBody] AcceptReferralInvitationRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var validation = ValidateAcceptance(request);
        if (validation is not null) return BadRequest(new { Message = validation });

        var invitation = await FindPending(token);
        if (invitation is null) return NotFound(new { Message = "Convite inválido, expirado, revogado ou já utilizado." });
        var now = DateTime.UtcNow;
        if (invitation.SignatureCodeSentAt > now.AddSeconds(-60))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { Message = "Aguarde um minuto antes de solicitar outro código." });
        if (invitation.SignatureCodeSendCount >= MaxCodeSends)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { Message = "Limite de códigos atingido. Solicite um novo convite à 3ESYSTEN." });

        var normalized = Normalize(request);
        if (invitation.Email is not null && !invitation.Email.Equals(normalized.Email, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { Message = "Use o mesmo e-mail para o qual o convite foi enviado." });
        if (await _catalog.ReferralPartners.AnyAsync(p => p.Document == normalized.Document))
            return Conflict(new { Message = "Já existe um parceiro cadastrado com este CPF/CNPJ." });

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        invitation.PendingAcceptanceJson = JsonSerializer.Serialize(normalized);
        invitation.SignatureCodeHash = HashSignatureCode(token, code);
        invitation.SignatureCodeSentAt = now;
        invitation.SignatureCodeExpiresAt = now.AddMinutes(CodeLifetimeMinutes);
        invitation.SignatureCodeAttempts = 0;
        invitation.SignatureCodeSendCount++;
        await _catalog.SaveChangesAsync();

        try
        {
            await _email.SendReferralSignatureCodeAsync(normalized.Email, normalized.Name, code, invitation.SignatureCodeExpiresAt.Value);
        }
        catch
        {
            invitation.SignatureCodeHash = null;
            invitation.SignatureCodeExpiresAt = null;
            invitation.SignatureCodeSentAt = null;
            invitation.SignatureCodeSendCount--;
            await _catalog.SaveChangesAsync();
            throw;
        }

        return Ok(new
        {
            Message = "Enviamos um código de 6 dígitos para confirmar o aceite.",
            Email = MaskEmail(normalized.Email),
            ExpiresAt = invitation.SignatureCodeExpiresAt,
        });
    }

    [HttpPost("{token}/confirm-signature")]
    public async Task<IActionResult> ConfirmSignature(string token, [FromBody] ConfirmReferralSignatureRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await using var transaction = await _catalog.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invitation = await FindPending(token);
        if (invitation is null) return NotFound(new { Message = "Convite inválido, expirado, revogado ou já utilizado." });
        var now = DateTime.UtcNow;
        if (invitation.SignatureCodeHash is null || invitation.SignatureCodeExpiresAt <= now || invitation.PendingAcceptanceJson is null)
            return BadRequest(new { Message = "O código expirou. Solicite um novo código." });
        if (invitation.SignatureCodeAttempts >= MaxCodeAttempts)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { Message = "Código bloqueado após muitas tentativas. Solicite um novo código." });

        if (!HashesEqual(invitation.SignatureCodeHash, HashSignatureCode(token, request.Code)))
        {
            invitation.SignatureCodeAttempts++;
            await _catalog.SaveChangesAsync();
            await transaction.CommitAsync();
            return BadRequest(new { Message = invitation.SignatureCodeAttempts >= MaxCodeAttempts
                ? "Código bloqueado após muitas tentativas. Solicite um novo código."
                : "Código incorreto." });
        }

        var acceptance = JsonSerializer.Deserialize<PendingAcceptance>(invitation.PendingAcceptanceJson);
        if (acceptance is null) return BadRequest(new { Message = "Não foi possível recuperar os dados do aceite. Solicite um novo código." });
        if (await _catalog.ReferralPartners.AnyAsync(p => p.Document == acceptance.Document))
            return Conflict(new { Message = "Já existe um parceiro cadastrado com este CPF/CNPJ." });

        var acceptedAt = DateTime.UtcNow;
        var evidenceId = Guid.NewGuid().ToString();
        var ipHash = HashIp();
        var userAgent = Truncate(Request.Headers.UserAgent.ToString(), 500);
        var evidencePayload = string.Join('\n', invitation.Id, evidenceId, acceptance.Name, acceptance.Email,
            acceptance.Document, invitation.ContractVersion, invitation.ContractText, acceptedAt.ToString("O"), ipHash, userAgent);
        var evidenceHash = Sha256(evidencePayload);
        var pdfBytes = _pdf.Generate(new ReferralContractPdfData(
            acceptance.Name, acceptance.PersonType, acceptance.Document, acceptance.Email, acceptance.Phone,
            invitation.PartnerKind, invitation.SetupCommissionPercent, invitation.MonthlyCommissionPercent,
            invitation.PaymentGraceDays, invitation.ContractVersion, invitation.ContractText, acceptedAt,
            evidenceId, evidenceHash, ipHash, userAgent));

        var partner = new ReferralPartner
        {
            Name = acceptance.Name, Email = acceptance.Email, Document = acceptance.Document,
            Phone = acceptance.Phone, PixKey = acceptance.PixKey, PersonType = acceptance.PersonType,
            PartnerKind = invitation.PartnerKind, ProfessionalRegistration = acceptance.ProfessionalRegistration,
            FiscalDocumentType = acceptance.PersonType == "PJ" ? "NFS-e" : "RPA",
            SetupCommissionPercent = invitation.SetupCommissionPercent,
            MonthlyCommissionPercent = invitation.MonthlyCommissionPercent,
            PaymentGraceDays = invitation.PaymentGraceDays,
            ContractVersion = invitation.ContractVersion, ContractText = invitation.ContractText,
            ContractAcceptedAt = acceptedAt, ContractEmailVerifiedAt = acceptedAt,
            ContractAcceptedIpHash = ipHash, ContractAcceptedUserAgent = userAgent,
            ContractEvidenceId = evidenceId, ContractEvidenceSha256 = evidenceHash,
            ContractPdf = pdfBytes, ContractPdfSha256 = Convert.ToHexString(SHA256.HashData(pdfBytes)),
            Active = true,
        };
        _catalog.ReferralPartners.Add(partner);
        invitation.AcceptedAt = acceptedAt;
        invitation.AcceptedPartnerId = partner.Id;
        invitation.SignatureCodeHash = null;
        invitation.SignatureCodeExpiresAt = null;
        invitation.PendingAcceptanceJson = null;
        await _catalog.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { partner.Id, Message = "Parceria assinada e documento gerado com sucesso.", SignedDocumentAvailable = true });
    }

    [HttpGet("{token}/signed-document")]
    public async Task<IActionResult> DownloadSignedDocument(string token)
    {
        var invitation = await FindByToken(token, tracking: false);
        if (invitation?.AcceptedPartnerId is null || !invitation.AcceptedAt.HasValue)
            return NotFound(new { Message = "Documento assinado não encontrado." });
        var partner = await _catalog.ReferralPartners.AsNoTracking().SingleOrDefaultAsync(p => p.Id == invitation.AcceptedPartnerId);
        if (partner?.ContractPdf is null) return NotFound(new { Message = "Documento assinado não encontrado." });
        return File(partner.ContractPdf, "application/pdf", $"termo-parceria-{partner.Id:N}.pdf");
    }

    private async Task<ReferralPartnerInvitation?> FindPending(string token)
    {
        var invitation = await FindByToken(token, tracking: true);
        var now = DateTime.UtcNow;
        return invitation is not null && invitation.AcceptedAt is null && invitation.RevokedAt is null && invitation.ExpiresAt > now
            ? invitation : null;
    }

    private async Task<ReferralPartnerInvitation?> FindByToken(string token, bool tracking)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200) return null;
        var hash = Sha256(token);
        var query = tracking ? _catalog.ReferralPartnerInvitations : _catalog.ReferralPartnerInvitations.AsNoTracking();
        return await query.FirstOrDefaultAsync(i => i.TokenHash == hash);
    }

    private ReferralInvitationDto PublicDto(ReferralPartnerInvitation i) => new()
    {
        Id = i.Id, Name = i.Name, Email = i.Email, PartnerKind = i.PartnerKind,
        SetupCommissionPercent = i.SetupCommissionPercent, MonthlyCommissionPercent = i.MonthlyCommissionPercent,
        PaymentGraceDays = i.PaymentGraceDays, ContractVersion = i.ContractVersion,
        ContractText = i.ContractText, ExpiresAt = i.ExpiresAt, AcceptedAt = i.AcceptedAt,
        SignatureCodeSentAt = i.SignatureCodeSentAt,
        SignedDocumentAvailable = i.AcceptedAt.HasValue && i.AcceptedPartnerId.HasValue,
        Status = i.AcceptedAt.HasValue ? "Aceito" : "Pendente",
    };

    private string HashIp()
    {
        var salt = _configuration["IP_HASH_SALT"] ?? _configuration["Security:IpHashSalt"] ?? string.Empty;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return Sha256($"{salt}:{ip}");
    }

    private static string? ValidateAcceptance(AcceptReferralInvitationRequest request)
    {
        if (!request.AcceptedTerms) return "É necessário aceitar o regulamento.";
        var digits = Digits(request.Document);
        if ((request.PersonType == "PF" && digits.Length != 11) || (request.PersonType == "PJ" && digits.Length != 14))
            return "Informe um CPF ou CNPJ compatível com o tipo de pessoa.";
        return null;
    }

    private static PendingAcceptance Normalize(AcceptReferralInvitationRequest r) => new(
        r.Name.Trim(), r.Email.Trim().ToLowerInvariant(), Digits(r.Document), Clean(r.Phone), Clean(r.PixKey),
        r.PersonType, Clean(r.ProfessionalRegistration));

    private static string HashSignatureCode(string token, string code) => Sha256($"{token}:{code}");
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool HashesEqual(string left, string right) => CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));
    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
    private static string MaskEmail(string email)
    {
        var parts = email.Split('@', 2);
        var visible = parts[0].Length <= 2 ? parts[0][..1] : parts[0][..2];
        return $"{visible}***@{parts[1]}";
    }

    private sealed record PendingAcceptance(string Name, string Email, string Document, string? Phone,
        string? PixKey, string PersonType, string? ProfessionalRegistration);
}
