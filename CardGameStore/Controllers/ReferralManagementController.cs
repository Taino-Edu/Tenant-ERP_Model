using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Security;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/platform/referrals")]
[Authorize(Policy = "PlatformOwnerOnly")]
[RequirePlatformPermission(PlatformPermission.ReferralsRead)]
public class ReferralManagementController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    private readonly IReferralCommissionService _commissions;

    public ReferralManagementController(CatalogDbContext catalog, IReferralCommissionService commissions)
    {
        _catalog = catalog;
        _commissions = commissions;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ReferralSummaryDto>> Summary()
    {
        var today = DateTime.UtcNow.Date;
        var items = await _catalog.ReferralCommissions.AsNoTracking().ToListAsync();
        var referredTenantIds = await _catalog.TenantReferrals.AsNoTracking()
            .Where(r => r.Active).Select(r => r.TenantId).Distinct().ToListAsync();
        var referredMrr = await _catalog.Tenants.AsNoTracking()
            .Where(t => referredTenantIds.Contains(t.Id) && t.Status == TenantStatus.Active)
            .SumAsync(t => (decimal?)t.MonthlyPrice) ?? 0m;

        return Ok(new ReferralSummaryDto
        {
            ActivePartners = await _catalog.ReferralPartners.CountAsync(p => p.Active),
            ReferredClients = referredTenantIds.Count,
            PendingAmount = items.Where(c => c.PaidAt == null).Sum(c => c.Amount),
            OverdueAmount = items.Where(c => c.PaidAt == null && c.DueDate.Date < today).Sum(c => c.Amount),
            PaidAmount = items.Where(c => c.PaidAt != null).Sum(c => c.Amount),
            ReferredMrr = referredMrr,
        });
    }

    [HttpGet("partners")]
    public async Task<ActionResult<List<ReferralPartnerDto>>> Partners()
    {
        var partners = await _catalog.ReferralPartners.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        var referrals = await _catalog.TenantReferrals.AsNoTracking().ToListAsync();
        var commissions = await _catalog.ReferralCommissions.AsNoTracking().ToListAsync();

        return Ok(partners.Select(p =>
        {
            var ids = referrals.Where(r => r.PartnerId == p.Id).Select(r => r.Id).ToHashSet();
            var own = commissions.Where(c => ids.Contains(c.ReferralId)).ToList();
            return new ReferralPartnerDto
            {
                Id = p.Id, Name = p.Name, Document = p.Document, Phone = p.Phone, Email = p.Email,
                PixKey = p.PixKey, SetupCommissionPercent = p.SetupCommissionPercent,
                MonthlyCommissionPercent = p.MonthlyCommissionPercent, PaymentDay = p.PaymentDay,
                Active = p.Active, ReferredClients = referrals.Count(r => r.PartnerId == p.Id && r.Active),
                PendingAmount = own.Where(c => c.PaidAt == null).Sum(c => c.Amount),
                PaidAmount = own.Where(c => c.PaidAt != null).Sum(c => c.Amount),
                NextPaymentDate = own.Where(c => c.PaidAt == null).OrderBy(c => c.DueDate)
                    .Select(c => (DateTime?)c.DueDate).FirstOrDefault(),
            };
        }).ToList());
    }

    [HttpPost("partners")]
    [RequirePlatformPermission(PlatformPermission.ReferralsManage)]
    public async Task<IActionResult> CreatePartner([FromBody] SaveReferralPartnerRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var partner = new ReferralPartner();
        Apply(partner, request);
        _catalog.ReferralPartners.Add(partner);
        await _catalog.SaveChangesAsync();
        return Created($"api/platform/referrals/partners/{partner.Id}", new { partner.Id });
    }

    [HttpPut("partners/{id:guid}")]
    [RequirePlatformPermission(PlatformPermission.ReferralsManage)]
    public async Task<IActionResult> UpdatePartner(Guid id, [FromBody] SaveReferralPartnerRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var partner = await _catalog.ReferralPartners.FindAsync(id);
        if (partner is null) return NotFound();
        Apply(partner, request);
        await _catalog.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("assignments")]
    public async Task<ActionResult<List<TenantReferralDto>>> Assignments()
    {
        var result = await (from r in _catalog.TenantReferrals.AsNoTracking()
                            join p in _catalog.ReferralPartners on r.PartnerId equals p.Id
                            join t in _catalog.Tenants on r.TenantId equals t.Id
                            orderby r.CreatedAt descending
                            select new TenantReferralDto
                            {
                                Id = r.Id, PartnerId = p.Id, PartnerName = p.Name,
                                TenantId = t.Id, TenantName = t.DisplayName ?? t.Slug,
                                SourceLeadId = r.SourceLeadId,
                                SetupCommissionPercent = r.SetupCommissionPercent,
                                MonthlyCommissionPercent = r.MonthlyCommissionPercent,
                                MonthlyCommissionCycles = r.MonthlyCommissionCycles,
                                StartedOn = r.StartedOn, Active = r.Active, Notes = r.Notes,
                            }).ToListAsync();
        return Ok(result);
    }

    [HttpPost("assignments")]
    [RequirePlatformPermission(PlatformPermission.ReferralsManage)]
    public async Task<IActionResult> SaveAssignment([FromBody] SaveTenantReferralRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var partner = await _catalog.ReferralPartners.FindAsync(request.PartnerId);
        var tenant = await _catalog.Tenants.FindAsync(request.TenantId);
        if (partner is null || tenant is null)
            return BadRequest(new { Message = "Vendedor ou cliente não encontrado." });
        if (request.SourceLeadId.HasValue && !await _catalog.Leads.AnyAsync(l => l.Id == request.SourceLeadId))
            return BadRequest(new { Message = "Lead de origem não encontrado." });

        var referral = await _catalog.TenantReferrals.FirstOrDefaultAsync(r => r.TenantId == request.TenantId);
        if (referral is null)
        {
            referral = new TenantReferral { TenantId = request.TenantId };
            _catalog.TenantReferrals.Add(referral);
        }

        referral.PartnerId = partner.Id;
        referral.SourceLeadId = request.SourceLeadId;
        referral.SetupCommissionPercent = request.SetupCommissionPercent ?? partner.SetupCommissionPercent;
        referral.MonthlyCommissionPercent = request.MonthlyCommissionPercent ?? partner.MonthlyCommissionPercent;
        referral.MonthlyCommissionCycles = request.MonthlyCommissionCycles;
        referral.StartedOn = (request.StartedOn ?? tenant.CreatedAt).ToUniversalTime();
        referral.Active = request.Active;
        referral.Notes = Clean(request.Notes);
        referral.UpdatedAt = DateTime.UtcNow;

        if (tenant.SetupFee > 0 && !await _catalog.TenantCharges.AnyAsync(c =>
                c.TenantId == tenant.Id && c.Kind == TenantChargeKind.Implantacao))
        {
            var created = tenant.CreatedAt.ToUniversalTime();
            _catalog.TenantCharges.Add(new TenantCharge
            {
                TenantId = tenant.Id, Kind = TenantChargeKind.Implantacao, Amount = tenant.SetupFee,
                ReferenceMonth = new DateTime(created.Year, created.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                DueDate = created.Date,
                Notes = "Implantação gerada ao registrar a indicação comercial.",
            });
        }

        await _catalog.SaveChangesAsync();
        await _commissions.SynchronizeReferralAsync(referral.Id);
        await _catalog.SaveChangesAsync();
        return Ok(new { referral.Id });
    }

    [HttpGet("commissions")]
    public async Task<ActionResult<List<ReferralCommissionDto>>> Commissions(
        [FromQuery] Guid? partnerId, [FromQuery] string? status)
    {
        var today = DateTime.UtcNow.Date;
        var query = from c in _catalog.ReferralCommissions.AsNoTracking()
                    join r in _catalog.TenantReferrals on c.ReferralId equals r.Id
                    join p in _catalog.ReferralPartners on r.PartnerId equals p.Id
                    join t in _catalog.Tenants on r.TenantId equals t.Id
                    select new { c, r, p, t };
        if (partnerId.HasValue) query = query.Where(x => x.p.Id == partnerId.Value);

        var rows = await query.OrderBy(x => x.c.PaidAt != null).ThenBy(x => x.c.DueDate).ToListAsync();
        var result = rows.Select(x => new ReferralCommissionDto
        {
            Id = x.c.Id, PartnerId = x.p.Id, PartnerName = x.p.Name,
            TenantId = x.t.Id, TenantName = x.t.DisplayName ?? x.t.Slug,
            Type = x.c.ChargeKind.ToString(), BaseAmount = x.c.BaseAmount,
            CommissionPercent = x.c.CommissionPercent, Amount = x.c.Amount,
            ReferenceMonth = x.c.ReferenceMonth, EarnedAt = x.c.EarnedAt,
            DueDate = x.c.DueDate, PaidAt = x.c.PaidAt,
            Status = x.c.PaidAt != null ? "Pago" : x.c.DueDate.Date < today ? "Vencido" : "Pendente",
        });
        if (!string.IsNullOrWhiteSpace(status))
            result = result.Where(c => c.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        return Ok(result.ToList());
    }

    [HttpPut("commissions/{id:guid}/payment")]
    [RequirePlatformPermission(PlatformPermission.ReferralsManage)]
    public async Task<IActionResult> SetCommissionPayment(Guid id, [FromBody] SetReferralCommissionPaymentRequest request)
    {
        if (request.PaidAt.HasValue && request.PaidAt.Value.Date > DateTime.UtcNow.Date)
            return BadRequest(new { Message = "A data de pagamento não pode ser futura." });
        var commission = await _catalog.ReferralCommissions.FindAsync(id);
        if (commission is null) return NotFound();
        commission.PaidAt = request.PaidAt?.ToUniversalTime();
        await _catalog.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(ReferralPartner partner, SaveReferralPartnerRequest request)
    {
        partner.Name = request.Name.Trim();
        partner.Document = Clean(request.Document);
        partner.Phone = Clean(request.Phone);
        partner.Email = Clean(request.Email)?.ToLowerInvariant();
        partner.PixKey = Clean(request.PixKey);
        partner.SetupCommissionPercent = request.SetupCommissionPercent;
        partner.MonthlyCommissionPercent = request.MonthlyCommissionPercent;
        partner.PaymentDay = request.PaymentDay;
        partner.Active = request.Active;
        partner.UpdatedAt = DateTime.UtcNow;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
