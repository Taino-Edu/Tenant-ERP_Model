// =============================================================================
// TenantSignupServiceTests.cs — Criação de loja pelo próprio lojista.
//
// O catálogo é real (Postgres, schema isolado), porque o que protege o fluxo
// são consultas e UPDATEs condicionais: a reserva da confirmação só prova
// alguma coisa contra um banco que de fato serializa as escritas. O
// provisionamento é simulado — criar schema e rodar ~60 migrations por teste
// não testaria nada deste serviço, só deixaria a suíte lenta.
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace CardGameStore.Tests.Services;

public sealed class TenantSignupServiceTests : IAsyncLifetime
{
    private const string Senha = "senha-forte-123";

    private readonly string _schema = TestDbFactory.IsolatedSchemaName("tenant_signup_service");
    private readonly Mock<ITenantProvisioningService> _provisioning = new();
    private readonly Mock<IEmailService> _email = new();
    private string? _linkEnviado;

    public async Task InitializeAsync()
    {
        TestDbFactory.ResetSchema(_schema);
        await using var db = NovoContexto();
        db.GetInfrastructure().GetRequiredService<IRelationalDatabaseCreator>().CreateTables();

        _email.SetupGet(e => e.AppUrl).Returns("https://3esysten.com.br");
        _email.Setup(e => e.SendTenantSignupConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Callback<string, string, string, string, string, DateTime, bool>((_, _, _, _, link, _, _) => _linkEnviado = link)
            .Returns(Task.CompletedTask);
    }

    public async Task DisposeAsync()
    {
        await using var cleanup = new NpgsqlConnection(TestDbFactory.ConnectionString);
        await cleanup.OpenAsync();
        await using var command = cleanup.CreateCommand();
        command.CommandText = $"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE";
        await command.ExecuteNonQueryAsync();
    }

    // ── Pedido ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Solicitar_GuardaSoOHashDoTokenEEnviaOLinkDeConfirmacao()
    {
        await using var db = NovoContexto();

        var resultado = await NovoServico(db).SolicitarAsync(Pedido());

        resultado.Status.Should().Be(SolicitacaoStatus.Enviada);
        _linkEnviado.Should().StartWith("https://3esysten.com.br/criar-loja/confirmar?token=");

        var signup = await db.TenantSignups.AsNoTracking().SingleAsync();
        signup.TokenHash.Should().Be(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(TokenDoLink()))),
            "o token só pode existir no e-mail; o banco guarda o hash");
        signup.Email.Should().Be("ana@example.com");
        signup.PlanName.Should().Be(TenantSignupService.PlanoPadrao);
        signup.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));

        _email.Verify(e => e.SendTenantSignupConfirmationAsync(
            "ana@example.com", "Ana Souza", "Empório da Ana", "emporio-da-ana.3esysten.com.br",
            It.IsAny<string>(), It.IsAny<DateTime>(), true), Times.Once);
        _provisioning.Invocations.Should().BeEmpty("pedido não confirmado não pode criar schema nenhum");
    }

    [Theory]
    [InlineData("-loja", SolicitacaoStatus.SlugInvalido)]
    [InlineData("loja_da_ana", SolicitacaoStatus.SlugInvalido)]
    [InlineData("loja-", SolicitacaoStatus.SlugInvalido)]
    [InlineData("dev-swagger", SolicitacaoStatus.SlugIndisponivel)]
    [InlineData("plataforma", SolicitacaoStatus.SlugIndisponivel)]
    public async Task Solicitar_RecusaEnderecoInvalidoOuReservado(string slug, SolicitacaoStatus esperado)
    {
        await using var db = NovoContexto();

        var resultado = await NovoServico(db).SolicitarAsync(Pedido(slug: slug));

        resultado.Status.Should().Be(esperado);
        (await db.TenantSignups.CountAsync()).Should().Be(0);
        _linkEnviado.Should().BeNull();
    }

    [Fact]
    public async Task Solicitar_EnderecoDeOutraLojaSugereAlternativa()
    {
        await using var db = NovoContexto();
        db.Tenants.Add(new Tenant { Slug = "emporio", SchemaName = "tenant_emporio" });
        await db.SaveChangesAsync();

        var resultado = await NovoServico(db).SolicitarAsync(Pedido(slug: "emporio"));

        resultado.Status.Should().Be(SolicitacaoStatus.SlugIndisponivel);
        resultado.SugestaoSlug.Should().Be("emporio-2");
    }

    [Fact]
    public async Task Solicitar_EnderecoPendenteFicaComQuemPediuPrimeiro_EQuemPediuPodeCorrigir()
    {
        await using var db = NovoContexto();
        var servico = NovoServico(db);

        (await servico.SolicitarAsync(Pedido())).Status.Should().Be(SolicitacaoStatus.Enviada);
        (await servico.SolicitarAsync(Pedido(email: "bia@example.com"))).Status
            .Should().Be(SolicitacaoStatus.SlugIndisponivel, "outra pessoa não pode tomar um endereço que está esperando confirmação");
        (await servico.SolicitarAsync(Pedido(email: "ANA@example.com "))).Status
            .Should().Be(SolicitacaoStatus.Enviada, "é a mesma pessoa corrigindo o próprio cadastro");

        (await db.TenantSignups.CountAsync()).Should().Be(1, "o pedido corrigido substitui o anterior em vez de acumular");
    }

    [Fact]
    public async Task Solicitar_FalhaNoEnvioNaoDeixaPedidoPendurado()
    {
        _email.Setup(e => e.SendTenantSignupConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("SMTP fora do ar"));
        await using var db = NovoContexto();

        var resultado = await NovoServico(db).SolicitarAsync(Pedido());

        resultado.Status.Should().Be(SolicitacaoStatus.EmailFalhou);
        (await db.TenantSignups.CountAsync()).Should().Be(0,
            "um pedido sem e-mail enviado só reservaria o endereço de alguém que nunca vai receber o link");
    }

    // ── Link ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task VerificarLink_MostraALojaSemReservarNemCriarNada()
    {
        await using var db = NovoContexto();
        var servico = NovoServico(db);
        await servico.SolicitarAsync(Pedido());

        var resultado = await servico.VerificarLinkAsync(TokenDoLink());

        resultado.Should().Be(new LinkDeLojaResultado(LinkDeLojaStatus.Valido, "emporio-da-ana", "Empório da Ana"));
        (await db.TenantSignups.AsNoTracking().SingleAsync()).ConfirmationStartedAt
            .Should().BeNull("abrir a página do link não pode travar a confirmação de ninguém");
        _provisioning.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task VerificarLink_RecusaTokenDesconhecidoExpiradoOuJaUsado()
    {
        ProvisionamentoCriaLoja();
        await using var db = NovoContexto();
        var servico = NovoServico(db);
        await servico.SolicitarAsync(Pedido());

        (await servico.VerificarLinkAsync("token-que-nunca-existiu"))
            .Should().Be(new LinkDeLojaResultado(LinkDeLojaStatus.TokenInvalido));

        await db.TenantSignups.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        (await servico.VerificarLinkAsync(TokenDoLink())).Status.Should().Be(LinkDeLojaStatus.Expirado);

        await db.TenantSignups.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddHours(1)));
        await servico.ConfirmarAsync(TokenDoLink(), Senha);
        (await servico.VerificarLinkAsync(TokenDoLink()))
            .Should().Be(new LinkDeLojaResultado(LinkDeLojaStatus.JaConfirmada, "emporio-da-ana"));
    }

    // ── Confirmação ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Confirmar_CriaALojaUmaVezComODonoEEntraDireto()
    {
        ProvisionamentoCriaLoja();
        await using var db = NovoContexto();
        var servico = NovoServico(db);
        await servico.SolicitarAsync(Pedido());

        var resultado = await servico.ConfirmarAsync(TokenDoLink(), Senha);

        resultado.Status.Should().Be(ConfirmacaoStatus.Criada);
        resultado.Slug.Should().Be("emporio-da-ana");

        var (modulosDoRio, maxUsuariosDoRio) = TenantProvisioningService.RecursosDosPlanos["Rio"];
        var chamada = _provisioning.Invocations.Should().ContainSingle().Subject;
        chamada.Arguments[0].Should().Be("emporio-da-ana");
        chamada.Arguments[1].Should().Be("ana@example.com");
        chamada.Arguments[2].Should().BeNull("a senha em texto puro não existe mais a esta altura");
        ((string[])chamada.Arguments[3]).Should().BeEquivalentTo(modulosDoRio);
        chamada.Arguments[4].Should().Be("Rio");
        chamada.Arguments[5].Should().Be(maxUsuariosDoRio);
        var dono = (TenantOwnerProfile)chamada.Arguments[8];
        dono.Name.Should().Be("Ana Souza");
        dono.StoreName.Should().Be("Empório da Ana");
        BCrypt.Net.BCrypt.Verify(Senha, dono.PasswordHash).Should().BeTrue();

        var ticket = await db.LoginRedirectTickets.AsNoTracking().SingleAsync();
        ticket.Ticket.Should().Be(resultado.Ticket);
        ticket.TargetKind.Should().Be(LoginRedirectTargetKind.Tenant);
        ticket.TenantSlug.Should().Be("emporio-da-ana");
        ticket.AccountId.Should().Be(dono.UserId, "o ticket entra como o admin que acabou de nascer");
        ticket.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(2), TimeSpan.FromSeconds(30));

        var signup = await db.TenantSignups.AsNoTracking().SingleAsync();
        signup.ConfirmedAt.Should().NotBeNull();
        signup.TenantId.Should().Be(ticket.TenantId!.Value);

        var repetido = await servico.ConfirmarAsync(TokenDoLink(), "outra-senha-qualquer");
        repetido.Status.Should().Be(ConfirmacaoStatus.JaConfirmada);
        repetido.Ticket.Should().BeNull("clicar de novo no link não pode virar um jeito de entrar na loja sem senha");
        _provisioning.Invocations.Should().HaveCount(1, "confirmar de novo não pode trocar a senha de uma loja que já existe");
    }

    [Fact]
    public async Task Confirmar_PedidoFeitoComOEmailDeOutraPessoaNaoDefineASenhaDaLoja()
    {
        // O ataque que motivou a senha na confirmação: alguém pede a loja com o
        // e-mail de um lojista, o lojista recebe o link legítimo e clica. A senha
        // do admin tem que ser a de quem abriu o e-mail, e não existir antes disso.
        ProvisionamentoCriaLoja();
        await using var db = NovoContexto();
        var servico = NovoServico(db);
        await servico.SolicitarAsync(Pedido());

        var colunas = await db.Database.SqlQueryRaw<string>(
                "SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = {0} AND table_name = 'tenant_signups'",
                _schema)
            .ToListAsync();
        colunas.Should().NotContain(c => c.Contains("password"), "o pedido anônimo não pode carregar senha nenhuma");

        await servico.ConfirmarAsync(TokenDoLink(), "senha-de-quem-abriu-o-email");

        var dono = (TenantOwnerProfile)_provisioning.Invocations.Should().ContainSingle().Subject.Arguments[8];
        BCrypt.Net.BCrypt.Verify("senha-de-quem-abriu-o-email", dono.PasswordHash).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curta12")]
    public async Task Confirmar_SenhaForaDaRegraNaoReservaNemCriaLoja(string? senha)
    {
        ProvisionamentoCriaLoja();
        await using var db = NovoContexto();
        var servico = NovoServico(db);
        await servico.SolicitarAsync(Pedido());

        var resultado = await servico.ConfirmarAsync(TokenDoLink(), senha);

        resultado.Status.Should().Be(ConfirmacaoStatus.SenhaInvalida);
        _provisioning.Invocations.Should().BeEmpty();
        (await db.TenantSignups.AsNoTracking().SingleAsync()).ConfirmationStartedAt
            .Should().BeNull("a pessoa corrige a senha e confirma de novo pelo mesmo link");
    }

    [Fact]
    public async Task Confirmar_SenhaAcimaDoQueOBcryptGuardaERecusada()
    {
        ProvisionamentoCriaLoja();
        await using var db = NovoContexto();
        var servico = NovoServico(db);
        await servico.SolicitarAsync(Pedido());

        var resultado = await servico.ConfirmarAsync(TokenDoLink(), new string('a', TenantSignupSenha.Maximo + 1));

        resultado.Status.Should().Be(ConfirmacaoStatus.SenhaInvalida);
        _provisioning.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirmar_DocumentoDoCadastroVaiParaALojaESaiDoPedido()
    {
        ProvisionamentoCriaLoja();
        await using var db = NovoContexto();
        var servico = NovoServico(db);
        var pedido = Pedido();
        pedido.Documento = "11.222.333/0001-81";
        await servico.SolicitarAsync(pedido);

        (await db.TenantSignups.AsNoTracking().SingleAsync()).BillingDocument.Should().Be("11222333000181");

        await servico.ConfirmarAsync(TokenDoLink(), Senha);

        var dono = (TenantOwnerProfile)_provisioning.Invocations.Should().ContainSingle().Subject.Arguments[8];
        dono.BillingDocument.Should().Be("11222333000181", "quem informou o documento recebe a fatura sem passar pela Assinatura");
        (await db.TenantSignups.AsNoTracking().SingleAsync()).BillingDocument.Should().BeNull(
            "o documento já está na loja; a cópia no pedido seria dado pessoal guardado duas vezes");
    }

    [Fact]
    public async Task Confirmar_LinkExpiradoNaoCriaLoja()
    {
        ProvisionamentoCriaLoja();
        await using var db = NovoContexto();
        var servico = NovoServico(db);
        await servico.SolicitarAsync(Pedido());
        await db.TenantSignups.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));

        var resultado = await servico.ConfirmarAsync(TokenDoLink(), Senha);

        resultado.Status.Should().Be(ConfirmacaoStatus.Expirado);
        _provisioning.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirmar_TokenDesconhecidoNaoRevelaNada()
    {
        await using var db = NovoContexto();

        var resultado = await NovoServico(db).ConfirmarAsync("token-que-nunca-existiu", Senha);

        resultado.Should().Be(new ConfirmarLojaResultado(ConfirmacaoStatus.TokenInvalido));
    }

    [Fact]
    public async Task Confirmar_DoisCliquesAoMesmoTempoCriamUmaLojaSo()
    {
        ProvisionamentoCriaLoja(demora: TimeSpan.FromMilliseconds(500));
        await using (var pedido = NovoContexto())
            await NovoServico(pedido).SolicitarAsync(Pedido());
        var token = TokenDoLink();

        await using var db1 = NovoContexto();
        await using var db2 = NovoContexto();
        var resultados = await Task.WhenAll(
            NovoServico(db1).ConfirmarAsync(token, Senha),
            NovoServico(db2).ConfirmarAsync(token, Senha));

        resultados.Select(r => r.Status).Should().BeEquivalentTo(
            [ConfirmacaoStatus.Criada, ConfirmacaoStatus.EmAndamento]);
        _provisioning.Invocations.Should().HaveCount(1, "o segundo clique tentaria criar o mesmo schema de novo");
    }

    [Fact]
    public async Task Confirmar_RespeitaOTetoDiarioDeLojas()
    {
        ProvisionamentoCriaLoja();
        await using var db = NovoContexto();
        db.TenantSignups.Add(new TenantSignup
        {
            TokenHash = TenantSignupService.HashDoToken("outra-loja"), Email = "outra@example.com",
            OwnerName = "Outra", StoreName = "Outra", Slug = "outra", PlanName = "Rio",
            PrivacyNoticeVersion = "2.2", ExpiresAt = DateTime.UtcNow.AddHours(1), ConfirmedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var servico = NovoServico(db, maxLojasPorDia: 1);
        await servico.SolicitarAsync(Pedido());

        var resultado = await servico.ConfirmarAsync(TokenDoLink(), Senha);

        resultado.Status.Should().Be(ConfirmacaoStatus.LimiteDiario);
        _provisioning.Invocations.Should().BeEmpty();
        (await db.TenantSignups.AsNoTracking().SingleAsync(s => s.Slug == "emporio-da-ana")).ConfirmationStartedAt
            .Should().BeNull("o teto adia a loja; o mesmo link precisa funcionar depois");
    }

    [Fact]
    public async Task Confirmar_FalhaNoProvisionamentoLiberaParaTentarDeNovo()
    {
        _provisioning.Setup(p => p.ProvisionAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string[]?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<TenantKind>(), It.IsAny<bool>(),
                It.IsAny<TenantOwnerProfile?>()))
            .ThrowsAsync(new TimeoutException("Postgres lento"));
        await using var db = NovoContexto();
        var servico = NovoServico(db);
        await servico.SolicitarAsync(Pedido());

        var resultado = await servico.ConfirmarAsync(TokenDoLink(), Senha);

        resultado.Status.Should().Be(ConfirmacaoStatus.Falhou);
        var signup = await db.TenantSignups.AsNoTracking().SingleAsync();
        signup.ConfirmationStartedAt.Should().BeNull("sem liberar a reserva, o próximo clique esperaria 5 minutos à toa");
        signup.ConfirmedAt.Should().BeNull("o cadastro continua valendo para a próxima tentativa");
    }

    // ── Apoio ────────────────────────────────────────────────────────────────

    private CatalogDbContext NovoContexto()
    {
        var connection = new NpgsqlConnectionStringBuilder(TestDbFactory.ConnectionString) { SearchPath = _schema }.ConnectionString;
        return new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options);
    }

    private TenantSignupService NovoServico(CatalogDbContext db, int? maxLojasPorDia = null)
    {
        var configuracao = new Dictionary<string, string?> { ["Multitenancy:RootDomain"] = "3esysten.com.br" };
        if (maxLojasPorDia is not null) configuracao["Signup:MaxLojasPorDia"] = maxLojasPorDia.ToString();

        var ambiente = new Mock<IHostEnvironment>();
        ambiente.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        return new TenantSignupService(
            db, _provisioning.Object, _email.Object,
            new ConfigurationBuilder().AddInMemoryCollection(configuracao).Build(),
            ambiente.Object, NullLogger<TenantSignupService>.Instance);
    }

    private void ProvisionamentoCriaLoja(TimeSpan? demora = null) =>
        _provisioning.Setup(p => p.ProvisionAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string[]?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<TenantKind>(), It.IsAny<bool>(),
                It.IsAny<TenantOwnerProfile?>()))
            .Returns(async (string slug, string? _, string? _, string[]? modulos, string? plano,
                            int? _, TenantKind _, bool _, TenantOwnerProfile? _) =>
            {
                if (demora is not null) await Task.Delay(demora.Value);
                await using var db = NovoContexto();
                var tenant = new Tenant
                {
                    Slug = slug, SchemaName = "tenant_" + slug.Replace('-', '_'),
                    PlanName = plano ?? "Rio", EnabledModules = modulos ?? ["fiscal"],
                };
                db.Tenants.Add(tenant);
                await db.SaveChangesAsync();
                return tenant;
            });

    private static SolicitarLojaRequest Pedido(string slug = "emporio-da-ana", string email = "ana@example.com") => new()
    {
        NomeResponsavel = "Ana Souza", Email = email, NomeLoja = "Empório da Ana", Slug = slug,
        PrivacyNoticeAcknowledged = true, PrivacyNoticeVersion = "2.2",
    };

    private string TokenDoLink()
    {
        _linkEnviado.Should().NotBeNull("o pedido deveria ter enviado o link");
        return Uri.UnescapeDataString(_linkEnviado!.Split("token=")[1]);
    }
}
