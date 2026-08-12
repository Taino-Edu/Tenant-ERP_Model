using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/platform/crm")]
[Authorize(Policy = "PlatformOwnerOnly")]
[RequirePlatformPermission(PlatformPermission.Leads)]
public sealed class PlatformCrmController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    private readonly AppDbContext _users;

    public PlatformCrmController(CatalogDbContext catalog, AppDbContext users)
    {
        _catalog = catalog;
        _users = users;
    }

    [HttpGet("assignees")]
    public async Task<ActionResult<IReadOnlyList<CrmAssigneeDto>>> ListAssignees(CancellationToken ct)
    {
        var owners = await _users.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.PlatformOwner && u.IsActive)
            .OrderBy(u => u.Name).ToListAsync(ct);
        return Ok(owners.Where(CanManageLeads).Select(u => new CrmAssigneeDto
        {
            Id = u.Id, Name = u.Name, Email = u.Email ?? string.Empty,
        }));
    }

    [HttpGet("leads/{leadId:guid}")]
    public async Task<ActionResult<CrmWorkspaceDto>> GetWorkspace(Guid leadId, CancellationToken ct)
    {
        if (!await _catalog.Leads.AsNoTracking().AnyAsync(l => l.Id == leadId, ct))
            return NotFound();

        var opportunity = await _catalog.CrmOpportunities.AsNoTracking()
            .FirstOrDefaultAsync(o => o.LeadId == leadId, ct);
        var activities = await _catalog.CrmActivities.AsNoTracking()
            .Where(a => a.LeadId == leadId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .ToListAsync(ct);
        return Ok(new CrmWorkspaceDto
        {
            Opportunity = opportunity is null ? null : ToDto(opportunity),
            Activities = activities.Select(ToDto).ToList(),
        });
    }

    [HttpGet("tasks")]
    public async Task<ActionResult<IReadOnlyList<CrmTaskDto>>> ListOpenTasks(CancellationToken ct)
    {
        var tasks = await _catalog.CrmActivities.AsNoTracking()
            .Include(a => a.Lead)
            .Where(a => a.Type == CrmActivityType.Tarefa && a.CompletedAt == null)
            .OrderBy(a => a.DueAt == null)
            .ThenBy(a => a.DueAt)
            .ThenByDescending(a => a.CreatedAt)
            .Take(500)
            .ToListAsync(ct);
        var leadIds = tasks.Select(a => a.LeadId).Distinct().ToList();
        var opportunities = await _catalog.CrmOpportunities.AsNoTracking()
            .Where(o => leadIds.Contains(o.LeadId))
            .ToDictionaryAsync(o => o.LeadId, ct);
        return Ok(tasks.Select(a => new CrmTaskDto
        {
            Id = a.Id, LeadId = a.LeadId, OpportunityId = a.OpportunityId,
            Type = a.Type.ToString(), Title = a.Title, Description = a.Description,
            DueAt = a.DueAt, CompletedAt = a.CompletedAt, Outcome = a.Outcome,
            CreatedByUserId = a.CreatedByUserId, CreatedByUserName = a.CreatedByUserName,
            CreatedAt = a.CreatedAt, LeadName = a.Lead.Nome,
            AssignedUserId = opportunities.GetValueOrDefault(a.LeadId)?.AssignedUserId,
            AssignedUserName = opportunities.GetValueOrDefault(a.LeadId)?.AssignedUserName,
        }).ToList());
    }

    [HttpPut("leads/{leadId:guid}/opportunity")]
    public async Task<ActionResult<CrmOpportunityDto>> SaveOpportunity(
        Guid leadId, [FromBody] SaveCrmOpportunityRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!Enum.TryParse<CrmOpportunityStage>(request.Stage, true, out var stage))
            return BadRequest(new { Message = "Etapa comercial inválida." });
        if (stage == CrmOpportunityStage.Perdido && string.IsNullOrWhiteSpace(request.LostReason))
            return BadRequest(new { Message = "Informe o motivo da perda." });
        if (!await _catalog.Leads.AnyAsync(l => l.Id == leadId, ct)) return NotFound();

        User? assignee = null;
        if (request.AssignedUserId.HasValue)
        {
            assignee = await _users.Users.AsNoTracking().FirstOrDefaultAsync(u =>
                u.Id == request.AssignedUserId && u.Role == UserRole.PlatformOwner && u.IsActive, ct);
            if (assignee is null || !CanManageLeads(assignee))
                return BadRequest(new { Message = "Responsável comercial inválido ou inativo." });
        }

        var now = DateTime.UtcNow;
        var opportunity = await _catalog.CrmOpportunities
            .FirstOrDefaultAsync(o => o.LeadId == leadId, ct);
        var isNew = opportunity is null;
        opportunity ??= new CrmOpportunity { LeadId = leadId, CreatedAt = now };
        var previousStage = opportunity.Stage;
        var previousOwner = opportunity.AssignedUserName;

        opportunity.Stage = stage;
        opportunity.Probability = request.Probability;
        opportunity.Value = request.Value;
        opportunity.ExpectedCloseDate = request.ExpectedCloseDate?.ToUniversalTime();
        opportunity.AssignedUserId = assignee?.Id;
        opportunity.AssignedUserName = assignee?.Name;
        opportunity.LostReason = stage == CrmOpportunityStage.Perdido ? request.LostReason?.Trim() : null;
        opportunity.ClosedAt = stage is CrmOpportunityStage.Ganho or CrmOpportunityStage.Perdido
            ? opportunity.ClosedAt ?? now : null;
        opportunity.UpdatedAt = now;
        if (isNew) _catalog.CrmOpportunities.Add(opportunity);

        if (isNew || previousStage != stage)
            AddSystemActivity(leadId, opportunity, CrmActivityType.MudancaEtapa,
                isNew ? $"Oportunidade criada em {stage}" : $"Etapa alterada: {previousStage} → {stage}", now);
        if (!string.Equals(previousOwner, opportunity.AssignedUserName, StringComparison.Ordinal))
            AddSystemActivity(leadId, opportunity, CrmActivityType.MudancaResponsavel,
                opportunity.AssignedUserName is null ? "Responsável removido" : $"Responsável definido: {opportunity.AssignedUserName}", now);

        await _catalog.SaveChangesAsync(ct);
        return Ok(ToDto(opportunity));
    }

    [HttpPost("leads/{leadId:guid}/activities")]
    public async Task<ActionResult<CrmActivityDto>> CreateActivity(
        Guid leadId, [FromBody] CreateCrmActivityRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!Enum.TryParse<CrmActivityType>(request.Type, true, out var type) ||
            type is CrmActivityType.MudancaEtapa or CrmActivityType.MudancaResponsavel)
            return BadRequest(new { Message = "Tipo de atividade inválido." });
        var lead = await _catalog.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return NotFound();
        if (type is CrmActivityType.Ligacao or CrmActivityType.WhatsApp or CrmActivityType.Email)
        {
            if (lead.OpposedAt.HasValue)
                return Conflict(new { Message = "Contato bloqueado: o titular registrou oposição." });
            if (lead.LegalBasis == LeadLegalBasis.LegitimoInteresse && !lead.LegitimateInterestAssessedAt.HasValue)
                return Conflict(new { Message = "Valide o teste de legítimo interesse antes de registrar contato ativo." });
            if (lead.LegalBasis is LeadLegalBasis.NaoDefinida or LeadLegalBasis.ObrigacaoLegal)
                return Conflict(new { Message = "Defina uma base legal compatível antes de registrar contato ativo." });
        }

        var actor = await CurrentActorAsync(ct);
        var opportunityId = await _catalog.CrmOpportunities.AsNoTracking()
            .Where(o => o.LeadId == leadId).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(ct);
        var activity = new CrmActivity
        {
            LeadId = leadId, OpportunityId = opportunityId, Type = type,
            Title = request.Title.Trim(), Description = request.Description?.Trim(),
            DueAt = request.DueAt?.ToUniversalTime(), CreatedAt = DateTime.UtcNow,
            CreatedByUserId = actor?.Id, CreatedByUserName = actor?.Name ?? "Sistema",
        };
        _catalog.CrmActivities.Add(activity);
        await _catalog.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, ToDto(activity));
    }

    [HttpPatch("activities/{id:guid}/complete")]
    public async Task<ActionResult<CrmActivityDto>> CompleteActivity(
        Guid id, [FromBody] CompleteCrmActivityRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var activity = await _catalog.CrmActivities.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (activity is null) return NotFound();
        if (activity.Type != CrmActivityType.Tarefa)
            return BadRequest(new { Message = "Somente tarefas podem ser concluídas." });
        activity.CompletedAt ??= DateTime.UtcNow;
        activity.Outcome = request.Outcome?.Trim();
        await _catalog.SaveChangesAsync(ct);
        return Ok(ToDto(activity));
    }

    private async Task<User?> CurrentActorAsync(CancellationToken ct)
    {
        var principal = HttpContext?.User;
        if (principal is null) return null;
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                    principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? await _users.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct)
            : null;
    }

    private static bool CanManageLeads(User user)
    {
        if (user.IsPlatformPrimaryOwner) return true;
        var permissions = PlatformAccessProfiles.Deserialize(user.PlatformPermissionsJson);
        return permissions.Contains(PlatformPermission.All, StringComparer.OrdinalIgnoreCase) ||
               permissions.Contains(PlatformPermission.Leads, StringComparer.OrdinalIgnoreCase);
    }

    private void AddSystemActivity(Guid leadId, CrmOpportunity opportunity,
        CrmActivityType type, string title, DateTime now) =>
        _catalog.CrmActivities.Add(new CrmActivity
        {
            LeadId = leadId, Opportunity = opportunity, Type = type, Title = title,
            CreatedByUserName = "Sistema", CreatedAt = now,
        });

    private static CrmOpportunityDto ToDto(CrmOpportunity o) => new()
    {
        Id = o.Id, LeadId = o.LeadId, Stage = o.Stage.ToString(),
        Probability = o.Probability, Value = o.Value,
        ExpectedCloseDate = o.ExpectedCloseDate,
        AssignedUserId = o.AssignedUserId, AssignedUserName = o.AssignedUserName,
        LostReason = o.LostReason, ClosedAt = o.ClosedAt,
        CreatedAt = o.CreatedAt, UpdatedAt = o.UpdatedAt,
    };

    private static CrmActivityDto ToDto(CrmActivity a) => new()
    {
        Id = a.Id, LeadId = a.LeadId, OpportunityId = a.OpportunityId,
        Type = a.Type.ToString(), Title = a.Title, Description = a.Description,
        DueAt = a.DueAt, CompletedAt = a.CompletedAt, Outcome = a.Outcome,
        CreatedByUserId = a.CreatedByUserId, CreatedByUserName = a.CreatedByUserName,
        CreatedAt = a.CreatedAt,
    };
}
