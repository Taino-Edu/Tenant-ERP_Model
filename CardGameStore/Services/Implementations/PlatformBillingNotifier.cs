// =============================================================================
// PlatformBillingNotifier.cs — E-mails de cobrança da plataforma para a loja.
//
// Dois cuidados moldam esta classe:
//
// 1. Escopo próprio no tenant-zero. O EmailService lê a configuração de SMTP do
//    schema do tenant corrente; chamado de dentro de uma requisição de loja, o
//    aviso de cobrança sairia pelo SMTP que o lojista configurou para os
//    clientes dele. Aqui todo envio abre um escopo apontado para o schema
//    "public", o da plataforma.
//
// 2. Um aviso por fato. O registro em TenantBillingNotice é gravado ANTES do
//    envio: se dois chamadores (job e webhook) avisarem ao mesmo tempo, o índice
//    único deixa só um passar. O custo é que uma falha de SMTP perde aquele
//    aviso — preferível a mandar o mesmo "sua loja será suspensa" a cada rodada.
// =============================================================================

using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class PlatformBillingNotifier : IPlatformBillingNotifier
{
    internal const string DadosFaltando  = "dados-faltando";
    internal const string AvisoSuspensao = "aviso-suspensao";
    internal const string Suspensa       = "suspensa";
    internal const string Reativada      = "reativada";

    /// <summary>Com quantos dias de antecedência avisar que falta o documento
    /// antes da primeira mensalidade.</summary>
    private const int DiasAntesDaPrimeiraCobranca = 5;

    /// <summary>Com quantos dias de antecedência avisar a suspensão.</summary>
    private const int DiasAntesDaSuspensao = 3;

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<PlatformBillingNotifier> _logger;

    public PlatformBillingNotifier(
        IServiceScopeFactory scopes, IConfiguration config, ILogger<PlatformBillingNotifier> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    /// <summary>Mesmo padrão do PlatformBillingService: 15 dias, o piso da
    /// cláusula 15.1 do contrato. Os dois leem a mesma chave, e divergir aqui
    /// faria o aviso prometer uma data de suspensão diferente da real.</summary>
    private int DiasDeCarencia =>
        int.TryParse(_config["Billing:DiasDeCarenciaAposVencimento"], out var dias) && dias >= 0
            ? dias
            : PlatformBillingService.CarenciaPadraoDoContrato;

    public void NotificarLojaSuspensa(Guid tenantId) =>
        Disparar(tenantId, Suspensa, (email, t) => email.SendLojaSuspensaAsync(
            t.BillingEmail!, NomeDe(t), NomeDe(t), UrlDaLoja(t.Slug, "/admin/assinatura")));

    public void NotificarLojaReativada(Guid tenantId) =>
        Disparar(tenantId, Reativada, (email, t) => email.SendLojaReativadaAsync(
            t.BillingEmail!, NomeDe(t), NomeDe(t), UrlDaLoja(t.Slug, "/login")));

    private void Disparar(Guid tenantId, string tipo, Func<IEmailService, Tenant, Task> enviar)
    {
        // Referência = o dia: suspensa e reativada de novo no mesmo dia é raro o
        // bastante para um aviso só bastar, e em dias diferentes são fatos novos.
        var referencia = DateTime.UtcNow.ToString("yyyy-MM-dd");

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = CriarEscopoDaPlataforma();
                var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                var tenant = await catalog.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
                if (tenant is null) return;

                await EnviarUmaVezAsync(scope.ServiceProvider, tenant, tipo, referencia, enviar, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar o aviso '{Tipo}' de cobrança do tenant {TenantId}", tipo, tenantId);
            }
        });
    }

    public async Task EnviarAvisosDePrazoAsync(CancellationToken ct = default)
    {
        using var scope = CriarEscopoDaPlataforma();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var hoje = DateTime.UtcNow.Date;

        // ── Falta o documento perto (ou depois) da primeira mensalidade ──────
        var limiteInicio = DateTime.SpecifyKind(hoje.AddDays(DiasAntesDaPrimeiraCobranca + 1), DateTimeKind.Utc);
        var semDocumento = await catalog.Tenants.AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active
                     && t.PaymentStatus != TenantPaymentStatus.Isento
                     && t.MonthlyPrice > 0
                     && t.BillingStartsOn != null && t.BillingStartsOn < limiteInicio
                     && (t.BillingCnpj == null || t.BillingCnpj == ""))
            .ToListAsync(ct);

        foreach (var tenant in semDocumento)
        {
            var inicio = tenant.BillingStartsOn!.Value;
            await EnviarUmaVezAsync(scope.ServiceProvider, tenant, DadosFaltando, inicio.ToString("yyyy-MM-dd"),
                (email, t) => email.SendCobrancaDadosFaltandoAsync(
                    t.BillingEmail!, NomeDe(t), NomeDe(t), inicio, UrlDaLoja(t.Slug, "/admin/assinatura")),
                ct);
        }

        // ── Suspensão chegando ───────────────────────────────────────────────
        // Vencida, ainda dentro da carência (a régua não suspendeu), e a
        // suspensão cai nos próximos dias. Uma cobrança por loja: a mais antiga,
        // que é a que vai derrubar a loja primeiro.
        var suspendeAPartirDe = DateTime.SpecifyKind(hoje.AddDays(-DiasDeCarencia), DateTimeKind.Utc);
        var avisarAte         = DateTime.SpecifyKind(hoje.AddDays(DiasAntesDaSuspensao - DiasDeCarencia + 1), DateTimeKind.Utc);
        var hojeUtc           = DateTime.SpecifyKind(hoje, DateTimeKind.Utc);

        var vencidas = await catalog.TenantCharges.AsNoTracking()
            .Where(c => c.PaidAt == null && c.Amount > 0
                     && c.DueDate < hojeUtc
                     && c.DueDate >= suspendeAPartirDe
                     && c.DueDate < avisarAte)
            .OrderBy(c => c.DueDate)
            .ToListAsync(ct);

        var tenantIds = vencidas.Select(c => c.TenantId).Distinct().ToList();
        var tenants = await catalog.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id)
                     && t.Status == TenantStatus.Active
                     && t.PaymentStatus != TenantPaymentStatus.Isento)
            .ToDictionaryAsync(t => t.Id, ct);

        foreach (var cobranca in vencidas.GroupBy(c => c.TenantId).Select(g => g.First()))
        {
            if (!tenants.TryGetValue(cobranca.TenantId, out var tenant)) continue;

            // A régua suspende quando o vencimento fica ANTES de hoje − carência,
            // ou seja, no dia seguinte ao último dia da carência.
            var suspensaoEm = cobranca.DueDate.Date.AddDays(DiasDeCarencia + 1);

            await EnviarUmaVezAsync(scope.ServiceProvider, tenant, AvisoSuspensao, cobranca.Id.ToString(),
                (email, t) => email.SendCobrancaAvisoSuspensaoAsync(
                    t.BillingEmail!, NomeDe(t), NomeDe(t), cobranca.Amount, cobranca.DueDate, suspensaoEm,
                    cobranca.PaymentUrl, UrlDaLoja(t.Slug, "/admin/assinatura")),
                ct);
        }
    }

    private async Task EnviarUmaVezAsync(
        IServiceProvider services, Tenant tenant, string tipo, string referencia,
        Func<IEmailService, Tenant, Task> enviar, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenant.BillingEmail))
        {
            _logger.LogWarning("Aviso '{Tipo}' do tenant {Slug} não enviado: loja sem e-mail de cobrança", tipo, tenant.Slug);
            return;
        }

        var catalog = services.GetRequiredService<CatalogDbContext>();

        if (await catalog.TenantBillingNotices.AnyAsync(
                n => n.TenantId == tenant.Id && n.Kind == tipo && n.Reference == referencia, ct))
            return;

        var registro = new TenantBillingNotice
        {
            TenantId  = tenant.Id,
            Kind      = tipo,
            Reference = referencia,
            SentTo    = tenant.BillingEmail,
        };
        catalog.TenantBillingNotices.Add(registro);

        try
        {
            await catalog.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Outro chamador registrou o mesmo aviso entre a consulta e o insert.
            catalog.Entry(registro).State = EntityState.Detached;
            return;
        }

        // "Entregue ao EmailService", não "enviado": sem SMTP configurado ele só
        // registra um aviso próprio no log e segue.
        await enviar(services.GetRequiredService<IEmailService>(), tenant);
        _logger.LogInformation("Aviso de cobrança '{Tipo}' do tenant {Slug} registrado e entregue ao EmailService", tipo, tenant.Slug);
    }

    private IServiceScope CriarEscopoDaPlataforma()
    {
        var scope = _scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>()
            .Set(TenantConstants.TenantZeroId, TenantConstants.TenantZeroSchema, ["fiscal"]);
        return scope;
    }

    private static string NomeDe(Tenant tenant) => tenant.DisplayName ?? tenant.Slug;

    /// <summary>Endereço da loja no subdomínio dela, com o esquema e a porta do
    /// AppUrl (em desenvolvimento, http://loja.localhost:3000).</summary>
    internal string UrlDaLoja(string slug, string caminho)
    {
        var appUrl = _config["SmtpSettings:AppUrl"];
        var raiz   = _config["Multitenancy:RootDomain"];

        if (!Uri.TryCreate(appUrl, UriKind.Absolute, out var uri))
            uri = new Uri("https://3esysten.com.br");

        var dominio = string.IsNullOrWhiteSpace(raiz) ? uri.Host : raiz;
        var porta   = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";

        return $"{uri.Scheme}://{slug}.{dominio}{porta}{caminho}";
    }
}
