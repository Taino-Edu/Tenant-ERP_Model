// =============================================================================
// NfceResultadoIncertoTests.cs — RES-001: o que acontece quando a resposta da
// SEFAZ se perde.
//
// Este é o único arquivo da suíte que exercita a transmissão inteira: monta,
// assina, gera QR Code e passa pela fronteira INfceSefazGateway — que aqui é
// uma SEFAZ falsa programável. É o que permite encenar os cenários que decidem
// se uma venda termina com um documento fiscal ou com dois:
//
//   • timeout DEPOIS de a SEFAZ autorizar (a resposta é que se perdeu);
//   • falha de conexão que nunca chegou a sair (contingência é legítima);
//   • duplicidade — a SEFAZ dizendo que aquela chave já existe;
//   • resolução tardia, no reprocessamento, de uma nota que ficou incerta.
//
// O critério de aceite do plano é o teste "SEFAZ autorizou e a resposta caiu":
// termina existindo exatamente UMA NFC-e autorizada, com a mesma chave dos dois
// lados.
// =============================================================================

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using DFe.Classes.Flags;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NFe.Classes.Protocolo;
using NFe.Utils;
using NfeDocumento = NFe.Classes.NFe;

namespace CardGameStore.Tests.Services;

public class NfceResultadoIncertoTests
{
    // ── SEFAZ falsa ───────────────────────────────────────────────────────────

    /// <summary>
    /// SEFAZ programável: cada chamada de autorização consome a próxima resposta
    /// da fila (que pode ser uma exceção de rede), e a consulta de chave responde
    /// pelo delegate configurado. Registra tudo o que passou por ela, porque a
    /// afirmação mais importante destes testes é sobre o que NÃO foi transmitido.
    /// </summary>
    private sealed class SefazFalsa : INfceSefazGateway
    {
        private readonly Queue<Func<NfeDocumento, RespostaAutorizacaoNfce>> _respostas = new();

        public List<NfeDocumento> Transmissoes { get; } = new();
        public List<string> ChavesConsultadas { get; } = new();
        public Func<string, RespostaConsultaChaveNfce>? AoConsultar { get; set; }

        public SefazFalsa Responde(Func<NfeDocumento, RespostaAutorizacaoNfce> resposta)
        {
            _respostas.Enqueue(resposta);
            return this;
        }

        public SefazFalsa Falha(Func<Exception> erro) =>
            Responde(_ => throw erro());

        public RespostaAutorizacaoNfce Autorizar(
            ConfiguracaoServico configuracao, X509Certificate2 certificado, NfeDocumento nfe)
        {
            Transmissoes.Add(nfe);
            if (_respostas.Count == 0)
                throw new InvalidOperationException(
                    "SEFAZ falsa recebeu uma transmissão a mais do que o teste programou.");
            return _respostas.Dequeue()(nfe);
        }

        public RespostaConsultaChaveNfce ConsultarChave(
            ConfiguracaoServico configuracao, X509Certificate2 certificado, string chaveAcesso)
        {
            ChavesConsultadas.Add(chaveAcesso);
            if (AoConsultar is null)
                throw new TimeoutException("Consulta indisponível: a SEFAZ falsa não responde.");
            return AoConsultar(chaveAcesso);
        }
    }

    private static protNFe Protocolo(int cStat, string motivo, string? chave, string nProt = "135260000000001") =>
        new()
        {
            versao = "4.00",
            infProt = new infProt
            {
                Id        = chave is null ? null : $"ID{chave}",
                tpAmb     = TipoAmbiente.Homologacao,
                verAplic  = "TESTE",
                chNFe     = chave,
                dhRecbto  = DateTimeOffset.Now,
                nProt     = nProt,
                cStat     = cStat,
                xMotivo   = motivo,
            },
        };

    private static string ChaveDe(NfeDocumento nfe) =>
        (nfe.infNFe.Id ?? string.Empty).Replace("NFe", string.Empty);

    private static RespostaAutorizacaoNfce Autorizada(NfeDocumento nfe, string nProt = "135260000000001") =>
        new(104, "Lote processado com sucesso",
            Protocolo(100, "Autorizado o uso da NF-e", ChaveDe(nfe), nProt),
            "<envio/>", "<retorno/>");

    private static RespostaAutorizacaoNfce Rejeitada(NfeDocumento nfe, int cStat, string motivo) =>
        new(104, "Lote processado com sucesso",
            Protocolo(cStat, motivo, ChaveDe(nfe)),
            "<envio/>", "<retorno/>");

    /// <summary>Duplicidade vem no nível do lote, sem protocolo do documento.</summary>
    private static RespostaAutorizacaoNfce Duplicidade() =>
        new(204, "Rejeicao: Duplicidade de NF-e", null, "<envio/>", "<retorno/>");

    private static RespostaConsultaChaveNfce ConsultaAutorizada(string chave, string nProt = "135260000000001") =>
        new(100, "Autorizado o uso da NF-e", Protocolo(100, "Autorizado o uso da NF-e", chave, nProt), "<retorno/>");

    private static RespostaConsultaChaveNfce ConsultaNaoConsta() =>
        new(217, "Rejeicao: NF-e nao consta na base de dados da SEFAZ", null, "<retorno/>");

    // ── Cenário base ──────────────────────────────────────────────────────────

    private static AppDbContext CreateDb([CallerMemberName] string testName = "") =>
        TestDbFactory.Create($"{nameof(NfceResultadoIncertoTests)}_{testName}");

    private static EncryptionService CreateEncryptionService()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        return new EncryptionService(config, env.Object);
    }

    private const string SenhaCertificadoTeste = "senha-teste-123";

    private static byte[] CriarPfxAutoassinado()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Fiscal Teste", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
        return cert.Export(X509ContentType.Pfx, SenhaCertificadoTeste);
    }

    /// <summary>
    /// Loja fiscalmente completa: é o que separa este arquivo do resto da suíte,
    /// onde a emissão sempre para no pré-voo. Aqui o documento chega até a rede.
    /// </summary>
    private static async Task<Comanda> SeedLojaEVendaAsync(AppDbContext db)
    {
        var enc = CreateEncryptionService();
        db.FiscalConfigs.Add(new FiscalConfig
        {
            Cnpj                      = "12345678000195",
            RazaoSocial               = "Loja Teste LTDA",
            InscricaoEstadual         = "110042490114",
            Logradouro                = "Rua Teste",
            Numero                    = "100",
            Bairro                    = "Centro",
            Municipio                 = "Sao Paulo",
            CodigoMunicipioIbge       = "3550308",
            Uf                        = "SP",
            Cep                       = "01001000",
            CscId                     = "000001",
            CscTokenEncrypted         = enc.Encrypt("F3B4C0D6-0000-4000-8000-000000000000"),
            CertificadoPfxEncrypted   = enc.Encrypt(Convert.ToBase64String(CriarPfxAutoassinado())),
            CertificadoSenhaEncrypted = enc.Encrypt(SenhaCertificadoTeste),
            SerieNfce                 = 1,
            ProximoNumeroNfce         = 500,
        });

        var user = new User { Id = Guid.NewGuid(), Name = "Cliente Teste", Role = UserRole.Customer };
        db.Users.Add(user);

        var product = new Product
        {
            Id           = Guid.NewGuid(),
            Name         = "Booster Pack",
            Category     = "MTG",
            PriceInCents = 1500,
            StockQuantity = 10,
            Ncm          = "95044000",
            PercentualTributosFederais  = 12.5m,
            PercentualTributosEstaduais = 18m,
            PercentualTributosMunicipais = 0m,
            FonteTributos = "IBPT 26.1.A",
        };
        db.Products.Add(product);

        var comanda = new Comanda
        {
            Id            = Guid.NewGuid(),
            UserId        = user.Id,
            Status        = ComandaStatus.Fechada,
            TotalInCents  = 1500,
            PaymentMethod = "Dinheiro",
            ClosedAt      = DateTime.UtcNow,
        };
        comanda.Items.Add(new ComandaItem
        {
            ComandaId        = comanda.Id,
            ProductId        = product.Id,
            ItemNameSnapshot = product.Name,
            UnitPriceInCents = 1500,
            Quantity         = 1,
            SubtotalInCents  = 1500,
        });
        db.Comandas.Add(comanda);

        await db.SaveChangesAsync();
        return comanda;
    }

    private static NfceEmissionService CreateService(AppDbContext db, SefazFalsa sefaz) =>
        new(db, CreateEncryptionService(), NullLogger<NfceEmissionService>.Instance,
            new ConfigurableFiscalTaxEngine(), sefaz);

    private static Exception TimeoutDeRede() =>
        new TaskCanceledException("A operação excedeu o tempo limite.", new TimeoutException());

    private static Exception ConexaoRecusada() =>
        new System.Net.Http.HttpRequestException(
            "Nao foi possivel conectar",
            new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused));

    // ── Classificação da falha ────────────────────────────────────────────────

    [Fact]
    public void ClassificarFalhaDeTransmissao_ConexaoRecusada_SabeQueNuncaChegou()
    {
        NfceEmissionService.ClassificarFalhaDeTransmissao(ConexaoRecusada())
            .Should().Be(NfceEmissionService.DestinoTentativa.NuncaChegou);
    }

    [Fact]
    public void ClassificarFalhaDeTransmissao_DnsNaoResolveu_SabeQueNuncaChegou()
    {
        var erro = new System.Net.WebException(
            "O nome remoto não pôde ser resolvido", System.Net.WebExceptionStatus.NameResolutionFailure);

        NfceEmissionService.ClassificarFalhaDeTransmissao(erro)
            .Should().Be(NfceEmissionService.DestinoTentativa.NuncaChegou);
    }

    [Fact]
    public void ClassificarFalhaDeTransmissao_Timeout_NaoPresumeFalha()
    {
        NfceEmissionService.ClassificarFalhaDeTransmissao(TimeoutDeRede())
            .Should().Be(NfceEmissionService.DestinoTentativa.Incerto);
    }

    [Fact]
    public void ClassificarFalhaDeTransmissao_FalhaDesconhecida_ErraParaOLadoSeguro()
    {
        // Uma falha de rede que não se sabe classificar não pode virar "não chegou":
        // esse é o palpite que produz a segunda NFC-e da mesma venda.
        NfceEmissionService.ClassificarFalhaDeTransmissao(new IOException("conexão perdida"))
            .Should().Be(NfceEmissionService.DestinoTentativa.Incerto);
    }

    // ── Máquina de estados ────────────────────────────────────────────────────

    [Fact]
    public async Task Emissao_SefazAutorizouEARespostaCaiu_RecuperaProtocoloSemEmitirOutroDocumento()
    {
        // Critério de aceite do RES-001: termina existindo exatamente uma NFC-e
        // autorizada, com a mesma chave dos dois lados.
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        var sefaz = new SefazFalsa().Falha(TimeoutDeRede);
        string? chaveConsultada = null;
        sefaz.AoConsultar = chave =>
        {
            chaveConsultada = chave;
            return ConsultaAutorizada(chave, nProt: "135260000009999");
        };

        var nota = await CreateService(db, sefaz).EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.Autorizada);
        nota.Protocolo.Should().Be("135260000009999");
        nota.XmlAutorizado.Should().NotBeNullOrWhiteSpace("o nfeProc é montado com o protocolo recuperado");
        nota.ChaveAcesso.Should().Be(chaveConsultada, "a chave consultada é a mesma do documento local");

        sefaz.Transmissoes.Should().HaveCount(1, "nenhum documento adicional pode ser transmitido");
        nota.DhContingencia.Should().BeNull("não houve contingência: a nota já estava autorizada");
        nota.XmlContingencia.Should().BeNull();
        nota.XmlTentativa.Should().BeNull("a tentativa deixou de estar em aberto");
        nota.TentativaId.Should().BeNull();
    }

    [Fact]
    public async Task Emissao_RespostaPerdidaESefazMudaSemResponder_FicaResultadoIncertoSemNovoDocumento()
    {
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        // Timeout na autorização e a consulta também não responde: não se sabe nada.
        var sefaz = new SefazFalsa().Falha(TimeoutDeRede);

        var nota = await CreateService(db, sefaz).EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.ResultadoIncerto);
        nota.ResultadoIncertoEm.Should().NotBeNull();
        nota.ChaveAcesso.Should().NotBeNullOrWhiteSpace("a chave transmitida fica registrada para consulta posterior");
        nota.XmlTentativa.Should().NotBeNullOrWhiteSpace("o XML enviado é o que permite recuperar a autorização depois");
        nota.TentativaId.Should().NotBeNull();
        nota.Numero.Should().NotBeNull("o número reservado continua preso a esta tentativa");

        sefaz.Transmissoes.Should().HaveCount(1);
        sefaz.ChavesConsultadas.Should().ContainSingle().Which.Should().Be(nota.ChaveAcesso);
        nota.DhContingencia.Should().BeNull("contingência sem saber o destino da chave duplicaria o documento");
        nota.XmlContingencia.Should().BeNull();
    }

    [Fact]
    public async Task Emissao_RespostaPerdidaEChaveNaoConstaNaSefaz_SegueParaContingencia()
    {
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        var sefaz = new SefazFalsa()
            .Falha(TimeoutDeRede)      // tentativa normal: resposta perdida
            .Falha(ConexaoRecusada);   // retransmissão do documento offline: SEFAZ ainda fora
        sefaz.AoConsultar = _ => ConsultaNaoConsta();

        var nota = await CreateService(db, sefaz).EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.AutorizadaContingencia,
            "com a chave confirmadamente ausente na SEFAZ, o documento offline é a conduta correta");
        nota.DhContingencia.Should().NotBeNull();
        nota.XmlContingencia.Should().NotBeNullOrWhiteSpace("é o documento que o consumidor leva");
        sefaz.ChavesConsultadas.Should().HaveCount(1, "consultou antes de decidir");
    }

    [Fact]
    public async Task Emissao_ConexaoNuncaEstabelecida_VaiDiretoParaContingenciaSemConsultar()
    {
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        var sefaz = new SefazFalsa()
            .Falha(ConexaoRecusada)
            .Falha(ConexaoRecusada);

        var nota = await CreateService(db, sefaz).EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.AutorizadaContingencia);
        sefaz.ChavesConsultadas.Should().BeEmpty(
            "a requisição não chegou a sair: não há chave a consultar e a contingência é imediata");
        nota.XmlContingencia.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Emissao_SefazRespondeDuplicidade_AdotaODocumentoQueJaExisteEmVezDeRejeitar()
    {
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        var sefaz = new SefazFalsa().Responde(_ => Duplicidade());
        sefaz.AoConsultar = chave => ConsultaAutorizada(chave, nProt: "135260000001234");

        var nota = await CreateService(db, sefaz).EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.Autorizada,
            "duplicidade é a SEFAZ dizendo que a nota existe lá — rejeitar localmente criaria divergência");
        nota.Protocolo.Should().Be("135260000001234");
        nota.XmlAutorizado.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Emissao_DuplicidadeSemConfirmacaoNaConsulta_NaoRejeitaNemEmiteOutro()
    {
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        var sefaz = new SefazFalsa().Responde(_ => Duplicidade());
        sefaz.AoConsultar = _ => ConsultaNaoConsta();

        var nota = await CreateService(db, sefaz).EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.ResultadoIncerto,
            "a SEFAZ se contradiz: não dá para rejeitar nem para emitir outro documento");
        nota.MotivoRejeicao.Should().Contain("duplicidade");
        sefaz.Transmissoes.Should().HaveCount(1);
    }

    [Fact]
    public async Task Emissao_RejeicaoDeNegocio_ContinuaSendoRejeicao()
    {
        // Regressão: o tratamento do resultado incerto não pode transformar
        // rejeição comum em estado pendente eterno.
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        var sefaz = new SefazFalsa().Responde(nfe =>
            Rejeitada(nfe, 778, "Rejeicao: Informado NCM inexistente"));

        var nota = await CreateService(db, sefaz).EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.Rejeitada);
        nota.MotivoRejeicao.Should().Contain("NCM inexistente");
        nota.XmlTentativa.Should().BeNull("o destino do documento é conhecido; não há tentativa em aberto");
        sefaz.ChavesConsultadas.Should().BeEmpty("rejeição de negócio não é ambiguidade");
    }

    // ── Resolução tardia (reprocessamento) ────────────────────────────────────

    [Fact]
    public async Task Reprocessar_ResultadoIncerto_ConsultaConfirmaAutorizacao_NaoRetransmite()
    {
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        var sefaz = new SefazFalsa().Falha(TimeoutDeRede);
        var service = CreateService(db, sefaz);

        var nota = await service.EmitirParaComandaAsync(comanda.Id);
        nota.Status.Should().Be(NotaFiscalStatus.ResultadoIncerto);
        var chaveOriginal = nota.ChaveAcesso;
        var numeroOriginal = nota.Numero;

        // A SEFAZ volta a responder e revela que aquele documento estava autorizado.
        sefaz.AoConsultar = chave => ConsultaAutorizada(chave, nProt: "135260000007777");

        var resolvida = await service.ReprocessarAsync(nota.Id);

        resolvida.Status.Should().Be(NotaFiscalStatus.Autorizada);
        resolvida.Protocolo.Should().Be("135260000007777");
        resolvida.ChaveAcesso.Should().Be(chaveOriginal, "é o mesmo documento, não um novo");
        resolvida.Numero.Should().Be(numeroOriginal);
        resolvida.XmlAutorizado.Should().NotBeNullOrWhiteSpace();
        sefaz.Transmissoes.Should().HaveCount(1, "nada foi retransmitido: só se consultou");
    }

    [Fact]
    public async Task Reprocessar_ResultadoIncerto_ChaveNaoConsta_RetransmiteOMesmoNumero()
    {
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        var sefaz = new SefazFalsa().Falha(TimeoutDeRede);
        var service = CreateService(db, sefaz);

        var nota = await service.EmitirParaComandaAsync(comanda.Id);
        var numeroOriginal = nota.Numero;

        // Agora a SEFAZ responde: não existe nada sob aquela chave.
        sefaz.AoConsultar = _ => ConsultaNaoConsta();
        sefaz.Responde(nfe => Autorizada(nfe, nProt: "135260000004242"));

        var resolvida = await service.ReprocessarAsync(nota.Id);

        resolvida.Status.Should().Be(NotaFiscalStatus.Autorizada);
        resolvida.Numero.Should().Be(numeroOriginal, "o número reservado é reaproveitado, não queimado");
        resolvida.Protocolo.Should().Be("135260000004242");
        sefaz.Transmissoes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Reprocessar_ResultadoIncerto_SefazContinuaMuda_NaoRetransmiteNada()
    {
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        var sefaz = new SefazFalsa().Falha(TimeoutDeRede);
        var service = CreateService(db, sefaz);

        var nota = await service.EmitirParaComandaAsync(comanda.Id);
        var resolvida = await service.ReprocessarAsync(nota.Id);

        resolvida.Status.Should().Be(NotaFiscalStatus.ResultadoIncerto);
        sefaz.Transmissoes.Should().HaveCount(1,
            "enquanto o destino da chave for desconhecido, nenhum documento novo sai");
        sefaz.ChavesConsultadas.Should().HaveCount(2, "cada ciclo é uma nova tentativa de descobrir");
    }

    // ── Numeração ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task InutilizarFaixa_ComNotaEmResultadoIncerto_Bloqueia()
    {
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        var sefaz = new SefazFalsa().Falha(TimeoutDeRede);
        var service = CreateService(db, sefaz);

        var nota = await service.EmitirParaComandaAsync(comanda.Id);
        nota.Status.Should().Be(NotaFiscalStatus.ResultadoIncerto);

        var inutilizar = () => service.InutilizarFaixaAsync(
            DateTime.UtcNow.Year, nota.Serie!.Value, nota.Numero!.Value, nota.Numero!.Value,
            "Numeracao abandonada apos falha de comunicacao");

        await inutilizar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*resultado incerto*",
                "declarar como não usado um número que pode ter documento autorizado é o pior desfecho possível");
    }
}
