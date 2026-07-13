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

    /// <summary>Lista todos os tenants cadastrados na plataforma, mais recente primeiro.
    /// Somente o dono da plataforma.</summary>
    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants()
    {
        var tenants = await _catalog.Tenants
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(tenants.Select(ToDto));
    }

    /// <summary>Provisiona uma loja nova: cria o schema PostgreSQL dedicado, aplica as
    /// migrations e cria o admin inicial. Somente o dono da plataforma.</summary>
    /// <param name="request">Slug da loja e credenciais do admin inicial.</param>
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

    /// <summary>Suspende ou reativa uma loja. Uma loja suspensa tem a API bloqueada
    /// (401/403 nas rotas do tenant), mas os dados permanecem intactos.</summary>
    /// <param name="id">Id do tenant.</param>
    /// <param name="request">Novo status ("Active" ou "Suspended").</param>
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

    /// <summary>Atualiza plano, status de pagamento e módulos pagos habilitados
    /// (ex: Fiscal, Estoque) de uma loja.</summary>
    /// <param name="id">Id do tenant.</param>
    /// <param name="request">Nome do plano, status de pagamento e lista de módulos habilitados.</param>
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

    /// <summary>
    /// Visão agregada de todas as lojas: receita do mês (tenants ativos), contagens de
    /// pagamento/módulo, e um sinal barato de "essa loja tá ativa?" por tenant (último
    /// login + última venda) — não é telemetria de verdade, só reaproveita dado que já
    /// é gravado em cada venda/login. Uma falha ao agregar um tenant específico não
    /// derruba a chamada inteira (log e segue pros outros).
    /// </summary>
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
                var (receitaMes, lastActivity) = await RunInTenantScopeAsync(tenant, async db =>
                {
                    var receita = await db.FechamentosPeriodo
                        .Where(f => f.Tipo == TipoFechamento.Dia && f.DataInicio >= inicioMes)
                        .SumAsync(f => (long?)(f.ReceitaComandas + f.ReceitaAvulsa)) ?? 0;

                    var lastLogin   = await db.Users.Where(u => u.IsActive).MaxAsync(u => (DateTime?)u.LastLoginAt);
                    var lastComanda = await db.Comandas.Where(c => c.ClosedAt != null).MaxAsync(c => (DateTime?)c.ClosedAt);
                    var lastVenda   = await db.VendasAvulsas.MaxAsync(v => (DateTime?)v.SoldAt);
                    var atividade   = new[] { lastLogin, lastComanda, lastVenda }.Where(d => d.HasValue).Max();

                    return (receita, atividade);
                });

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

    /// <summary>Roda uma consulta dentro do schema de um tenant específico, num
    /// escopo de DI isolado — mesmo padrão repetido em cada leitura cross-tenant
    /// (overview, staff, clientes, audit log). Não reaproveita o request atual
    /// porque este controller não roda no schema de nenhum tenant (é
    /// PlatformOwnerOnly, atende no domínio da própria plataforma).</summary>
    private async Task<T> RunInTenantScopeAsync<T>(Tenant tenant, Func<AppDbContext, Task<T>> query)
    {
        using var scope = _scopeFactory.CreateScope();
        var tc = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tc.Set(tenant.Id, tenant.SchemaName, tenant.EnabledModules);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    // =========================================================================
    // Funcionários & clientes de um tenant específico — visão do dono da
    // plataforma pra dentro de cada loja.
    // =========================================================================

    /// <summary>Lista os funcionários/admins (Role Admin ou Operator) do schema
    /// de um tenant. Nunca inclui hash de senha nem tokens.</summary>
    /// <param name="id">Id do tenant.</param>
    [HttpGet("tenants/{id:guid}/staff")]
    public async Task<IActionResult> GetTenantStaff(Guid id)
    {
        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        try
        {
            var staff = await RunInTenantScopeAsync(tenant, db => db.Users
                .Include(u => u.Perfil)
                .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.Operator)
                .OrderBy(u => u.Name)
                .Select(u => new TenantStaffDto
                {
                    Id          = u.Id,
                    Name        = u.Name,
                    Email       = u.Email,
                    Role        = u.Role,
                    PerfilNome  = u.Perfil != null ? u.Perfil.Nome : null,
                    IsActive    = u.IsActive,
                    LastLoginAt = u.LastLoginAt,
                    CreatedAt   = u.CreatedAt,
                })
                .ToListAsync());

            return Ok(staff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar funcionários do tenant {Slug}", tenant.Slug);
            return StatusCode(500, new { Message = "Falha ao carregar funcionários desta loja." });
        }
    }

    /// <summary>Lista os clientes (Role Customer) do schema de um tenant, paginado.</summary>
    /// <param name="id">Id do tenant.</param>
    /// <param name="page">Número da página (base 1, padrão 1).</param>
    /// <param name="pageSize">Registros por página (padrão 50, máximo 200).</param>
    [HttpGet("tenants/{id:guid}/customers")]
    public async Task<IActionResult> GetTenantCustomers(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        try
        {
            var result = await RunInTenantScopeAsync(tenant, async db =>
            {
                var query = db.Users.Where(u => u.Role == UserRole.Customer);
                var total = await query.CountAsync();

                var items = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new TenantCustomerDto
                    {
                        Id          = u.Id,
                        Name        = u.Name,
                        Email       = u.Email,
                        WhatsApp    = u.WhatsApp,
                        IsActive    = u.IsActive,
                        LastLoginAt = u.LastLoginAt,
                        CreatedAt   = u.CreatedAt,
                    })
                    .ToListAsync();

                return new PagedResult<TenantCustomerDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar clientes do tenant {Slug}", tenant.Slug);
            return StatusCode(500, new { Message = "Falha ao carregar clientes desta loja." });
        }
    }

    // =========================================================================
    // Logs (fase 1) — expõe o AuditLog de negócio (Create/Update/Delete) que já
    // existe em cada schema de tenant. Não é log de sistema/erro — essa é uma
    // fase futura, exigiria um sink novo (hoje é só console -> arquivo do
    // Docker, sem agregador).
    // =========================================================================

    /// <summary>Audit log de um tenant específico (mesma fonte de dados do
    /// AuditController, visto pelo dono da plataforma).</summary>
    /// <param name="id">Id do tenant.</param>
    /// <param name="page">Número da página (base 1, padrão 1).</param>
    /// <param name="pageSize">Registros por página (padrão 50, máximo 200).</param>
    /// <param name="entityType">Filtro por tipo de entidade (ex: "User").</param>
    /// <param name="action">Filtro por ação (ex: "Create", "Update", "Delete").</param>
    [HttpGet("tenants/{id:guid}/audit-logs")]
    public async Task<IActionResult> GetTenantAuditLogs(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? entityType = null,
        [FromQuery] string? action = null)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        try
        {
            var result = await RunInTenantScopeAsync(tenant, async db =>
            {
                var query = db.AuditLogs.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(entityType))
                    query = query.Where(a => a.EntityType == entityType);

                if (!string.IsNullOrWhiteSpace(action))
                    query = query.Where(a => a.Action == action);

                var total = await query.CountAsync();

                var items = await query
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new AuditLogDto
                    {
                        Id            = a.Id,
                        ActorUserId   = a.ActorUserId,
                        ActorUserName = a.ActorUserName,
                        Action        = a.Action,
                        EntityType    = a.EntityType,
                        EntityId      = a.EntityId,
                        Details       = a.Details,
                        CreatedAt     = a.CreatedAt,
                    })
                    .ToListAsync();

                return new PagedResult<AuditLogDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar audit logs do tenant {Slug}", tenant.Slug);
            return StatusCode(500, new { Message = "Falha ao carregar o histórico desta loja." });
        }
    }

    /// <summary>Feed agregado dos 100 registros de auditoria mais recentes entre
    /// todos os tenants ativos (20 mais recentes de cada, mesclados e cortados).
    /// Custo aceitável no volume atual de tenants — se crescer muito isso vira
    /// um job agregador, não uma query on-demand.</summary>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAggregatedAuditLogs()
    {
        var tenants = await _catalog.Tenants.Where(t => t.Status == TenantStatus.Active).ToListAsync();
        var feed = new List<PlatformAuditLogDto>();

        foreach (var tenant in tenants)
        {
            try
            {
                var recent = await RunInTenantScopeAsync(tenant, db => db.AuditLogs
                    .AsNoTracking()
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(20)
                    .Select(a => new PlatformAuditLogDto
                    {
                        Id            = a.Id,
                        TenantSlug    = tenant.Slug,
                        ActorUserId   = a.ActorUserId,
                        ActorUserName = a.ActorUserName,
                        Action        = a.Action,
                        EntityType    = a.EntityType,
                        EntityId      = a.EntityId,
                        Details       = a.Details,
                        CreatedAt     = a.CreatedAt,
                    })
                    .ToListAsync());

                feed.AddRange(recent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao agregar audit log do tenant {Slug}", tenant.Slug);
            }
        }

        return Ok(feed.OrderByDescending(a => a.CreatedAt).Take(100));
    }

    /// <summary>
    /// Gera um ticket de uso único (expira em 90s) pro dono da plataforma acessar o
    /// admin de uma loja sem digitar subdomínio nem logar de novo — o ticket é trocado
    /// por uma sessão de verdade em <c>GET /api/auth/impersonate</c> (AuthController), que
    /// já roda no domínio certo da loja (o token só nasce como cookie no redeem). Rejeita
    /// lojas suspensas.
    /// </summary>
    /// <param name="id">Id do tenant a acessar.</param>
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

    // =========================================================================
    // Leads — quem demonstrou interesse via CTA da landing (POST /api/leads,
    // LeadsController, sem auth). Gestão aqui é PlatformOwnerOnly.
    // =========================================================================

    private static LeadDto ToDto(Lead l) => new()
    {
        Id                = l.Id,
        Nome              = l.Nome,
        Telefone          = l.Telefone,
        Email             = l.Email,
        Mensagem          = l.Mensagem,
        Origem            = l.Origem,
        Status            = l.Status.ToString(),
        Notas             = l.Notas,
        CreatedAt         = l.CreatedAt,
        UpdatedAt         = l.UpdatedAt,
        ConvertedTenantId = l.ConvertedTenantId,
    };

    /// <summary>Lista os leads captados pela landing, mais recente primeiro.
    /// Somente o dono da plataforma.</summary>
    /// <param name="status">Filtro opcional por status ("Novo", "Contatado", "Convertido", "Perdido").</param>
    [HttpGet("leads")]
    public async Task<IActionResult> ListLeads([FromQuery] string? status = null)
    {
        var query = _catalog.Leads.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<LeadStatus>(status, out var parsed))
                return BadRequest(new { Message = $"Status inválido: '{status}'." });
            query = query.Where(l => l.Status == parsed);
        }

        var leads = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
        return Ok(leads.Select(ToDto));
    }

    /// <summary>Atualiza status/anotações de um lead — inclui marcar como
    /// convertido quando o dono cadastra o tenant correspondente.</summary>
    /// <param name="id">Id do lead.</param>
    /// <param name="request">Novo status, anotações e (opcional) id do tenant gerado.</param>
    [HttpPatch("leads/{id:guid}")]
    public async Task<IActionResult> UpdateLead(Guid id, [FromBody] UpdateLeadRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Enum.TryParse<LeadStatus>(request.Status, out var status))
            return BadRequest(new { Message = $"Status inválido: '{request.Status}'." });

        var lead = await _catalog.Leads.FirstOrDefaultAsync(l => l.Id == id);
        if (lead is null) return NotFound();

        lead.Status            = status;
        lead.Notas             = request.Notas;
        lead.ConvertedTenantId = request.ConvertedTenantId ?? lead.ConvertedTenantId;
        lead.UpdatedAt         = DateTime.UtcNow;
        await _catalog.SaveChangesAsync();

        return Ok(ToDto(lead));
    }

    // =========================================================================
    // Suporte — lado do dono da plataforma. Tickets são abertos pelo lojista
    // via SupportController (api/support, AdminOnly) e vivem no catálogo —
    // cross-tenant por natureza, mesmo raciocínio de ContadorAviso.
    // =========================================================================

    private static SupportTicketDto ToDto(SupportTicket t, string? tenantSlug) => new()
    {
        Id                = t.Id,
        TenantId          = t.TenantId,
        TenantSlug        = tenantSlug,
        Subject           = t.Subject,
        Status            = t.Status.ToString(),
        CreatedByUserName = t.CreatedByUserName,
        CreatedAt         = t.CreatedAt,
        UpdatedAt         = t.UpdatedAt,
        MessageCount      = t.Messages.Count,
    };

    private static SupportTicketMessageDto ToDto(SupportTicketMessage m) => new()
    {
        Id         = m.Id,
        AuthorRole = m.AuthorRole.ToString(),
        AuthorName = m.AuthorName,
        Body       = m.Body,
        CreatedAt  = m.CreatedAt,
    };

    /// <summary>Lista os chamados de suporte de todas as lojas, mais recente
    /// primeiro.</summary>
    /// <param name="status">Filtro opcional por status.</param>
    /// <param name="tenantId">Filtro opcional por tenant.</param>
    [HttpGet("support-tickets")]
    public async Task<IActionResult> ListSupportTickets([FromQuery] string? status = null, [FromQuery] Guid? tenantId = null)
    {
        var query = _catalog.SupportTickets.Include(t => t.Messages).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<SupportTicketStatus>(status, out var parsed))
                return BadRequest(new { Message = $"Status inválido: '{status}'." });
            query = query.Where(t => t.Status == parsed);
        }

        if (tenantId.HasValue)
            query = query.Where(t => t.TenantId == tenantId.Value);

        var tickets      = await query.OrderByDescending(t => t.UpdatedAt).ToListAsync();
        var slugByTenant = await _catalog.Tenants.ToDictionaryAsync(t => t.Id, t => t.Slug);

        return Ok(tickets.Select(t => ToDto(t, slugByTenant.GetValueOrDefault(t.TenantId))));
    }

    /// <summary>Detalha um chamado de qualquer loja, com todas as mensagens.</summary>
    /// <param name="id">Id do chamado.</param>
    [HttpGet("support-tickets/{id:guid}")]
    public async Task<IActionResult> GetSupportTicket(Guid id)
    {
        var ticket = await _catalog.SupportTickets.Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();

        var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == ticket.TenantId);

        var dto = new SupportTicketDetailDto
        {
            Id = ticket.Id, TenantId = ticket.TenantId, TenantSlug = tenant?.Slug,
            Subject = ticket.Subject, Status = ticket.Status.ToString(),
            CreatedByUserName = ticket.CreatedByUserName,
            CreatedAt = ticket.CreatedAt, UpdatedAt = ticket.UpdatedAt,
            MessageCount = ticket.Messages.Count,
            Messages = ticket.Messages.OrderBy(m => m.CreatedAt).Select(ToDto).ToList(),
        };
        return Ok(dto);
    }

    /// <summary>Responde um chamado — a primeira resposta de um chamado Aberto
    /// já marca ele como EmAndamento.</summary>
    /// <param name="id">Id do chamado.</param>
    /// <param name="request">Texto da mensagem.</param>
    [HttpPost("support-tickets/{id:guid}/messages")]
    public async Task<IActionResult> ReplySupportTicket(Guid id, [FromBody] CreateSupportMessageRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ticket = await _catalog.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();

        var ownerIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        var ownerId      = ownerIdClaim != null && Guid.TryParse(ownerIdClaim.Value, out var g) ? g : Guid.Empty;
        var ownerName    = User.FindFirst(JwtRegisteredClaimNames.Name)?.Value ?? "Dono da Plataforma";

        _catalog.SupportTicketMessages.Add(new SupportTicketMessage
        {
            TicketId     = ticket.Id,
            AuthorRole   = SupportTicketAuthorRole.Platform,
            AuthorUserId = ownerId,
            AuthorName   = ownerName,
            Body         = request.Body.Trim(),
        });

        if (ticket.Status == SupportTicketStatus.Aberto)
            ticket.Status = SupportTicketStatus.EmAndamento;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _catalog.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Muda o status de um chamado (ex: marcar como Resolvido/Fechado).</summary>
    /// <param name="id">Id do chamado.</param>
    /// <param name="request">Novo status.</param>
    [HttpPatch("support-tickets/{id:guid}/status")]
    public async Task<IActionResult> UpdateSupportTicketStatus(Guid id, [FromBody] UpdateSupportTicketStatusRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Enum.TryParse<SupportTicketStatus>(request.Status, out var status))
            return BadRequest(new { Message = $"Status inválido: '{request.Status}'." });

        var ticket = await _catalog.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();

        ticket.Status    = status;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _catalog.SaveChangesAsync();

        return Ok(new { ticket.Id, Status = ticket.Status.ToString() });
    }
}
