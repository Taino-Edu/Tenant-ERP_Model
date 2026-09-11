// =============================================================================
// TenantSignupService.cs — A loja nasce pelas mãos do próprio lojista.
//
// Antes, "Começar meu teste grátis" gravava um lead e respondia "a equipe vai
// falar com você em breve": o teste prometido dependia de alguém da plataforma
// provisionar a loja à mão e mandar a senha por fora. Quem se interessava às
// dez da noite ia dormir e esquecia.
//
// O fluxo tem duas etapas, e a divisão é o que torna seguro deixar qualquer um
// criar uma loja sem login:
//
//   1. Solicitar — valida endereço e plano, guarda o pedido (só hashes) e manda
//      o link. Não cria schema, não roda migration, não gasta nada: um e-mail
//      digitado errado, ou um script disparando pedidos, gera no máximo linhas
//      que expiram em 24 horas.
//   2. Confirmar — quem clicou no link provou que lê aquela caixa de entrada.
//      Só então o provisionamento de sempre roda (o mesmo do painel da
//      plataforma, com os 15 dias grátis de ApplyCommercialTerms), e a resposta
//      traz um LoginRedirectTicket para a pessoa cair logada no subdomínio da
//      loja nova.
//
// Os tetos diários (Signup:MaxSolicitacoesPorDia e Signup:MaxLojasPorDia) são
// disjuntor, não regra comercial: cada loja confirmada é um schema com ~60
// tabelas no mesmo Postgres de todo mundo, e o rate limit por IP não segura
// quem distribui os pedidos entre muitos endereços.
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public sealed class TenantSignupService : ITenantSignupService
{
    internal static readonly TimeSpan ValidadeDoLink = TimeSpan.FromHours(24);

    /// <summary>A resposta da confirmação redireciona na hora; dois minutos cobrem
    /// celular lento sem deixar um ticket de entrada circulando por muito tempo.</summary>
    internal static readonly TimeSpan ValidadeDoTicketDeEntrada = TimeSpan.FromMinutes(2);

    /// <summary>Provisionar leva segundos. Reserva mais velha que isto é de um
    /// processo que caiu no meio, e o próximo clique pode retomar.</summary>
    private static readonly TimeSpan ReservaDaConfirmacao = TimeSpan.FromMinutes(5);

    /// <summary>O plano em destaque no site. É o que o Tenant já assume quando
    /// ninguém informa plano, então o teste grátis mostra o mesmo produto que o
    /// painel da plataforma criaria.</summary>
    internal const string PlanoPadrao = "Rio";

    /// <summary>Mais estrito que o SlugPattern do provisionamento: sem hífen no
    /// começo ou no fim. O painel da plataforma continua podendo criar esses;
    /// quem digita no site não precisa de um endereço que parece erro.</summary>
    private static readonly Regex SlugPublico = new(@"^[a-z0-9](?:[a-z0-9-]{0,18}[a-z0-9])?$", RegexOptions.Compiled);

    private const string MensagemSlugInvalido =
        "Use de 1 a 20 caracteres: letras minúsculas, números e hífen, sem hífen no começo ou no fim.";

    /// <summary>Endereços que um desconhecido não pode ocupar. Vão além dos
    /// reservados do provisionamento porque o painel é operado por quem sabe o
    /// que está fazendo, e o site não: "dev-swagger" é um host que o nginx já
    /// serve, e "plataforma" ou "suporte" na mão de terceiro viram phishing com
    /// o nosso domínio.</summary>
    internal static readonly HashSet<string> SlugsReservados = new(StringComparer.OrdinalIgnoreCase)
    {
        "public", "www", "api", "admin", "app", "mail", "email", "smtp", "ftp",
        "dev-swagger", "swagger", "plataforma", "contador", "cliente", "clientes",
        "parceiros", "suporte", "ajuda", "status", "blog", "docs", "painel",
        "login", "entrar", "cadastro", "criar-loja", "octus", "3esysten",
    };

    private readonly CatalogDbContext _catalog;
    private readonly ITenantProvisioningService _provisioning;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;
    private readonly ILogger<TenantSignupService> _logger;

    public TenantSignupService(
        CatalogDbContext catalog,
        ITenantProvisioningService provisioning,
        IEmailService email,
        IConfiguration config,
        IHostEnvironment env,
        ILogger<TenantSignupService> logger)
    {
        _catalog      = catalog;
        _provisioning = provisioning;
        _email        = email;
        _config       = config;
        _env          = env;
        _logger       = logger;
    }

    private int MaxSolicitacoesPorDia => _config.GetValue("Signup:MaxSolicitacoesPorDia", 200);
    private int MaxLojasPorDia        => _config.GetValue("Signup:MaxLojasPorDia", 30);

    // ── Endereço ─────────────────────────────────────────────────────────────

    public async Task<DisponibilidadeSlugDto> VerificarSlugAsync(string? slug, CancellationToken ct = default)
    {
        var normalizado = NormalizarSlug(slug);
        var motivo = await MotivoDeIndisponibilidadeAsync(normalizado, emailDoPedido: null, ct);

        return new DisponibilidadeSlugDto
        {
            Slug       = normalizado,
            Disponivel = motivo is null,
            Motivo     = motivo,
            Sugestao   = motivo is not null && SlugPublico.IsMatch(normalizado)
                ? await SugerirSlugAsync(normalizado, emailDoPedido: null, ct)
                : null,
        };
    }

    // ── Etapa 1: pedido ──────────────────────────────────────────────────────

    public async Task<SolicitarLojaResultado> SolicitarAsync(SolicitarLojaRequest request, CancellationToken ct = default)
    {
        var slug  = NormalizarSlug(request.Slug);
        var email = request.Email.Trim().ToLowerInvariant();

        if (!SlugPublico.IsMatch(slug))
            return new(SolicitacaoStatus.SlugInvalido, MensagemSlugInvalido);

        var plano = PlanoCanonico(request.Plano);
        if (plano is null)
            return new(SolicitacaoStatus.PlanoInvalido, "Plano desconhecido. Escolha Lagoa, Rio ou Mar.");

        var motivo = await MotivoDeIndisponibilidadeAsync(slug, email, ct);
        if (motivo is not null)
            return new(SolicitacaoStatus.SlugIndisponivel, motivo, await SugerirSlugAsync(slug, email, ct));

        var agora = DateTime.UtcNow;
        if (await _catalog.TenantSignups.CountAsync(s => s.CreatedAt > agora.AddDays(-1), ct) >= MaxSolicitacoesPorDia)
        {
            _logger.LogWarning("Teto diário de pedidos de loja atingido ({Max}). Pedido para '{Slug}' recusado.",
                MaxSolicitacoesPorDia, slug);
            return new(SolicitacaoStatus.LimiteDiario,
                "Recebemos muitos cadastros hoje. Tente de novo mais tarde ou fale com a gente pelo WhatsApp.");
        }

        // Pedir de novo com o mesmo e-mail substitui o pedido anterior (é o
        // "corrigir os dados" da tela de confirmação). Confirmação já em curso
        // fica intocada. Na mesma passada vai o lixo: pedido que ninguém
        // confirmou e venceu há uma semana é dado pessoal sem finalidade.
        await _catalog.TenantSignups
            .Where(s => s.ConfirmedAt == null && s.ConfirmationStartedAt == null
                     && (s.Email == email || s.ExpiresAt < agora.AddDays(-7)))
            .ExecuteDeleteAsync(ct);

        var token = GerarToken();
        var signup = new TenantSignup
        {
            TokenHash    = HashDoToken(token),
            Email        = email,
            OwnerName    = request.NomeResponsavel.Trim(),
            StoreName    = request.NomeLoja.Trim(),
            Slug         = slug,
            PlanName     = plano,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Senha),
            Phone        = string.IsNullOrWhiteSpace(request.WhatsApp) ? null : request.WhatsApp.Trim(),
            PrivacyNoticeVersion        = request.PrivacyNoticeVersion.Trim(),
            PrivacyNoticeAcknowledgedAt = agora,
            CreatedAt    = agora,
            ExpiresAt    = agora + ValidadeDoLink,
        };
        _catalog.TenantSignups.Add(signup);
        await _catalog.SaveChangesAsync(ct);

        var link = $"{_email.AppUrl}/criar-loja/confirmar?token={Uri.EscapeDataString(token)}";

        // Em Development não há SMTP: sem isto não haveria como testar o fluxo
        // local sem abrir o banco. Em qualquer outro ambiente o link é uma
        // credencial e não entra em log.
        if (_env.IsDevelopment())
            _logger.LogInformation("Pedido de loja '{Slug}' aguardando confirmação. Link (só em Development): {Link}",
                slug, link);

        try
        {
            // requireDelivery fora de Development: sem SMTP em produção o envio
            // "funcionaria" em silêncio e a pessoa esperaria um e-mail que nunca
            // sai. Melhor dizer na hora que não deu.
            await _email.SendTenantSignupConfirmationAsync(
                email, signup.OwnerName, signup.StoreName, EnderecoDaLoja(slug), link, signup.ExpiresAt,
                requireDelivery: !_env.IsDevelopment());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar a confirmação do pedido de loja '{Slug}'.", slug);
            _catalog.TenantSignups.Remove(signup);
            await _catalog.SaveChangesAsync(CancellationToken.None);
            return new(SolicitacaoStatus.EmailFalhou,
                "Não conseguimos enviar o e-mail de confirmação agora. Tente de novo em alguns minutos.");
        }

        return new(SolicitacaoStatus.Enviada);
    }

    // ── Etapa 2: confirmação ─────────────────────────────────────────────────

    public async Task<ConfirmarLojaResultado> ConfirmarAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new(ConfirmacaoStatus.TokenInvalido);

        var hash = HashDoToken(token.Trim());
        var signup = await _catalog.TenantSignups.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == hash, ct);

        if (signup is null)
            return new(ConfirmacaoStatus.TokenInvalido);
        if (signup.ConfirmedAt is not null)
            return new(ConfirmacaoStatus.JaConfirmada, signup.Slug);

        var agora = DateTime.UtcNow;
        if (signup.ExpiresAt < agora)
            return new(ConfirmacaoStatus.Expirado);

        if (await _catalog.TenantSignups.CountAsync(s => s.ConfirmedAt > agora.AddDays(-1), ct) >= MaxLojasPorDia)
        {
            _logger.LogWarning("Teto diário de lojas criadas pelo site atingido ({Max}). Confirmação de '{Slug}' adiada.",
                MaxLojasPorDia, signup.Slug);
            return new(ConfirmacaoStatus.LimiteDiario, signup.Slug,
                Mensagem: "Muitas lojas foram criadas hoje. Seu cadastro continua guardado: tente de novo amanhã pelo mesmo link.");
        }

        var limiteDaReserva = agora - ReservaDaConfirmacao;
        var reservou = await _catalog.TenantSignups
            .Where(s => s.Id == signup.Id && s.ConfirmedAt == null
                     && (s.ConfirmationStartedAt == null || s.ConfirmationStartedAt < limiteDaReserva))
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.ConfirmationStartedAt, agora), ct);

        if (reservou == 0)
            return new(ConfirmacaoStatus.EmAndamento, signup.Slug);

        // Daqui em diante nada usa o ct: se o navegador fechar no meio do
        // provisionamento, a loja ainda precisa terminar de nascer e ser marcada,
        // senão o próximo clique tentaria criar o mesmo schema de novo.
        var (modulos, maxUsuarios) = TenantProvisioningService.RecursosDosPlanos[signup.PlanName];
        var adminId = Guid.NewGuid();
        Tenant tenant;
        try
        {
            tenant = await _provisioning.ProvisionAsync(
                signup.Slug, signup.Email, adminPassword: null,
                enabledModules: modulos, planName: signup.PlanName, maxUsers: maxUsuarios,
                kind: TenantKind.Native, isPubliclyListed: false,
                owner: new TenantOwnerProfile(
                    adminId, signup.OwnerName,
                    signup.PasswordHash ?? throw new InvalidOperationException("Pedido sem senha antes da confirmação."),
                    signup.StoreName));
        }
        catch (Exception ex)
        {
            await _catalog.TenantSignups
                .Where(s => s.Id == signup.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.ConfirmationStartedAt, (DateTime?)null), CancellationToken.None);

            if (await _catalog.Tenants.AnyAsync(t => t.Slug == signup.Slug, CancellationToken.None))
                return new(ConfirmacaoStatus.SlugIndisponivel, signup.Slug,
                    Mensagem: "Outra loja confirmou esse endereço antes. Faça o cadastro de novo escolhendo outro.");

            _logger.LogError(ex, "Falha ao criar a loja '{Slug}' pedida pelo site.", signup.Slug);
            return new(ConfirmacaoStatus.Falhou, signup.Slug,
                Mensagem: "Não conseguimos criar sua loja agora. Seu cadastro continua guardado: tente de novo em alguns minutos.");
        }

        await _catalog.TenantSignups
            .Where(s => s.Id == signup.Id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.ConfirmedAt, DateTime.UtcNow)
                .SetProperty(s => s.TenantId, tenant.Id)
                .SetProperty(s => s.PasswordHash, (string?)null), CancellationToken.None);

        var ticket = new LoginRedirectTicket
        {
            Ticket     = GerarToken(),
            TargetKind = LoginRedirectTargetKind.Tenant,
            AccountId  = adminId,
            TenantId   = tenant.Id,
            TenantSlug = tenant.Slug,
            ExpiresAt  = DateTime.UtcNow + ValidadeDoTicketDeEntrada,
        };
        _catalog.LoginRedirectTickets.Add(ticket);
        await _catalog.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation("Loja '{Slug}' criada pelo próprio lojista no site (plano {Plano}).",
            tenant.Slug, signup.PlanName);

        return new(ConfirmacaoStatus.Criada, tenant.Slug, ticket.Ticket);
    }

    // ── Apoio ────────────────────────────────────────────────────────────────

    private static string NormalizarSlug(string? slug) => (slug ?? string.Empty).Trim().ToLowerInvariant();

    private static string? PlanoCanonico(string? plano)
    {
        if (string.IsNullOrWhiteSpace(plano)) return PlanoPadrao;
        return TenantProvisioningService.RecursosDosPlanos.Keys
            .FirstOrDefault(k => k.Equals(plano.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Null quando o endereço está livre. Pedido pendente do MESMO e-mail
    /// (emailDoPedido) não bloqueia: é a pessoa corrigindo o próprio cadastro.</summary>
    private async Task<string?> MotivoDeIndisponibilidadeAsync(string slug, string? emailDoPedido, CancellationToken ct)
    {
        if (!SlugPublico.IsMatch(slug))
            return MensagemSlugInvalido;
        if (SlugsReservados.Contains(slug))
            return "Esse endereço é reservado. Escolha outro.";
        if (await _catalog.Tenants.AnyAsync(t => t.Slug == slug, ct))
            return "Esse endereço já é de outra loja.";

        var agora = DateTime.UtcNow;
        var reservadoPorOutroPedido = await _catalog.TenantSignups.AnyAsync(s =>
            s.Slug == slug && s.ConfirmedAt == null && s.ExpiresAt > agora
            && (emailDoPedido == null || s.Email != emailDoPedido), ct);
        if (reservadoPorOutroPedido)
            return "Esse endereço está reservado por um cadastro que ainda não foi confirmado. Escolha outro.";

        return null;
    }

    private async Task<string?> SugerirSlugAsync(string slug, string? emailDoPedido, CancellationToken ct)
    {
        for (var n = 2; n <= 20; n++)
        {
            var sufixo = "-" + n;
            var candidato = slug[..Math.Min(slug.Length, 20 - sufixo.Length)].TrimEnd('-') + sufixo;
            if (await MotivoDeIndisponibilidadeAsync(candidato, emailDoPedido, ct) is null)
                return candidato;
        }
        return null;
    }

    private string EnderecoDaLoja(string slug)
    {
        var raiz = _config["Multitenancy:RootDomain"];
        return $"{slug}.{(string.IsNullOrWhiteSpace(raiz) ? "3esysten.com.br" : raiz)}";
    }

    private static string GerarToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    internal static string HashDoToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
