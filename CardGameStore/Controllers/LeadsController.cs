// =============================================================================
// LeadsController.cs — Captação pública de lead (CTA "quer sua loja com a
// sua cara?" da landing institucional). SEM autenticação.
//
// POST /api/leads → registra o interesse; a gestão (listar/atualizar) mora
// em PlatformController, PlatformOwnerOnly — este controller só recebe.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/leads")]
[AllowAnonymous]
[Produces("application/json")]
public class LeadsController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    private readonly ILogger<LeadsController> _logger;

    public LeadsController(CatalogDbContext catalog, ILogger<LeadsController> logger)
    {
        _catalog = catalog;
        _logger  = logger;
    }

    /// <summary>Registra o interesse de um lojista em contratar a plataforma.
    /// Público — sem login, é o formulário do site institucional.</summary>
    /// <param name="request">Nome e telefone (obrigatórios), e-mail e mensagem (opcionais).</param>
    [HttpPost]
    [EnableRateLimiting("public-lead")]
    public async Task<IActionResult> Create([FromBody] CreateLeadRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var lead = new Lead
        {
            Nome     = request.Nome.Trim(),
            Telefone = request.Telefone.Trim(),
            Email    = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Mensagem = string.IsNullOrWhiteSpace(request.Mensagem) ? null : request.Mensagem.Trim(),
            Campaign = Clean(request.Campaign),
            UtmSource = Clean(request.UtmSource),
            UtmMedium = Clean(request.UtmMedium),
            UtmCampaign = Clean(request.UtmCampaign),
            UtmTerm = Clean(request.UtmTerm),
            UtmContent = Clean(request.UtmContent),
            ReferrerUrl = Clean(request.ReferrerUrl),
            LandingPage = Clean(request.LandingPage),
            DataOriginDetails = "Contato enviado pelo próprio titular no formulário institucional",
            ProcessingPurpose = "Responder ao contato e realizar procedimentos para possível contratação da plataforma",
            LegalBasis = LeadLegalBasis.ProcedimentosPreContratuais,
            PrivacyNoticeVersion = request.PrivacyNoticeVersion.Trim(),
            PrivacyNoticeAcknowledgedAt = DateTime.UtcNow,
            RetentionReviewAt = DateTime.UtcNow.AddDays(180),
        };

        _catalog.Leads.Add(lead);
        await _catalog.SaveChangesAsync();

        _logger.LogInformation("Lead novo recebido: {Nome} ({Telefone})", lead.Nome, lead.Telefone);

        return Ok(new { Message = "Recebemos seu contato, vamos falar com você em breve." });
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
