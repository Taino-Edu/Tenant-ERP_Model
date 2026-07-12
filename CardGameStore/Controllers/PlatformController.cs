// =============================================================================
// PlatformController.cs — Gestão de tenants pelo dono da plataforma.
//
// GET   /api/platform/tenants            → lista todos os tenants
// POST  /api/platform/tenants            → provisiona um tenant novo
// PATCH /api/platform/tenants/{id}/status → suspende/reativa um tenant
// =============================================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/platform")]
[Authorize(Policy = "PlatformOwnerOnly")]
public class PlatformController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    private readonly ITenantProvisioningService _provisioning;
    private readonly ILogger<PlatformController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public PlatformController(
        CatalogDbContext catalog,
        ITenantProvisioningService provisioning,
        ILogger<PlatformController> logger,
        IServiceScopeFactory scopeFactory)
    {
        _catalog      = catalog;
        _provisioning = provisioning;
        _logger       = logger;
        _scopeFactory = scopeFactory;
    }

    private static TenantSummaryDto ToDto(Tenant t) => new()
    {
        Id             = t.Id,
        Slug           = t.Slug,
        SchemaName     = t.SchemaName,
        Status         = t.Status.ToString(),
        CreatedAt      = t.CreatedAt,
        PlanName       = t.PlanName,
        PaymentStatus  = t.PaymentStatus.ToString(),
        EnabledModules = t.EnabledModules,
    };

    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants()
    {
        var tenants = await _catalog.Tenants
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(tenants.Select(ToDto));
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var tenant = await _provisioning.ProvisionAsync(request.Slug, request.AdminEmail, request.AdminPassword);
            return CreatedAtAction(nameof(ListTenants), ToDto(tenant));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada ao provisionar tenant '{Slug}'.", request.Slug);
            return StatusCode(500, new { Message = "Falha ao provisionar o tenant. Tente novamente." });
        }
    }

    [HttpPatch("tenants/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTenantStatusRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Enum.TryParse<TenantStatus>(request.Status, out var status))
            return BadRequest(new { Message = $"Status inválido: '{request.Status}'." });

        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        tenant.Status = status;
        await _catalog.SaveChangesAsync();

        return Ok(ToDto(tenant));
    }

    [HttpPatch("tenants/{id:guid}/billing")]
    public async Task<IActionResult> UpdateBilling(Guid id, [FromBody] UpdateTenantBillingRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Enum.TryParse<TenantPaymentStatus>(request.PaymentStatus, out var paymentStatus))
            return BadRequest(new { Message = $"Status de pagamento inválido: '{request.PaymentStatus}'." });

        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        tenant.PlanName       = request.PlanName;
        tenant.PaymentStatus  = paymentStatus;
        tenant.EnabledModules = request.EnabledModules;
        await _catalog.SaveChangesAsync();

        return Ok(ToDto(tenant));
    }

    // ── GET /api/platform/overview ─────────────────────────────────────────────
    // Visão agregada: receita do mês (todos os tenants ativos), contagens de
    // pagamento/módulo, e um sinal barato de "essa loja tá ativa?" por tenant
    // (último login + última venda) — não é telemetria de verdade (não existe
    // nada disso hoje), só reaproveita dado que já é gravado em cada venda/login.
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var tenants = await _catalog.Tenants.ToListAsync();
        var active  = tenants.Where(t => t.Status == TenantStatus.Active).ToList();

        var dto = new PlatformOverviewDto
        {
            ActiveTenants        = active.Count,
            SuspendedTenants     = tenants.Count - active.Count,
            PaymentStatusCounts  = tenants.GroupBy(t => t.PaymentStatus.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            ModuleAdoptionCounts = tenants.SelectMany(t => t.EnabledModules).GroupBy(m => m).ToDictionary(g => g.Key, g => g.Count()),
        };

        var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        foreach (var tenant in active)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var tc = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tc.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var receitaMes = await db.FechamentosPeriodo
                    .Where(f => f.Tipo == TipoFechamento.Dia && f.DataInicio >= inicioMes)
                    .SumAsync(f => (long?)(f.ReceitaComandas + f.ReceitaAvulsa)) ?? 0;

                var lastLogin   = await db.Users.Where(u => u.IsActive).MaxAsync(u => (DateTime?)u.LastLoginAt);
                var lastComanda = await db.Comandas.Where(c => c.ClosedAt != null).MaxAsync(c => (DateTime?)c.ClosedAt);
                var lastVenda   = await db.VendasAvulsas.MaxAsync(v => (DateTime?)v.SoldAt);
                var lastActivity = new[] { lastLogin, lastComanda, lastVenda }.Where(d => d.HasValue).Max();

                dto.ReceitaMesAtualCents += receitaMes;
                dto.Tenants.Add(new TenantActivityDto
                {
                    TenantId             = tenant.Id,
                    ReceitaMesAtualCents = receitaMes,
                    LastActivityAt       = lastActivity,
                });
            }
            catch (Exception ex)
            {
                // Um schema quebrado/migração pendente não pode derrubar o
                // overview inteiro — loga e segue pros outros tenants.
                _logger.LogError(ex, "Falha ao agregar overview do tenant {Slug}", tenant.Slug);
            }
        }

        return Ok(dto);
    }

    // ── POST /api/platform/tenants/{id}/impersonate ────────────────────────────
    // Gera um ticket de uso único (90s pra clicar) pro dono da plataforma entrar
    // direto no admin daquela loja — ver GET /api/auth/impersonate (AuthController)
    // pra onde o ticket é trocado por uma sessão de verdade. Não é o token em si:
    // o token só nasce como cookie já no domínio certo da loja, no redeem.
    [HttpPost("tenants/{id:guid}/impersonate")]
    public async Task<IActionResult> Impersonate(Guid id)
    {
        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        if (tenant.Status != TenantStatus.Active)
            return Conflict(new { Message = "Não é possível acessar uma loja suspensa. Reative primeiro." });

        var ownerIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (ownerIdClaim is null || !Guid.TryParse(ownerIdClaim.Value, out var ownerId))
            return Unauthorized();

        var ownerName = User.FindFirst(JwtRegisteredClaimNames.Name)?.Value ?? "Dono da Plataforma";

        var ticket = new PlatformImpersonationTicket
        {
            Ticket              = GenerateTicket(),
            TenantId            = tenant.Id,
            TenantSlug          = tenant.Slug,
            PlatformOwnerUserId = ownerId,
            PlatformOwnerName   = ownerName,
            ExpiresAt           = DateTime.UtcNow.AddSeconds(90),
        };
        _catalog.PlatformImpersonationTickets.Add(ticket);
        await _catalog.SaveChangesAsync();

        _logger.LogInformation("Ticket de impersonação gerado — dono {OwnerId} pra loja {Slug}", ownerId, tenant.Slug);

        return Ok(new { ticket = ticket.Ticket });
    }

    /// <summary>32 bytes aleatórios, base64url — credencial de uso único, não um
    /// identificador sequencial/previsível.</summary>
    private static string GenerateTicket() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
