// =============================================================================
// AccountLocatorService.cs — "Não achei minha conta aqui, procurar em outro
// lugar": confirma a senha contra PlatformOwner (schema public), Contador
// (catálogo) e todo tenant ativo, gerando um LoginRedirectTicket pra cada
// acerto. Só é chamado por ação explícita do usuário (botão, depois de um
// login falhar) — nunca em toda tentativa de senha errada, porque o loop por
// tenant escala linearmente com o número de lojas (poucas hoje; revisar se
// crescer muito, mesmo espírito de outras decisões já documentadas no
// BACKLOG.md).
// =============================================================================

using System.Security.Cryptography;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardGameStore.Services.Implementations;

public class AccountLocatorService : IAccountLocatorService
{
    private readonly CatalogDbContext _catalog;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountLocatorService> _logger;

    public AccountLocatorService(CatalogDbContext catalog, IServiceScopeFactory scopeFactory, ILogger<AccountLocatorService> logger)
    {
        _catalog      = catalog;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    public async Task<List<LocateAccountMatchDto>> LocateAsync(string email, string password)
    {
        var matches = new List<LocateAccountMatchDto>();
        var normalizedEmail = email.Trim().ToLowerInvariant();

        // ── PlatformOwner: User no schema "public" (tenant-zero) ────────────────
        var platformOwner = await RunInTenantScopeAsync(
            TenantConstants.TenantZeroId, TenantConstants.TenantZeroSchema, Array.Empty<string>(),
            db => db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive && u.Role == UserRole.PlatformOwner));

        if (platformOwner != null && platformOwner.PasswordHash != null
            && BCrypt.Net.BCrypt.Verify(password, platformOwner.PasswordHash))
        {
            matches.Add(await MintMatchAsync(LoginRedirectTargetKind.PlatformOwner, platformOwner.Id, "Dono da Plataforma", null, null));
        }

        // ── Contador: cross-tenant, já vive no catálogo ─────────────────────────
        var conta = await _catalog.ContadorAccounts.FirstOrDefaultAsync(c => c.Email == normalizedEmail);
        if (conta != null && BCrypt.Net.BCrypt.Verify(password, conta.PasswordHash))
        {
            matches.Add(await MintMatchAsync(LoginRedirectTargetKind.Contador, conta.Id, "Contador", null, null));
        }

        // ── Admin/Operator/Customer: um loop por tenant ativo ───────────────────
        var tenants = await _catalog.Tenants
            .Where(t => t.Status == TenantStatus.Active)
            .Select(t => new { t.Id, t.Slug, t.DisplayName, t.SchemaName, t.EnabledModules })
            .ToListAsync();

        foreach (var tenant in tenants)
        {
            try
            {
                var user = await RunInTenantScopeAsync(
                    tenant.Id, tenant.SchemaName, tenant.EnabledModules,
                    db => db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive
                        && (u.Role == UserRole.Admin || u.Role == UserRole.Operator || u.Role == UserRole.Customer)));

                if (user != null && user.PasswordHash != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    var label = string.IsNullOrWhiteSpace(tenant.DisplayName) ? tenant.Slug : tenant.DisplayName;
                    matches.Add(await MintMatchAsync(LoginRedirectTargetKind.Tenant, user.Id, label!, tenant.Id, tenant.Slug));
                }
            }
            catch (Exception ex)
            {
                // Mesmo espírito do overview da plataforma: um schema quebrado
                // não pode travar a busca nos demais tenants.
                _logger.LogError(ex, "Falha ao procurar conta no tenant {Slug} durante locate-account", tenant.Slug);
            }
        }

        _logger.LogInformation("locate-account: {Count} acerto(s) pra {Email}", matches.Count, MaskEmail(normalizedEmail));
        return matches;
    }

    private async Task<LocateAccountMatchDto> MintMatchAsync(string targetKind, Guid accountId, string label, Guid? tenantId, string? tenantSlug)
    {
        var ticket = new LoginRedirectTicket
        {
            Ticket     = GenerateTicket(),
            TargetKind = targetKind,
            AccountId  = accountId,
            TenantId   = tenantId,
            TenantSlug = tenantSlug,
            ExpiresAt  = DateTime.UtcNow.AddSeconds(90),
        };
        _catalog.LoginRedirectTickets.Add(ticket);
        await _catalog.SaveChangesAsync();

        return new LocateAccountMatchDto(label, targetKind, tenantSlug, ticket.Ticket);
    }

    /// <summary>Mesmo padrão de PlatformController.RunInTenantScopeAsync — escopo de DI
    /// isolado por tenant, pra resolver um AppDbContext conectado no schema certo.</summary>
    private async Task<T> RunInTenantScopeAsync<T>(Guid tenantId, string schemaName, string[] enabledModules, Func<AppDbContext, Task<T>> query)
    {
        using var scope = _scopeFactory.CreateScope();
        var tc = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tc.Set(tenantId, schemaName, enabledModules);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    private static string GenerateTicket() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var local = email[..at];
        var visible = local.Length > 1 ? local[0] + new string('*', Math.Min(local.Length - 1, 3)) : "*";
        return visible + email[at..];
    }
}
