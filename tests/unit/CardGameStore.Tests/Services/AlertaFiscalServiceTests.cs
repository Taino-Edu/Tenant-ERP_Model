// =============================================================================
// AlertaFiscalServiceTests.cs — CON-002: alertas por severidade e idade, com
// responsável e confirmação de resolução.
//
// O que estes testes protegem não é "o alerta aparece" — é o comportamento do
// modelo de reconciliação, que é onde os painéis de pendência costumam mentir:
//
//   • rodar de novo não duplica (a chave vem do fato, não da execução);
//   • o alerta some sozinho quando o fato some, e só então;
//   • confirmar resolução com o problema ainda de pé faz o alerta REABRIR —
//     uma nota rejeitada não deixa de estar rejeitada porque alguém clicou.
// =============================================================================

using System.Runtime.CompilerServices;
using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Services;

public class AlertaFiscalServiceTests
{
    private static AppDbContext CreateDb([CallerMemberName] string testName = "") =>
        TestDbFactory.Create($"{nameof(AlertaFiscalServiceTests)}_{testName}");

    private static AlertaFiscalService CreateService(AppDbContext db) =>
        new(db, new ConciliacaoFiscalService(db), NullLogger<AlertaFiscalService>.Instance);

    private static async Task<NotaFiscalEmitida> SeedNotaAsync(
        AppDbContext db, NotaFiscalStatus status, Action<NotaFiscalEmitida>? ajustar = null)
    {
        var nota = new NotaFiscalEmitida
        {
            Origem               = NotaFiscalOrigem.VendaAvulsa,
            VendaAvulsaId        = Guid.NewGuid(),
            Status               = status,
            Serie                = 1,
            Numero               = 500,
            ValorTotalEmCentavos = 15000,
            CreatedAt            = DateTime.UtcNow.AddMinutes(-10),
        };
        ajustar?.Invoke(nota);
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();
        return nota;
    }

    /// <summary>
    /// Loja aparelhada para emitir. O certificado é o que separa "tem o módulo
    /// fiscal" (que todo tenant tem por padrão) de "usa o fiscal" — e só na
    /// segunda existe expectativa de que uma venda tenha documento.
    /// </summary>
    private static async Task SeedLojaQueEmiteAsync(AppDbContext db)
    {
        db.FiscalConfigs.Add(new FiscalConfig
        {
            Cnpj                    = "12345678000195",
            RazaoSocial             = "Loja Fiscal LTDA",
            Uf                      = "SP",
            CertificadoPfxEncrypted = "conteudo-irrelevante-para-o-teste",
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedAdminAsync(AppDbContext db, bool ativo = true)
    {
        var admin = new User
        {
            Id = Guid.NewGuid(), Name = "Admin Fiscal", Role = UserRole.Admin, IsActive = ativo,
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        return admin.Id;
    }

    // ── Canal externo (CON-002, item 3) ───────────────────────────────────────

    [Fact]
    public async Task Sincronizar_AlertaCritico_MandaEmailParaAdminsComEndereco()
    {
        // Notificação in-app só é vista por quem abre o painel. Um resultado
        // incerto às 19h de sábado não pode esperar segunda.
        using var db = CreateDb();
        var admin = new User
        {
            Id = Guid.NewGuid(), Name = "Dona da Loja", Role = UserRole.Admin,
            IsActive = true, Email = "dona@loja.teste",
        };
        var semEmail = new User
        {
            Id = Guid.NewGuid(), Name = "Operador Sem Email", Role = UserRole.Admin, IsActive = true,
        };
        db.Users.AddRange(admin, semEmail);
        await db.SaveChangesAsync();
        await SeedNotaAsync(db, NotaFiscalStatus.ResultadoIncerto, n =>
            n.ResultadoIncertoEm = DateTime.UtcNow.AddMinutes(-1));

        var email = new Mock<IEmailService>();
        var service = new AlertaFiscalService(
            db, new ConciliacaoFiscalService(db), NullLogger<AlertaFiscalService>.Instance, email.Object);

        await service.SincronizarAsync();

        email.Verify(e => e.SendAlertaFiscalCriticoAsync(
            "dona@loja.teste", "Dona da Loja", It.IsAny<string>(), It.IsAny<string>(), 1), Times.Once);
        email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sincronizar_SegundoCiclo_NaoReenviaEmailDoMesmoAlerta()
    {
        // A deduplicação vale para o canal externo também: sem isso, o mesmo
        // resultado incerto geraria um e-mail a cada 15 minutos até alguém
        // resolver — e o admin criaria uma regra de filtro para o remetente.
        using var db = CreateDb();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(), Name = "Dona", Role = UserRole.Admin,
            IsActive = true, Email = "dona@loja.teste",
        });
        await db.SaveChangesAsync();
        await SeedNotaAsync(db, NotaFiscalStatus.ResultadoIncerto, n =>
            n.ResultadoIncertoEm = DateTime.UtcNow.AddMinutes(-1));

        var email = new Mock<IEmailService>();
        var service = new AlertaFiscalService(
            db, new ConciliacaoFiscalService(db), NullLogger<AlertaFiscalService>.Instance, email.Object);

        await service.SincronizarAsync();
        await service.SincronizarAsync();

        email.Verify(e => e.SendAlertaFiscalCriticoAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Once, "o fato continua o mesmo — só a primeira detecção notifica");
    }

    [Fact]
    public async Task Sincronizar_SmtpForaDoAr_NaoImpedeOAlertaDeSerGravado()
    {
        // O painel é a fonte do alerta; o e-mail é reforço. Deixar a falha de
        // SMTP derrubar a sincronização apagaria a pendência inteira.
        using var db = CreateDb();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(), Name = "Dona", Role = UserRole.Admin,
            IsActive = true, Email = "dona@loja.teste",
        });
        await db.SaveChangesAsync();
        await SeedNotaAsync(db, NotaFiscalStatus.ResultadoIncerto, n =>
            n.ResultadoIncertoEm = DateTime.UtcNow.AddMinutes(-1));

        var email = new Mock<IEmailService>();
        email.Setup(e => e.SendAlertaFiscalCriticoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("SMTP indisponível"));
        var service = new AlertaFiscalService(
            db, new ConciliacaoFiscalService(db), NullLogger<AlertaFiscalService>.Instance, email.Object);

        var act = async () => await service.SincronizarAsync();

        await act.Should().NotThrowAsync();
        db.AlertasFiscais.Should().ContainSingle();
    }

    // ── Quem é cobrado (e quem não é) ─────────────────────────────────────────

    [Fact]
    public async Task Sincronizar_LojaSemCertificado_NaoCobraVendaSemDocumento()
    {
        // O módulo fiscal vem habilitado por padrão em TODO tenant, então o
        // reconciliador roda para lojas que nunca optaram por emitir nota. Sem
        // esta separação, cada venda dos últimos 7 dias viraria um alerta por
        // dia — escalando para Alta em três — no painel de quem só usa o PDV.
        using var db = CreateDb();
        var ontemBr = BrazilTime.NowBr().Date.AddDays(-1);
        var meioDiaUtc = BrazilTime.DateToUtcStart(ontemBr).AddHours(15);

        var user = new User { Id = Guid.NewGuid(), Name = "Cliente", Role = UserRole.Customer };
        db.Users.Add(user);
        db.Comandas.Add(new Comanda
        {
            Id = Guid.NewGuid(), UserId = user.Id, Status = ComandaStatus.Fechada,
            ClosedAt = meioDiaUtc, TotalInCents = 9900, PaymentMethod = PaymentMethod.Dinheiro,
        });
        await db.SaveChangesAsync();

        await CreateService(db).SincronizarAsync();

        db.AlertasFiscais.Where(a => a.Tipo == TipoAlertaFiscal.VendaSemDocumento)
            .Should().BeEmpty("sem certificado nenhuma venda deveria ter documento — não há o que cobrar");
    }

    [Fact]
    public async Task Sincronizar_LojaSemCertificado_AindaCobraNotaComProblema()
    {
        // O contrário também precisa valer: se existe nota rejeitada, a loja
        // emitiu em algum momento e o problema é real, mesmo que o certificado
        // tenha sido removido depois.
        using var db = CreateDb();
        await SeedNotaAsync(db, NotaFiscalStatus.Rejeitada, n =>
            n.MotivoRejeicao = "Rejeicao 999 - teste");

        await CreateService(db).SincronizarAsync();

        db.AlertasFiscais.Where(a => a.Tipo == TipoAlertaFiscal.NotaRejeitada)
            .Should().ContainSingle("pendência que nasce de uma nota não depende de expectativa nenhuma");
    }

    // ── Detecção e severidade ─────────────────────────────────────────────────

    [Fact]
    public async Task Sincronizar_ResultadoIncerto_AlertaCriticoDesdeOPrimeiroCiclo()
    {
        using var db = CreateDb();
        await SeedNotaAsync(db, NotaFiscalStatus.ResultadoIncerto, n =>
        {
            n.ChaveAcesso        = new string('9', 44);
            n.ResultadoIncertoEm = DateTime.UtcNow.AddMinutes(-2);
        });

        await CreateService(db).SincronizarAsync();

        var alerta = await db.AlertasFiscais.SingleAsync();
        alerta.Tipo.Should().Be(TipoAlertaFiscal.ResultadoIncerto);
        alerta.Severidade.Should().Be(SeveridadeAlertaFiscal.Critica,
            "o risco de documento duplicado já está no máximo no primeiro segundo — não cresce com o tempo");
        alerta.Detalhe.Should().Contain("Não emita outra nota",
            "o alerta precisa dizer o que fazer, e aqui o que fazer é não agir");
    }

    [Fact]
    public async Task Sincronizar_ContingenciaRecente_EhAltaENaoCritica()
    {
        using var db = CreateDb();
        await SeedNotaAsync(db, NotaFiscalStatus.AutorizadaContingencia, n =>
            n.DhContingencia = DateTime.UtcNow.AddHours(-2));

        await CreateService(db).SincronizarAsync();

        var alerta = await db.AlertasFiscais.SingleAsync();
        alerta.Tipo.Should().Be(TipoAlertaFiscal.ContingenciaPendente);
        alerta.Severidade.Should().Be(SeveridadeAlertaFiscal.Alta);
        alerta.Detalhe.Should().Contain("Restam cerca de 21h");
    }

    [Fact]
    public async Task Sincronizar_ContingenciaPertoDoPrazoLegal_EscalaParaCritica()
    {
        using var db = CreateDb();
        var nota = await SeedNotaAsync(db, NotaFiscalStatus.AutorizadaContingencia, n =>
            n.DhContingencia = DateTime.UtcNow.AddHours(-2));
        var service = CreateService(db);

        await service.SincronizarAsync();
        (await db.AlertasFiscais.SingleAsync()).Severidade.Should().Be(SeveridadeAlertaFiscal.Alta);

        // O tempo passa: a mesma pendência agora está perto de vencer o prazo de 24h.
        nota.DhContingencia = DateTime.UtcNow.AddHours(-21);
        await db.SaveChangesAsync();
        await service.SincronizarAsync();

        var alerta = await db.AlertasFiscais.SingleAsync();
        alerta.Severidade.Should().Be(SeveridadeAlertaFiscal.Critica, "a idade é que define a gravidade aqui");
        alerta.Ocorrencias.Should().Be(2, "é a mesma pendência reconfirmada, não uma nova");
    }

    [Fact]
    public async Task Sincronizar_ContingenciaVencida_DizQueAVendaEstaSemDocumentoValido()
    {
        using var db = CreateDb();
        await SeedNotaAsync(db, NotaFiscalStatus.AutorizadaContingencia, n =>
            n.DhContingencia = DateTime.UtcNow.AddHours(-30));

        await CreateService(db).SincronizarAsync();

        var alerta = await db.AlertasFiscais.SingleAsync();
        alerta.Severidade.Should().Be(SeveridadeAlertaFiscal.Critica);
        alerta.Detalhe.Should().Contain("passou do prazo legal");
        alerta.Detalhe.Should().Contain("contador");
    }

    [Fact]
    public async Task Sincronizar_NotaRejeitada_TrazMotivoEAcaoSugerida()
    {
        using var db = CreateDb();
        await SeedNotaAsync(db, NotaFiscalStatus.Rejeitada, n =>
            n.MotivoRejeicao = "Rejeicao: Informado NCM inexistente");

        await CreateService(db).SincronizarAsync();

        var alerta = await db.AlertasFiscais.SingleAsync();
        alerta.Tipo.Should().Be(TipoAlertaFiscal.NotaRejeitada);
        alerta.Detalhe.Should().Contain("NCM inexistente");
        alerta.Detalhe.Should().Contain("inutilize a faixa", "o plano exige motivo E ação sugerida");
    }

    [Fact]
    public async Task Sincronizar_NotaRejeitadaComNumeroJaInutilizado_NaoAlerta()
    {
        // Número inutilizado é assunto encerrado: a sequência está íntegra e não
        // há nada a fazer. Cobrar isso todo ciclo seria ruído permanente.
        using var db = CreateDb();
        await SeedNotaAsync(db, NotaFiscalStatus.Rejeitada, n =>
        {
            n.MotivoRejeicao        = "Rejeicao qualquer";
            n.InutilizadoEm         = DateTime.UtcNow.AddHours(-1);
            n.ProtocoloInutilizacao = "135260000000001";
        });

        await CreateService(db).SincronizarAsync();

        (await db.AlertasFiscais.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sincronizar_NotaAutorizada_NaoGeraPendencia()
    {
        using var db = CreateDb();
        await SeedNotaAsync(db, NotaFiscalStatus.Autorizada, n => n.Protocolo = "135260000000001");

        await CreateService(db).SincronizarAsync();

        (await db.AlertasFiscais.CountAsync()).Should().Be(0);
    }

    // ── Reconciliação: o coração do CON-002 ───────────────────────────────────

    [Fact]
    public async Task Sincronizar_DuasVezes_NaoDuplicaOAlerta()
    {
        using var db = CreateDb();
        await SeedNotaAsync(db, NotaFiscalStatus.ResultadoIncerto, n => n.ChaveAcesso = new string('9', 44));
        var service = CreateService(db);

        await service.SincronizarAsync();
        await service.SincronizarAsync();
        await service.SincronizarAsync();

        var alertas = await db.AlertasFiscais.ToListAsync();
        alertas.Should().ContainSingle("a identidade do alerta vem do fato, não da execução");
        alertas[0].Ocorrencias.Should().Be(3, "mas cada ciclo registra que o problema continua lá");
    }

    [Fact]
    public async Task Sincronizar_FatoResolvido_FechaOAlertaAutomaticamente()
    {
        using var db = CreateDb();
        var nota = await SeedNotaAsync(db, NotaFiscalStatus.AutorizadaContingencia, n =>
            n.DhContingencia = DateTime.UtcNow.AddHours(-1));
        var service = CreateService(db);
        await service.SincronizarAsync();

        // A retransmissão funcionou: a contingência deixou de existir.
        nota.Status         = NotaFiscalStatus.Autorizada;
        nota.DhContingencia = null;
        nota.Protocolo      = "135260000000001";
        await db.SaveChangesAsync();
        await service.SincronizarAsync();

        var alerta = await db.AlertasFiscais.SingleAsync();
        alerta.ResolvidoEm.Should().NotBeNull();
        alerta.ResolvidoAutomaticamente.Should().BeTrue();
        alerta.ResolvidoPorUserId.Should().BeNull("ninguém resolveu — a condição é que deixou de existir");
    }

    [Fact]
    public async Task Sincronizar_ResolvidoManualmenteMasFatoContinua_Reabre()
    {
        using var db = CreateDb();
        var adminId = await SeedAdminAsync(db);
        await SeedNotaAsync(db, NotaFiscalStatus.Rejeitada, n => n.MotivoRejeicao = "Rejeicao qualquer");
        var service = CreateService(db);

        await service.SincronizarAsync();
        var alerta = await db.AlertasFiscais.SingleAsync();
        await service.ResolverAsync(alerta.Id, adminId, "Vou corrigir amanha");

        // A nota continua rejeitada — a confirmação foi otimista.
        await service.SincronizarAsync();

        db.ChangeTracker.Clear();
        var reaberto = await db.AlertasFiscais.SingleAsync();
        reaberto.ResolvidoEm.Should().BeNull("pendência fiscal não some porque alguém clicou");
        reaberto.ReabertoEm.Should().NotBeNull();
        reaberto.Reaberturas.Should().Be(1);
        reaberto.ResolucaoObservacao.Should().BeNull("a resolução anterior não vale mais");
    }

    [Fact]
    public async Task Sincronizar_AlertaCritico_NotificaAdminsUmaVezSo()
    {
        using var db = CreateDb();
        await SeedAdminAsync(db);
        await SeedNotaAsync(db, NotaFiscalStatus.ResultadoIncerto, n => n.ChaveAcesso = new string('9', 44));
        var service = CreateService(db);

        await service.SincronizarAsync();
        await service.SincronizarAsync();
        await service.SincronizarAsync();

        (await db.Notifications.CountAsync()).Should().Be(1,
            "a deduplicação é estrutural: notifica na criação e na escalada, não a cada ciclo");
    }

    [Fact]
    public async Task Sincronizar_ContingenciaQueEscalaParaCritica_NotificaNaEscalada()
    {
        using var db = CreateDb();
        await SeedAdminAsync(db);
        var nota = await SeedNotaAsync(db, NotaFiscalStatus.AutorizadaContingencia, n =>
            n.DhContingencia = DateTime.UtcNow.AddHours(-1));
        var service = CreateService(db);

        await service.SincronizarAsync();
        (await db.Notifications.CountAsync()).Should().Be(0, "alta ainda não é crítica");

        nota.DhContingencia = DateTime.UtcNow.AddHours(-22);
        await db.SaveChangesAsync();
        await service.SincronizarAsync();

        (await db.Notifications.CountAsync()).Should().Be(1, "a escalada é o momento de avisar");
    }

    // ── Venda sem documento ───────────────────────────────────────────────────

    [Fact]
    public async Task Sincronizar_VendasSemDocumentoDeOntem_GeraUmAlertaPorDia()
    {
        using var db = CreateDb();
        await SeedLojaQueEmiteAsync(db);
        var ontemBr = BrazilTime.NowBr().Date.AddDays(-1);
        var meioDiaUtc = BrazilTime.DateToUtcStart(ontemBr).AddHours(15);

        var user = new User { Id = Guid.NewGuid(), Name = "Cliente", Role = UserRole.Customer };
        db.Users.Add(user);
        for (var i = 0; i < 3; i++)
            db.Comandas.Add(new Comanda
            {
                Id = Guid.NewGuid(), UserId = user.Id, Status = ComandaStatus.Fechada,
                ClosedAt = meioDiaUtc, TotalInCents = 2500, PaymentMethod = "Dinheiro",
            });
        await db.SaveChangesAsync();

        await CreateService(db).SincronizarAsync();

        var alerta = await db.AlertasFiscais.SingleAsync();
        alerta.Tipo.Should().Be(TipoAlertaFiscal.VendaSemDocumento);
        alerta.Titulo.Should().Contain("3 venda(s)");
        alerta.Detalhe.Should().Contain("75,00", "o valor sem documento é o número que o contador precisa ver");
        alerta.Severidade.Should().Be(SeveridadeAlertaFiscal.Media, "ontem ainda é fechamento diário normal");
    }

    [Fact]
    public async Task Sincronizar_VendasSemDocumentoAntigas_EscalamParaAlta()
    {
        using var db = CreateDb();
        await SeedLojaQueEmiteAsync(db);
        var diaBr = BrazilTime.NowBr().Date.AddDays(-5);
        var meioDiaUtc = BrazilTime.DateToUtcStart(diaBr).AddHours(15);

        var user = new User { Id = Guid.NewGuid(), Name = "Cliente", Role = UserRole.Customer };
        db.Users.Add(user);
        db.Comandas.Add(new Comanda
        {
            Id = Guid.NewGuid(), UserId = user.Id, Status = ComandaStatus.Fechada,
            ClosedAt = meioDiaUtc, TotalInCents = 9900, PaymentMethod = "Dinheiro",
        });
        await db.SaveChangesAsync();

        await CreateService(db).SincronizarAsync();

        var alerta = await db.AlertasFiscais.SingleAsync();
        alerta.Severidade.Should().Be(SeveridadeAlertaFiscal.Alta,
            "passando de três dias já não é 'o dia ainda não fechou'");
    }

    [Fact]
    public async Task Sincronizar_VendaDeHoje_NaoEhCobradaAinda()
    {
        // Cobrar documento de uma venda que acabou de acontecer geraria alerta
        // que se resolve sozinho em minutos — ruído, não informação.
        using var db = CreateDb();
        var user = new User { Id = Guid.NewGuid(), Name = "Cliente", Role = UserRole.Customer };
        db.Users.Add(user);
        db.Comandas.Add(new Comanda
        {
            Id = Guid.NewGuid(), UserId = user.Id, Status = ComandaStatus.Fechada,
            ClosedAt = DateTime.UtcNow, TotalInCents = 5000, PaymentMethod = "Dinheiro",
        });
        await db.SaveChangesAsync();

        await CreateService(db).SincronizarAsync();

        (await db.AlertasFiscais.CountAsync(a => a.Tipo == TipoAlertaFiscal.VendaSemDocumento))
            .Should().Be(0);
    }

    // ── Lacuna de numeração ───────────────────────────────────────────────────

    [Fact]
    public async Task Sincronizar_BuracoNaSequencia_ApontaOsNumerosFaltantes()
    {
        using var db = CreateDb();
        await SeedLojaQueEmiteAsync(db);
        foreach (var numero in new[] { 10, 11, 14 })
            await SeedNotaAsync(db, NotaFiscalStatus.Autorizada, n =>
            {
                n.Numero    = numero;
                n.Protocolo = "135260000000001";
                n.EmitidoEm = DateTime.UtcNow.AddHours(-numero);
            });

        await CreateService(db).SincronizarAsync();

        var alerta = await db.AlertasFiscais
            .SingleAsync(a => a.Tipo == TipoAlertaFiscal.LacunaNumeracao);
        alerta.Titulo.Should().Contain("2 número(s) faltando");
        alerta.Detalhe.Should().Contain("12-13");
    }

    [Fact]
    public async Task Sincronizar_BuracoCobertoPorInutilizacao_NaoEhLacuna()
    {
        using var db = CreateDb();
        await SeedLojaQueEmiteAsync(db);
        foreach (var numero in new[] { 10, 11, 14 })
            await SeedNotaAsync(db, NotaFiscalStatus.Autorizada, n =>
            {
                n.Numero    = numero;
                n.Protocolo = "135260000000001";
                n.EmitidoEm = DateTime.UtcNow.AddHours(-numero);
            });

        db.InutilizacoesFiscais.Add(new InutilizacaoFiscal
        {
            Ano = BrazilTime.NowBr().Year, Serie = 1, NumeroInicial = 12, NumeroFinal = 13,
            Justificativa = "Numeracao abandonada por falha de comunicacao",
            Protocolo = "135260000000002",
        });
        await db.SaveChangesAsync();

        await CreateService(db).SincronizarAsync();

        (await db.AlertasFiscais.CountAsync(a => a.Tipo == TipoAlertaFiscal.LacunaNumeracao))
            .Should().Be(0, "a faixa foi formalmente inutilizada: a sequência está íntegra");
    }

    [Theory]
    [InlineData(new[] { 12, 13 }, "12-13")]
    [InlineData(new[] { 7 }, "7")]
    [InlineData(new[] { 1, 2, 3, 9, 20, 21 }, "1-3, 9, 20-21")]
    public void ResumirFaixas_CompactaNumerosConsecutivos(int[] numeros, string esperado)
    {
        AlertaFiscalService.ResumirFaixas(numeros).Should().Be(esperado);
    }

    // ── Exportação mensal ─────────────────────────────────────────────────────

    [Fact]
    public void ExportacaoMensalEstaAtrasada_NoDiaPrimeiro_NuncaCobra()
    {
        // O job roda no dia 1; cobrar antes dele viraria alarme falso mensal.
        AlertaFiscalService.ExportacaoMensalEstaAtrasada(
            new DateTime(2026, 8, 1), ultimoEnvioUtc: null, emailContador: "contador@exemplo.com")
            .Should().BeFalse();
    }

    [Fact]
    public void ExportacaoMensalEstaAtrasada_SemContadorConfigurado_NaoCobra()
    {
        AlertaFiscalService.ExportacaoMensalEstaAtrasada(
            new DateTime(2026, 8, 6), ultimoEnvioUtc: null, emailContador: null)
            .Should().BeFalse();
    }

    [Fact]
    public void ExportacaoMensalEstaAtrasada_EnvioDoMesAnterior_Cobra()
    {
        AlertaFiscalService.ExportacaoMensalEstaAtrasada(
            new DateTime(2026, 8, 6),
            ultimoEnvioUtc: new DateTime(2026, 7, 1, 3, 0, 0, DateTimeKind.Utc),
            emailContador: "contador@exemplo.com")
            .Should().BeTrue();
    }

    [Fact]
    public void ExportacaoMensalEstaAtrasada_JaEnviadoNesteMes_NaoCobra()
    {
        AlertaFiscalService.ExportacaoMensalEstaAtrasada(
            new DateTime(2026, 8, 6),
            ultimoEnvioUtc: new DateTime(2026, 8, 1, 3, 0, 0, DateTimeKind.Utc),
            emailContador: "contador@exemplo.com")
            .Should().BeFalse();
    }

    // ── Responsável e confirmação de resolução ────────────────────────────────

    [Fact]
    public async Task AtribuirResponsavel_RegistraQuemVaiResolverEQuando()
    {
        using var db = CreateDb();
        var adminId = await SeedAdminAsync(db);
        await SeedNotaAsync(db, NotaFiscalStatus.Rejeitada, n => n.MotivoRejeicao = "Motivo");
        var service = CreateService(db);
        await service.SincronizarAsync();
        var alerta = await db.AlertasFiscais.SingleAsync();

        await service.AtribuirResponsavelAsync(alerta.Id, adminId);

        db.ChangeTracker.Clear();
        var atualizado = await db.AlertasFiscais.SingleAsync();
        atualizado.ResponsavelUserId.Should().Be(adminId);
        atualizado.ResponsavelDefinidoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task AtribuirResponsavel_UsuarioInativo_Recusa()
    {
        using var db = CreateDb();
        var inativoId = await SeedAdminAsync(db, ativo: false);
        await SeedNotaAsync(db, NotaFiscalStatus.Rejeitada, n => n.MotivoRejeicao = "Motivo");
        var service = CreateService(db);
        await service.SincronizarAsync();
        var alerta = await db.AlertasFiscais.SingleAsync();

        var atribuir = () => service.AtribuirResponsavelAsync(alerta.Id, inativoId);

        await atribuir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*usuário ativo*");
    }

    [Fact]
    public async Task Resolver_SemDescreverOQueFoiFeito_Recusa()
    {
        using var db = CreateDb();
        var adminId = await SeedAdminAsync(db);
        await SeedNotaAsync(db, NotaFiscalStatus.Rejeitada, n => n.MotivoRejeicao = "Motivo");
        var service = CreateService(db);
        await service.SincronizarAsync();
        var alerta = await db.AlertasFiscais.SingleAsync();

        var resolver = () => service.ResolverAsync(alerta.Id, adminId, "ok");

        await resolver.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*auditável*", "confirmação sem conteúdo não serve como trilha");
    }

    [Fact]
    public async Task Resolver_RegistraQuemConfirmouEOQueFoiFeito()
    {
        using var db = CreateDb();
        var adminId = await SeedAdminAsync(db);
        await SeedNotaAsync(db, NotaFiscalStatus.Rejeitada, n => n.MotivoRejeicao = "Motivo");
        var service = CreateService(db);
        await service.SincronizarAsync();
        var alerta = await db.AlertasFiscais.SingleAsync();

        await service.ResolverAsync(alerta.Id, adminId, "NCM corrigido no cadastro e nota reemitida.");

        db.ChangeTracker.Clear();
        var resolvido = await db.AlertasFiscais.SingleAsync();
        resolvido.ResolvidoEm.Should().NotBeNull();
        resolvido.ResolvidoPorUserId.Should().Be(adminId);
        resolvido.ResolvidoAutomaticamente.Should().BeFalse();
        resolvido.ResolucaoObservacao.Should().Contain("NCM corrigido");
    }

    // ── Painel ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Listar_OrdenaPorGravidadeEIdade_EResumeOQuadro()
    {
        using var db = CreateDb();
        await SeedNotaAsync(db, NotaFiscalStatus.Rejeitada, n =>
        {
            n.Numero = 800;
            n.MotivoRejeicao = "Motivo";
        });
        await SeedNotaAsync(db, NotaFiscalStatus.ResultadoIncerto, n =>
        {
            n.Numero = 801;
            n.ChaveAcesso = new string('9', 44);
            n.ResultadoIncertoEm = DateTime.UtcNow.AddMinutes(-30);
        });
        var service = CreateService(db);
        await service.SincronizarAsync();

        var painel = await service.ListarAsync();

        painel.TotalAbertos.Should().Be(2);
        painel.Criticos.Should().Be(1);
        painel.Altos.Should().Be(1);
        painel.SemResponsavel.Should().Be(2);
        painel.Alertas[0].Severidade.Should().Be(nameof(SeveridadeAlertaFiscal.Critica),
            "o mais grave aparece primeiro");
        painel.Alertas[0].IdadeEmHoras.Should().Be(0);
    }

    [Fact]
    public async Task Listar_PorPadrao_NaoTrazOHistoricoResolvido()
    {
        using var db = CreateDb();
        var adminId = await SeedAdminAsync(db);
        await SeedNotaAsync(db, NotaFiscalStatus.Rejeitada, n => n.MotivoRejeicao = "Motivo");
        var service = CreateService(db);
        await service.SincronizarAsync();
        var alerta = await db.AlertasFiscais.SingleAsync();
        await service.ResolverAsync(alerta.Id, adminId, "Faixa inutilizada na SEFAZ.");

        (await service.ListarAsync()).Alertas.Should().BeEmpty();
        (await service.ListarAsync(incluirResolvidos: true)).Alertas.Should().ContainSingle();
    }
}
