// =============================================================================
// NfceEmissionServiceTests.cs — Testes do motor de emissão de NFC-e.
// Não há como testar uma transmissão real à SEFAZ neste ambiente (sem
// certificado de homologação nem rede) — os testes aqui verificam a garantia
// central do serviço: nunca lançar exceção e sempre deixar a nota registrada
// como PendenteEmissao quando a emissão não pode ser concluída.
// =============================================================================

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using DFe.Classes.Entidades;
using DFe.Classes.Flags;
using DFe.Utils;
using NFe.Classes.Informacoes.Identificacao.Tipos;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual.Tipos;
using System.Security.Authentication;

namespace CardGameStore.Tests.Services;

public class NfceEmissionServiceTests
{
    private static AppDbContext CreateDb() => TestDbFactory.Create(nameof(NfceEmissionServiceTests));

    private static EncryptionService CreateEncryptionService()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        return new EncryptionService(config, env.Object);
    }

    private static NfceEmissionService CreateService(AppDbContext db) =>
        new(db, CreateEncryptionService(), NullLogger<NfceEmissionService>.Instance);

    private static async Task<Comanda> SeedComandaFechadaAsync(AppDbContext db)
    {
        var user = new User { Id = Guid.NewGuid(), Name = "Cliente Teste", Role = UserRole.Customer };
        db.Users.Add(user);

        var product = new Product { Id = Guid.NewGuid(), Name = "Booster Pack", Category = "MTG", PriceInCents = 1500, StockQuantity = 10, Ncm = "95044000" };
        db.Products.Add(product);

        var comanda = new Comanda
        {
            Id            = Guid.NewGuid(),
            UserId        = user.Id,
            Status        = ComandaStatus.Fechada,
            TotalInCents  = 1500,
            PaymentMethod = "Dinheiro",
        };
        comanda.Items.Add(new ComandaItem
        {
            ComandaId          = comanda.Id,
            ProductId          = product.Id,
            ItemNameSnapshot    = product.Name,
            UnitPriceInCents    = 1500,
            Quantity            = 1,
            SubtotalInCents     = 1500,
        });
        db.Comandas.Add(comanda);
        await db.SaveChangesAsync();
        return comanda;
    }

    [Fact]
    public async Task EmitirParaComandaAsync_SemFiscalConfig_RegistraPendenteEmissaoSemLancarExcecao()
    {
        using var db = CreateDb();
        var comanda = await SeedComandaFechadaAsync(db);
        var service = CreateService(db);

        var nota = await service.EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.PendenteEmissao);
        nota.ComandaId.Should().Be(comanda.Id);
        nota.ValorTotalEmCentavos.Should().Be(1500);
    }

    [Fact]
    public async Task EmitirParaComandaAsync_ComFiscalConfigIncompleto_RegistraPendenteEmissaoSemLancarExcecao()
    {
        using var db = CreateDb();
        var comanda = await SeedComandaFechadaAsync(db);

        // Config existe mas sem certificado/endereço — deve cair no caminho "não configurado".
        db.FiscalConfigs.Add(new FiscalConfig { Cnpj = "12345678000100" });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var nota = await service.EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.PendenteEmissao);
    }

    private const string SenhaCertificadoTeste = "senha-teste-123";

    private static byte[] CreateSelfSignedPfx(string senha, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Fiscal Teste", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(notBefore, notAfter);
        return cert.Export(X509ContentType.Pfx, senha);
    }

    [Fact]
    public async Task EmitirParaComandaAsync_ComCertificadoVencido_RegistraPendenteEmissaoSemLancarExcecao()
    {
        // F3: certificado vencido tem que bloquear ANTES de qualquer tentativa de rede — senão
        // o handshake mTLS falho seria mal-classificado como "SEFAZ fora do ar" e cairia em
        // contingência offline (tpEmis=9) indevidamente.
        using var db = CreateDb();
        var comanda = await SeedComandaFechadaAsync(db);
        var enc     = CreateEncryptionService();

        var pfxBytes = CreateSelfSignedPfx(SenhaCertificadoTeste, DateTimeOffset.UtcNow.AddDays(-400), DateTimeOffset.UtcNow.AddDays(-1));

        db.FiscalConfigs.Add(new FiscalConfig
        {
            Cnpj                      = "12345678000100",
            RazaoSocial                = "Loja Teste LTDA",
            Logradouro                 = "Rua Teste",
            CodigoMunicipioIbge        = "3550308",
            Uf                         = "SP",
            CscId                      = "000001", // F12: pré-voo exige CSC antes do certificado
            CscTokenEncrypted          = enc.Encrypt(Guid.NewGuid().ToString()),
            CertificadoPfxEncrypted    = enc.Encrypt(Convert.ToBase64String(pfxBytes)),
            CertificadoSenhaEncrypted  = enc.Encrypt(SenhaCertificadoTeste),
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var nota = await service.EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.PendenteEmissao);
        nota.MotivoRejeicao.Should().Contain("vencido");
        nota.ChaveAcesso.Should().BeNull(); // nunca chegou a tentar transmitir
    }

    [Fact]
    public async Task EmitirParaComandaAsync_SemCsc_RegistraPendenteEmissaoSemLancarExcecao()
    {
        // F12: sem CSC a transmissão sai sem QR Code (obrigatório em NFC-e) — bloqueia no
        // pré-voo, antes de reservar número, em vez de queimar numeração à toa.
        using var db = CreateDb();
        var comanda = await SeedComandaFechadaAsync(db);
        var enc     = CreateEncryptionService();
        var pfxBytes = CreateSelfSignedPfx(SenhaCertificadoTeste, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        db.FiscalConfigs.Add(new FiscalConfig
        {
            Cnpj                      = "12345678000100",
            RazaoSocial                = "Loja Teste LTDA",
            Logradouro                 = "Rua Teste",
            CodigoMunicipioIbge        = "3550308",
            Uf                         = "SP",
            CertificadoPfxEncrypted    = enc.Encrypt(Convert.ToBase64String(pfxBytes)),
            CertificadoSenhaEncrypted  = enc.Encrypt(SenhaCertificadoTeste),
            // CscId/CscToken deliberadamente ausentes
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var nota = await service.EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.PendenteEmissao);
        nota.MotivoRejeicao.Should().Contain("CSC");
        nota.Numero.Should().BeNull(); // nunca chegou a reservar número
    }

    [Fact]
    public async Task EmitirParaComandaAsync_Repetido_ReutilizaMesmaNota()
    {
        using var db = CreateDb();
        var comanda = await SeedComandaFechadaAsync(db);
        var service = CreateService(db);

        var primeira = await service.EmitirParaComandaAsync(comanda.Id);
        var segunda = await service.EmitirParaComandaAsync(comanda.Id);

        segunda.Id.Should().Be(primeira.Id);
        (await db.NotasFiscaisEmitidas.CountAsync(n => n.ComandaId == comanda.Id)).Should().Be(1);
    }

    [Fact]
    public async Task EmitirParaComandaAsync_ComDescontoEPontos_RegistraValorLiquido()
    {
        using var db = CreateDb();
        var comanda = await SeedComandaFechadaAsync(db);
        comanda.TotalInCents = 1000;
        comanda.DiscountInCents = 300;
        comanda.PointsApplied = 200;
        await db.SaveChangesAsync();

        var nota = await CreateService(db).EmitirParaComandaAsync(comanda.Id);

        nota.ValorTotalEmCentavos.Should().Be(1000);
        nota.Status.Should().Be(NotaFiscalStatus.PendenteEmissao);
    }

    [Fact]
    public void CriarConfiguracaoCertificado_UsaA1ByteArray()
    {
        var config = NfceEmissionService.CriarConfiguracaoCertificado([1, 2, 3], "senha");

        config.TipoCertificado.Should().Be(TipoCertificado.A1ByteArray);
        config.ArrayBytesArquivo.Should().Equal(1, 2, 3);
        config.Senha.Should().Be("senha");
    }

    [Fact]
    public void CriarConfiguracaoServico_DefineTipoEmissaoNormal()
    {
        var config = NfceEmissionService.CriarConfiguracaoServico(Estado.SP, TipoAmbiente.Homologacao);

        config.tpEmis.Should().Be(TipoEmissao.teNormal);
        config.ModeloDocumento.Should().Be(ModeloDocumento.NFCe);
    }

    [Theory]
    [InlineData("12.345.678/0001-90", "12345678000190")]
    [InlineData("12345678000190", "12345678000190")]
    public void NormalizarCnpjParaSefaz_CentralizaFormatoDaFronteira(string entrada, string esperado)
    {
        NfceEmissionService.NormalizarCnpjParaSefaz(entrada).Should().Be(esperado);
    }

    [Fact]
    public void NormalizarCnpjParaSefaz_FormatoIncompativel_ExplicaContrato()
    {
        var act = () => NfceEmissionService.NormalizarCnpjParaSefaz("identificador-futuro");

        act.Should().Throw<FiscalNaoConfiguradoException>().WithMessage("*identificador fiscal*");
    }

    [Theory]
    [InlineData("123.456.789-01", "12345678901")]
    [InlineData("12345678901", "12345678901")]
    public void NormalizarCpfOpcionalParaSefaz_RemoveFormatacao(string entrada, string esperado)
    {
        NfceEmissionService.NormalizarCpfOpcionalParaSefaz(entrada).Should().Be(esperado);
    }

    [Fact]
    public void NormalizarCpfOpcionalParaSefaz_CpfInvalido_ExplicaProblema()
    {
        var act = () => NfceEmissionService.NormalizarCpfOpcionalParaSefaz("123");

        act.Should().Throw<FiscalNaoConfiguradoException>().WithMessage("*11 dígitos*");
    }

    [Fact]
    public async Task EmitirParaComandaAsync_ComandaInexistente_NuncaLancaExcecao()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var nota = await service.EmitirParaComandaAsync(Guid.NewGuid());

        nota.Status.Should().Be(NotaFiscalStatus.PendenteEmissao);
    }

    [Fact]
    public async Task EmitirParaVendaAvulsaAsync_SemVendaCorrespondente_NuncaLancaExcecao()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var nota = await service.EmitirParaVendaAvulsaAsync(Guid.NewGuid());

        nota.Status.Should().Be(NotaFiscalStatus.PendenteEmissao);
        nota.Origem.Should().Be(NotaFiscalOrigem.VendaAvulsa);
    }

    [Fact]
    public async Task ReprocessarAsync_NotaAutorizada_DevolveSemTentarDeNovo()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida { Status = NotaFiscalStatus.Autorizada, ChaveAcesso = "X", Protocolo = "P" };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var resultado = await service.ReprocessarAsync(nota.Id);

        resultado.Status.Should().Be(NotaFiscalStatus.Autorizada);
        resultado.TentativasReprocessamento.Should().Be(0); // nem tentou — devolveu direto
    }

    [Fact]
    public async Task EstornarOrigemNoErpAsync_Comanda_RestauraUmaUnicaVez()
    {
        using var db = CreateDb();
        var comanda = await SeedComandaFechadaAsync(db);
        comanda.FiscalEffectsCapturedAt = DateTime.UtcNow;
        comanda.PointsDebitedAtSale = 300;
        comanda.PointsAwardedAtSale = 10;
        comanda.CashbackDebitedAtSale = 200;
        comanda.User.PointsBalance = 500;
        comanda.User.BalanceInCents = 800;
        (await db.Products.SingleAsync()).StockQuantity = 9;
        var nota = new NotaFiscalEmitida
        {
            Origem = NotaFiscalOrigem.Comanda,
            ComandaId = comanda.Id,
            Status = NotaFiscalStatus.Cancelada,
        };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.EstornarOrigemNoErpAsync(nota.Id);
        await service.EstornarOrigemNoErpAsync(nota.Id);

        db.ChangeTracker.Clear();
        var atual = await db.Comandas.Include(c => c.User).Include(c => c.Items).ThenInclude(i => i.Product)
            .SingleAsync(c => c.Id == comanda.Id);
        atual.Status.Should().Be(ComandaStatus.Cancelada);
        atual.User.PointsBalance.Should().Be(790);
        atual.User.BalanceInCents.Should().Be(1000);
        atual.Items.Single().Product!.StockQuantity.Should().Be(10);
        (await db.NotasFiscaisEmitidas.FindAsync(nota.Id))!.ErpEstornadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task ReprocessarAsync_AcimaDoLimiteDeTentativas_NaoTentaDeNovo()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida { Status = NotaFiscalStatus.Rejeitada, TentativasReprocessamento = 10 };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var resultado = await service.ReprocessarAsync(nota.Id);

        resultado.TentativasReprocessamento.Should().Be(10); // não incrementou — nem tentou
    }

    [Fact]
    public async Task ReprocessarAsync_NumeroJaInutilizado_NaoTentaNovamente()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida
        {
            Status = NotaFiscalStatus.Rejeitada,
            InutilizadoEm = DateTime.UtcNow,
            TentativasReprocessamento = 2,
        };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var resultado = await CreateService(db).ReprocessarAsync(nota.Id);

        resultado.TentativasReprocessamento.Should().Be(2);
    }

    [Fact]
    public async Task ReprocessarAsync_ContingenciaDentroDoPrazoLegal_IgnoraLimiteDeTentativasComum()
    {
        // F2: contingência (AutorizadaContingencia) NUNCA deve ser bloqueada pelo contador de
        // tentativas comum (10, pensado pra PendenteEmissao/Rejeitada) — só pelo prazo legal de
        // 24h. Nota com 10+ tentativas mas dentro do prazo ainda deve tentar de novo.
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida
        {
            Status                     = NotaFiscalStatus.AutorizadaContingencia,
            TentativasReprocessamento  = 10,
            DhContingencia             = DateTime.UtcNow.AddHours(-1), // bem dentro das 24h
        };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var resultado = await service.ReprocessarAsync(nota.Id);

        resultado.TentativasReprocessamento.Should().Be(11); // tentou de novo apesar do contador alto
    }

    [Fact]
    public async Task ReprocessarAsync_ContingenciaAposPrazoLegalDe24h_NaoTentaDeNovo()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida
        {
            Status         = NotaFiscalStatus.AutorizadaContingencia,
            DhContingencia = DateTime.UtcNow.AddHours(-25), // passou das 24h
        };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var resultado = await service.ReprocessarAsync(nota.Id);

        resultado.TentativasReprocessamento.Should().Be(0); // não incrementou — nem tentou
        resultado.Status.Should().Be(NotaFiscalStatus.AutorizadaContingencia); // continua como estava, exige ação manual
    }

    [Fact]
    public async Task CancelarAsync_NotaNaoAutorizada_LancaExcecao()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida { Status = NotaFiscalStatus.PendenteEmissao };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Func<Task> act = () => service.CancelarAsync(nota.Id, "Motivo com mais de quinze caracteres");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Autorizada*");
    }

    [Fact]
    public async Task InutilizarFaixaAsync_FaixaComDocumentoAutorizado_BloqueiaAntesDaSefaz()
    {
        using var db = CreateDb();
        db.NotasFiscaisEmitidas.Add(new NotaFiscalEmitida
        {
            Status = NotaFiscalStatus.Autorizada,
            Serie = 1,
            Numero = 50,
        });
        await db.SaveChangesAsync();

        Func<Task> act = async () => await CreateService(db).InutilizarFaixaAsync(
            DateTime.Now.Year, 1, 49, 51, "Faixa abandonada por erro operacional");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*autorizada*");
    }

    [Fact]
    public async Task InutilizarFaixaAsync_DocumentoDeOutroAnoNaoBloqueiaFaixa()
    {
        using var db = CreateDb();
        var anoAtual = DateTime.Now.Year;
        db.NotasFiscaisEmitidas.Add(new NotaFiscalEmitida
        {
            Status = NotaFiscalStatus.Autorizada,
            Serie = 1,
            Numero = 50,
            EmitidoEm = new DateTime(anoAtual - 1, 6, 1, 12, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();

        Func<Task> act = async () => await CreateService(db).InutilizarFaixaAsync(
            anoAtual, 1, 49, 51, "Faixa abandonada por erro operacional");

        // Sem configuração fiscal, a execução só deve parar no pré-voo da SEFAZ,
        // demonstrando que a nota do ano anterior não conflitou com a faixa atual.
        await act.Should().ThrowAsync<FiscalNaoConfiguradoException>().WithMessage("*Certificado*");
    }

    [Fact]
    public async Task InutilizarFaixaAsync_MesmaFaixaJaRegistrada_EhIdempotente()
    {
        using var db = CreateDb();
        var existente = new InutilizacaoFiscal
        {
            Ano = DateTime.Now.Year,
            Serie = 1,
            NumeroInicial = 10,
            NumeroFinal = 12,
            Justificativa = "Faixa abandonada por erro operacional",
            Protocolo = "123",
        };
        db.InutilizacoesFiscais.Add(existente);
        await db.SaveChangesAsync();

        var resultado = await CreateService(db).InutilizarFaixaAsync(
            existente.Ano, 1, 10, 12, existente.Justificativa);

        resultado.Id.Should().Be(existente.Id);
        (await db.InutilizacoesFiscais.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1002)]
    public async Task InutilizarFaixaAsync_FaixaInvalida_BloqueiaAntesDaSefaz(int inicio, int fim)
    {
        using var db = CreateDb();
        Func<Task> act = async () => await CreateService(db).InutilizarFaixaAsync(
            DateTime.Now.Year, 1, inicio, fim, "Faixa abandonada por erro operacional");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CancelarAsync_JustificativaCurta_LancaExcecao()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida { Status = NotaFiscalStatus.Autorizada, EmitidoEm = DateTime.UtcNow };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Func<Task> act = () => service.CancelarAsync(nota.Id, "curta demais");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*15 caracteres*");
    }

    [Fact]
    public async Task CancelarAsync_ForaDaJanelaLegal_LancaExcecao()
    {
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida
        {
            Status       = NotaFiscalStatus.Autorizada,
            EmitidoEm    = DateTime.UtcNow, // recente — mas EmitidoEm não conta pra janela (F14)
            AutorizadoEm = DateTime.UtcNow.AddHours(-2), // muito depois dos 30 min de janela
        };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Func<Task> act = () => service.CancelarAsync(nota.Id, "Motivo com mais de quinze caracteres");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*janela legal*");
    }

    [Fact]
    public async Task CancelarAsync_SemAutorizadoEm_LancaExcecaoMesmoComEmitidoEmRecente()
    {
        // F14: a janela conta a partir de AutorizadoEm, não de EmitidoEm — uma nota antiga
        // (de antes da migration, ou nunca corretamente autorizada) sem AutorizadoEm não pode
        // ser tratada como "dentro da janela" só porque EmitidoEm é recente.
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida
        {
            Status    = NotaFiscalStatus.Autorizada,
            EmitidoEm = DateTime.UtcNow,
        };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Func<Task> act = () => service.CancelarAsync(nota.Id, "Motivo com mais de quinze caracteres");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*janela legal*");
    }

    [Fact]
    public async Task CancelarAsync_DentroDaJanelaPorAutorizadoEm_PassaDaValidacaoDeJanela()
    {
        // F14: EmitidoEm antigo (contingência com venda de horas atrás) não deve mais barrar o
        // cancelamento se a autorização de verdade (AutorizadoEm) foi recente — o teste prova
        // isso indiretamente: sem FiscalConfig, se passasse da checagem de janela cairia na
        // checagem de configuração fiscal (exceção diferente), não na de janela.
        using var db = CreateDb();
        var nota = new NotaFiscalEmitida
        {
            Status       = NotaFiscalStatus.Autorizada,
            EmitidoEm    = DateTime.UtcNow.AddHours(-6), // venda em contingência, horas atrás
            AutorizadoEm = DateTime.UtcNow.AddMinutes(-5), // autorizada de verdade agora há pouco
        };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Func<Task> act = () => service.CancelarAsync(nota.Id, "Motivo com mais de quinze caracteres");

        await act.Should().ThrowAsync<FiscalNaoConfiguradoException>(
            "passou da checagem de janela (é isso que o teste prova) e caiu na falta de FiscalConfig");
    }

    [Fact]
    public async Task EmitirParaComandaAsync_ComandaCancelada_AnulaNotaSemTentarTransmitir()
    {
        using var db = CreateDb();
        var comanda = await SeedComandaFechadaAsync(db);
        comanda.Status = ComandaStatus.Cancelada;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var nota = await service.EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.Cancelada);
        nota.CanceladoEm.Should().NotBeNull();
        nota.ChaveAcesso.Should().BeNull(); // nunca chegou a ser transmitida à SEFAZ
    }

    [Fact]
    public async Task ReprocessarAsync_ComandaFoiCanceladaAposFicarPendente_AnulaNotaEmVezDeEmitir()
    {
        using var db = CreateDb();
        var comanda = await SeedComandaFechadaAsync(db);

        var nota = new NotaFiscalEmitida
        {
            Origem    = NotaFiscalOrigem.Comanda,
            ComandaId = comanda.Id,
            Status    = NotaFiscalStatus.PendenteEmissao,
        };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();

        // Comanda é cancelada enquanto a nota ainda está pendente (ex: retry automático não rodou ainda)
        comanda.Status = ComandaStatus.Cancelada;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var resultado = await service.ReprocessarAsync(nota.Id);

        resultado.Status.Should().Be(NotaFiscalStatus.Cancelada);
    }

    // ── Mapeamento de CSOSN (MontarIcmsSimplesNacional) ───────────────────────

    private static NfceEmissionService.ItemFiscal Item(string? csosn, decimal? percentualCredito = null) =>
        new(Nome: "Item Teste", Ncm: "95044000", Cfop: "5102", Csosn: csosn,
            PercentualCreditoSn: percentualCredito, Quantidade: 1, PrecoUnitarioCentavos: 1000, SubtotalCentavos: 1000,
            PercentualTributosFederais: 10m, PercentualTributosEstaduais: 5m,
            PercentualTributosMunicipais: 0m, FonteTributos: "Tabela teste 2026");

    [Theory]
    [InlineData("8474.31.00", "84743100")]
    [InlineData(" 9504 4000 ", "95044000")]
    public void SanitizarNcm_RemoveFormatacao(string entrada, string esperado)
    {
        NfceEmissionService.SanitizarNcm(entrada).Should().Be(esperado);
    }

    [Fact]
    public void SanitizarNcm_QuantidadeDeDigitosInvalida_ExplicaErro()
    {
        var act = () => NfceEmissionService.SanitizarNcm("123.45");

        act.Should().Throw<FiscalNaoConfiguradoException>().WithMessage("*8 digitos*");
    }

    [Theory]
    [InlineData("28.063.00", "2806300")]
    [InlineData(" 2806300 ", "2806300")]
    public void SanitizarCest_RemoveFormatacao(string entrada, string esperado)
    {
        NfceEmissionService.SanitizarCest(entrada, obrigatorio: true).Should().Be(esperado);
    }

    [Fact]
    public void MontarItem_ComIcmsStSemCest_BloqueiaAntesDaEmissao()
    {
        var item = Item("202") with
        {
            Cfop = "5403", ModalidadeBcSt = 4, PercentualMvaSt = 40m,
            PercentualReducaoBcSt = 0m, AliquotaIcmsSt = 18m, AliquotaIcmsProprio = 12m,
        };

        var act = () => NfceEmissionService.MontarItem(item, 1);

        act.Should().Throw<FiscalNaoConfiguradoException>().WithMessage("*CEST obrigatorio*");
    }

    [Theory]
    [InlineData("5.102", 5102)]
    [InlineData(" 5102 ", 5102)]
    public void SanitizarCfop_RemoveFormatacao(string entrada, int esperado)
    {
        NfceEmissionService.SanitizarCfop(entrada).Should().Be(esperado);
    }

    [Fact]
    public void SanitizarCfop_Invalido_ExplicaErro()
    {
        var act = () => NfceEmissionService.SanitizarCfop("51-A");

        act.Should().Throw<FiscalNaoConfiguradoException>().WithMessage("*4 digitos*");
    }

    [Theory]
    [InlineData("01310-100", "01310100")]
    [InlineData("01.310 100", "01310100")]
    public void SanitizarCep_RemoveFormatacao(string entrada, string esperado)
    {
        NfceEmissionService.SanitizarCep(entrada).Should().Be(esperado);
    }

    [Fact]
    public void DistribuirDesconto_PreservaTotalExatoMesmoComArredondamento()
    {
        var itens = new[]
        {
            Item("102") with { SubtotalCentavos = 1000 },
            Item("102") with { SubtotalCentavos = 2000 },
            Item("102") with { SubtotalCentavos = 3000 },
        };

        var descontos = NfceEmissionService.DistribuirDesconto(itens, 1001);

        descontos.Sum().Should().Be(1001);
        descontos.Zip(itens).Should().OnlyContain(x => x.First <= x.Second.SubtotalCentavos);
    }

    [Fact]
    public void MontarItem_IncluiDescontoESanitizaCamposFiscais()
    {
        var item = Item("102") with { Ncm = "8474.31.00", Cfop = "5.102" };

        var det = NfceEmissionService.MontarItem(item, 1, 125);

        det.prod.NCM.Should().Be("84743100");
        det.prod.CEST.Should().BeNull();
        det.prod.CFOP.Should().Be(5102);
        det.prod.vDesc.Should().Be(1.25m);
        det.imposto.vTotTrib.Should().Be(1.32m);
        var xml = DFe.Utils.FuncoesXml.ClasseParaXmlString(det);
        xml.Should().Contain("<vTotTrib>1.32</vTotTrib>");
    }

    [Fact]
    public void CalcularTributosAproximados_UsaValorLiquidoESeparaEsferas()
    {
        var tributos = NfceEmissionService.CalcularTributosAproximados(Item("102"), descontoCentavos: 200);

        tributos.Federal.Should().Be(0.80m);
        tributos.Estadual.Should().Be(0.40m);
        tributos.Municipal.Should().Be(0m);
        tributos.Total.Should().Be(1.20m);
        tributos.Fonte.Should().Be("Tabela teste 2026");
    }

    [Fact]
    public void CalcularTributosAproximados_TabelaAutomaticaVencida_BloqueiaDocumento()
    {
        var item = Item("102") with
        {
            TributosPreenchidosAutomaticamente = true,
            TributosVigenciaFim = DateTime.UtcNow.AddDays(-2),
        };

        var act = () => NfceEmissionService.CalcularTributosAproximados(item);

        act.Should().Throw<FiscalNaoConfiguradoException>().WithMessage("*venceu*");
    }

    [Theory]
    [InlineData("102", Csosnicms.Csosn102)]
    [InlineData(null,  Csosnicms.Csosn102)]
    [InlineData("",    Csosnicms.Csosn102)]
    [InlineData("103", Csosnicms.Csosn103)]
    [InlineData("300", Csosnicms.Csosn300)]
    [InlineData("400", Csosnicms.Csosn400)]
    public void MontarIcmsSimplesNacional_CodigosSemCredito_UsamClasseICMSSN102(string? csosn, Csosnicms esperado)
    {
        var icms = NfceEmissionService.MontarIcmsSimplesNacional(Item(csosn));

        icms.Should().BeOfType<ICMSSN102>();
        ((ICMSSN102)icms).CSOSN.Should().Be(esperado);
    }

    [Fact]
    public void MontarIcmsSimplesNacional_Csosn101_CalculaCreditoCorretamente()
    {
        var icms = NfceEmissionService.MontarIcmsSimplesNacional(Item("101", percentualCredito: 2.5m));

        var sn101 = icms.Should().BeOfType<ICMSSN101>().Subject;
        sn101.CSOSN.Should().Be(Csosnicms.Csosn101);
        sn101.pCredSN.Should().Be(2.5m);
        sn101.vCredICMSSN.Should().Be(0.25m); // R$10,00 (1000 centavos) * 2,5% = R$0,25
    }

    [Fact]
    public void MontarIcmsSimplesNacional_Csosn101_UsaBaseLiquidaAposDesconto()
    {
        var icms = NfceEmissionService.MontarIcmsSimplesNacional(
            Item("101", percentualCredito: 2.5m), descontoCentavos: 200);

        icms.Should().BeOfType<ICMSSN101>().Which.vCredICMSSN.Should().Be(0.20m);
    }

    [Fact]
    public void MontarIcmsSimplesNacional_Csosn500_UsaClasseICMSSN500()
    {
        var icms = NfceEmissionService.MontarIcmsSimplesNacional(Item("500"));

        icms.Should().BeOfType<ICMSSN500>().Which.CSOSN.Should().Be(Csosnicms.Csosn500);
    }

    [Fact]
    public void MontarIcmsSimplesNacional_Csosn900_UsaClasseICMSSN900()
    {
        var icms = NfceEmissionService.MontarIcmsSimplesNacional(Item("900"));

        icms.Should().BeOfType<ICMSSN900>().Which.CSOSN.Should().Be(Csosnicms.Csosn900);
    }

    [Theory]
    [InlineData("201")]
    [InlineData("202")]
    [InlineData("203")]
    public void MontarIcmsSimplesNacional_CsosnComIcmsSt_LancaFiscalNaoConfigurado(string csosn)
    {
        var act = () => NfceEmissionService.MontarIcmsSimplesNacional(Item(csosn));

        act.Should().Throw<FiscalNaoConfiguradoException>().WithMessage("*BC-ST*");
    }

    [Fact]
    public void MontarItem_Csosn202Configurado_DecompoeStSemAlterarTotalConsumidor()
    {
        var item = new NfceEmissionService.ItemFiscal(
            Nome: "Produto ST", Ncm: "95044000", Cfop: "5403", Csosn: "202",
            PercentualCreditoSn: null, Quantidade: 1, PrecoUnitarioCentavos: 10000,
            SubtotalCentavos: 10000, OrigemMercadoria: 0, ModalidadeBcSt: 4,
            PercentualMvaSt: 40m, PercentualReducaoBcSt: 0m,
            AliquotaIcmsSt: 18m, AliquotaIcmsProprio: 12m, AliquotaFcpSt: 2m,
            Cest: "2806300", PercentualTributosFederais: 10m,
            PercentualTributosEstaduais: 5m, PercentualTributosMunicipais: 0m,
            FonteTributos: "Tabela teste 2026");

        var det = NfceEmissionService.MontarItem(item, 1);
        var icms = det.imposto.ICMS.TipoICMS.Should().BeOfType<ICMSSN202>().Subject;

        icms.CSOSN.Should().Be(Csosnicms.Csosn202);
        det.prod.CEST.Should().Be("2806300");
        DFe.Utils.FuncoesXml.ClasseParaXmlString(det).Should().Contain("<CEST>2806300</CEST>");
        icms.vBCST.Should().Be(120.69m);
        icms.vICMSST.Should().Be(11.38m);
        icms.vFCPST.Should().Be(2.41m);
        (det.prod.vProd - det.prod.vDesc + icms.vICMSST + icms.vFCPST!.Value)
            .Should().Be(100m, "o preço cadastrado já é o total final ao consumidor");
    }

    [Fact]
    public void MontarItem_Csosn201_CalculaCreditoESomaTotaisSt()
    {
        var item = new NfceEmissionService.ItemFiscal(
            Nome: "Produto ST", Ncm: "95044000", Cfop: "5403", Csosn: "201",
            PercentualCreditoSn: 2.5m, Quantidade: 1, PrecoUnitarioCentavos: 10000,
            SubtotalCentavos: 10000, ModalidadeBcSt: 6,
            PercentualReducaoBcSt: 0m, AliquotaIcmsSt: 18m,
            AliquotaIcmsProprio: 12m, AliquotaFcpSt: 0m,
            Cest: "2806300", PercentualTributosFederais: 10m,
            PercentualTributosEstaduais: 5m, PercentualTributosMunicipais: 0m,
            FonteTributos: "Tabela teste 2026");

        var det = NfceEmissionService.MontarItem(item, 1);
        var icms = det.imposto.ICMS.TipoICMS.Should().BeOfType<ICMSSN201>().Subject;
        var totais = NfceEmissionService.SomarTotaisIcms(new[] { det });

        icms.vCredICMSSN.Should().BeGreaterThan(0);
        totais.BaseSt.Should().Be(icms.vBCST);
        totais.ValorSt.Should().Be(icms.vICMSST);
        totais.ValorFcpSt.Should().Be(0);
    }

    [Fact]
    public void MontarIcmsSimplesNacional_CsosnDesconhecido_LancaFiscalNaoConfigurado()
    {
        var act = () => NfceEmissionService.MontarIcmsSimplesNacional(Item("999"));

        act.Should().Throw<FiscalNaoConfiguradoException>();
    }

    [Fact]
    public void MontarItem_ComIbsCbs2026_UsaBaseLiquidaEClassificacaoOficial()
    {
        var det = NfceEmissionService.MontarItem(
            Item("102"), numero: 1, descontoCentavos: 200, incluirIbsCbs: true);

        var ibsCbs = det.imposto.IBSCBS!;
        ibsCbs.CST.ToString().Should().Be("Cst000");
        ibsCbs.cClassTrib.Should().Be("000001");
        ibsCbs.gIBSCBS!.vBC.Should().Be(8m);
        ibsCbs.gIBSCBS.gIBSUF!.pIBSUF.Should().Be(0.1m);
        ibsCbs.gIBSCBS.gIBSUF.vIBSUF.Should().Be(0.01m);
        ibsCbs.gIBSCBS.gIBSMun!.pIBSMun.Should().Be(0m);
        ibsCbs.gIBSCBS.gCBS!.pCBS.Should().Be(0.9m);
        ibsCbs.gIBSCBS.gCBS.vCBS.Should().Be(0.07m);

        var xml = DFe.Utils.FuncoesXml.ClasseParaXmlString(det);
        xml.Should().Contain("<IBSCBS>");
        xml.Should().Contain("<CST>000</CST>");
        xml.Should().Contain("<cClassTrib>000001</cClassTrib>");
    }

    [Fact]
    public void MontarTotaisIbsCbs2026_SomaBasesLiquidasETributosDosItens()
    {
        var itens = new[]
        {
            NfceEmissionService.MontarItem(Item("102"), 1, 200, incluirIbsCbs: true),
            NfceEmissionService.MontarItem(Item("102"), 2, 0, incluirIbsCbs: true),
        };

        var total = NfceEmissionService.MontarTotaisIbsCbs2026(itens);

        total.vBCIBSCBS.Should().Be(18m);
        total.gIBS!.gIBSUF!.vIBSUF.Should().Be(0.02m);
        total.gIBS.gIBSMun!.vIBSMun.Should().Be(0m);
        total.gIBS.vIBS.Should().Be(0.02m);
        total.gCBS!.vCBS.Should().Be(0.16m);
    }

    // ── Detecção de falha de conectividade (contingência) ─────────────────────

    [Theory]
    [InlineData(typeof(System.Net.Http.HttpRequestException))]
    [InlineData(typeof(System.Net.WebException))]
    [InlineData(typeof(System.Net.Sockets.SocketException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(TaskCanceledException))]
    public void EhFalhaDeConectividade_TiposDeRede_RetornaTrue(Type tipoExcecao)
    {
        var ex = (Exception)Activator.CreateInstance(tipoExcecao)!;

        NfceEmissionService.EhFalhaDeConectividade(ex).Should().BeTrue();
    }

    [Fact]
    public void EhFalhaDeConectividade_ExcecaoEmbrulhadaEmInnerException_RetornaTrue()
    {
        var ex = new InvalidOperationException("erro genérico da lib", new System.Net.Http.HttpRequestException("timeout"));

        NfceEmissionService.EhFalhaDeConectividade(ex).Should().BeTrue();
    }

    [Fact]
    public void EhFalhaDeConectividade_ExcecaoDeNegocio_RetornaFalse()
    {
        var ex = new InvalidOperationException("CNPJ inválido");

        NfceEmissionService.EhFalhaDeConectividade(ex).Should().BeFalse();
    }

    [Fact]
    public void EhFalhaDeConectividade_ErroTlsLocalEmHttpRequest_NaoViraContingencia()
    {
        var ex = new System.Net.Http.HttpRequestException(
            "TLS", new AuthenticationException("certificado rejeitado"));

        NfceEmissionService.EhFalhaDeConectividade(ex).Should().BeFalse();
        NfceEmissionService.EhFalhaDeCertificadoLocal(ex).Should().BeTrue();
    }
}
