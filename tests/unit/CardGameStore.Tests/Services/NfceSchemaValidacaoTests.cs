// =============================================================================
// NfceSchemaValidacaoTests.cs — XML-002: o XML que o motor produz passa no
// schema oficial da SEFAZ?
//
// Esta é a única pergunta que testes de montagem não respondem. Um `det` com os
// campos certos pode virar um documento inválido por ordem de elemento, grupo
// condicional ausente ou tipo fora do domínio — e a resposta hoje só aparecia
// como rejeição da SEFAZ, depois de o número já ter sido consumido.
//
// O XML validado aqui NÃO é remontado para o teste: é o `XmlTentativa`, que o
// RES-001 persiste com o documento assinado exatamente como foi transmitido.
// Validar qualquer outra coisa seria validar uma aproximação.
//
// As fixtures de `Fixtures/Nfce/` de propósito NÃO servem aqui: elas foram
// escritas à mão para o parser do DANFE (que tolera campo ausente) e usam UF 99
// inexistente para não parecerem documento real. Falham no XSD por construção.
// =============================================================================

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Schema;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NFe.Utils;
using NfeDocumento = NFe.Classes.NFe;

namespace CardGameStore.Tests.Services;

public class NfceSchemaValidacaoTests
{
    /// <summary>SEFAZ que nunca responde: o objetivo é chegar até o XML assinado,
    /// não até a autorização. O documento fica em `XmlTentativa`.</summary>
    private sealed class SefazMuda : INfceSefazGateway
    {
        public RespostaAutorizacaoNfce Autorizar(
            ConfiguracaoServico configuracao, X509Certificate2 certificado, NfeDocumento nfe) =>
            throw new TimeoutException("A SEFAZ não respondeu (proposital: o teste quer o XML, não o protocolo).");

        public RespostaConsultaChaveNfce ConsultarChave(
            ConfiguracaoServico configuracao, X509Certificate2 certificado, string chaveAcesso) =>
            throw new TimeoutException("Consulta indisponível.");
    }

    private static AppDbContext CreateDb([CallerMemberName] string testName = "") =>
        TestDbFactory.Create($"{nameof(NfceSchemaValidacaoTests)}_{testName}");

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
    /// Loja fiscalmente completa em SP (UF real — aqui a UF precisa existir no
    /// domínio TCodUfIBGE, ao contrário das fixtures do parser).
    /// </summary>
    private static async Task<Comanda> SeedLojaEVendaAsync(
        AppDbContext db, AmbienteFiscal ambiente = AmbienteFiscal.Homologacao,
        string? gtin = null, Action<Comanda>? ajustarVenda = null)
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
            ProximoNumeroNfce         = 900,
            Ambiente                  = ambiente,
        });

        var user = new User
        {
            Id = Guid.NewGuid(), Name = "Cliente Teste", Role = UserRole.Customer,
            Cpf = "00000000191",
        };
        db.Users.Add(user);

        var product = new Product
        {
            Id            = Guid.NewGuid(),
            Name          = "Booster Pack Coleção Especial",
            Category      = "MTG",
            PriceInCents  = 1500,
            StockQuantity = 10,
            Ncm           = "95044000",
            Barcode       = gtin,
            PercentualTributosFederais   = 12.5m,
            PercentualTributosEstaduais  = 18m,
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
        ajustarVenda?.Invoke(comanda);
        db.Comandas.Add(comanda);

        await db.SaveChangesAsync();
        return comanda;
    }

    private static NfceEmissionService CreateService(AppDbContext db) =>
        new(db, CreateEncryptionService(), NullLogger<NfceEmissionService>.Instance,
            new ConfigurableFiscalTaxEngine(), new SefazMuda());

    // ── Validação ─────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> ValidarContraSchemaOficial(string xmlNfe)
    {
        var erros = new List<string>();

        var documento = new XmlDocument();
        documento.LoadXml(xmlNfe);
        documento.Schemas = SchemasOficiais.Nfe;
        documento.Validate((_, e) => erros.Add($"{e.Severity}: {e.Message}"));

        return erros;
    }

    [Fact]
    public void SchemaOficial_EstaVersionadoNoRepositorio()
    {
        // Se o pacote sumir do repositório, os testes abaixo passariam a não
        // provar nada — e o go-live perderia a evidência de "validação XSD com
        // versão do pacote" que a seção 17.1 do plano exige.
        File.Exists(SchemasOficiais.CaminhoNfe).Should().BeTrue(
            $"o schema oficial precisa estar versionado em {SchemasOficiais.CaminhoNfe}");
    }

    [Fact]
    public async Task XmlAssinadoPeloMotor_PassaNoSchemaOficialDaSefaz()
    {
        // O teste central do XML-002. O documento validado é o que foi
        // transmitido — não uma remontagem feita para o teste passar.
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);

        var nota = await CreateService(db).EmitirParaComandaAsync(comanda.Id);

        nota.XmlTentativa.Should().NotBeNullOrWhiteSpace(
            "sem o XML transmitido persistido não há o que validar");

        var erros = ValidarContraSchemaOficial(nota.XmlTentativa!);

        erros.Should().BeEmpty(
            "o XML emitido precisa ser válido para o schema oficial ANTES de consumir numeração — " +
            "descobrir isso pela rejeição da SEFAZ é caro e tardio");
    }

    [Fact]
    public async Task XmlComGtinValido_PassaNoSchemaOficial()
    {
        // cEAN/cEANTrib com GTIN real exercita um domínio diferente do literal
        // "SEM GTIN" — e é o caminho que XML-001 abriu.
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db, gtin: "7891234567895");

        var nota = await CreateService(db).EmitirParaComandaAsync(comanda.Id);

        ValidarContraSchemaOficial(nota.XmlTentativa!).Should().BeEmpty();
    }

    [Fact]
    public async Task XmlComPagamentoDividido_PassaNoSchemaOficial()
    {
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db, ajustarVenda: c =>
        {
            c.SecondPaymentMethod         = "Pix";
            c.SecondPaymentAmountInCents  = 500;
        });

        var nota = await CreateService(db).EmitirParaComandaAsync(comanda.Id);

        ValidarContraSchemaOficial(nota.XmlTentativa!).Should().BeEmpty();
    }

    [Fact]
    public async Task XmlEmProducao_SemGruposIbsCbs_PassaNoSchemaOficial()
    {
        // Produção em 2026 não destaca IBS/CBS (RTC-001). O documento sem os
        // grupos também precisa ser válido — os grupos são opcionais no schema
        // enquanto a regra não os tornar obrigatórios.
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db, ambiente: AmbienteFiscal.Producao);

        var nota = await CreateService(db).EmitirParaComandaAsync(comanda.Id);

        nota.XmlTentativa.Should().NotBeNullOrWhiteSpace();
        nota.XmlTentativa!.Should().NotContain("<IBSCBS>",
            "em produção, em 2026, o destaque não sai — é o comportamento que RTC-001 preservou");
        ValidarContraSchemaOficial(nota.XmlTentativa!).Should().BeEmpty();
    }

    [Fact]
    public async Task XmlEmHomologacao_ComGruposIbsCbs_PassaNoSchemaOficial()
    {
        // O contrário: em homologação os grupos SAEM, e é aqui que se descobre
        // se o leiaute do RTC está montado do jeito que a SEFAZ espera.
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db, ambiente: AmbienteFiscal.Homologacao);

        var nota = await CreateService(db).EmitirParaComandaAsync(comanda.Id);

        nota.XmlTentativa!.Should().Contain("<IBSCBS>");
        ValidarContraSchemaOficial(nota.XmlTentativa!).Should().BeEmpty();
    }

    // ── Conduta em runtime (XML-002) ──────────────────────────────────────────

    /// <summary>Validador que reprova tudo — simula um documento inválido sem
    /// precisar quebrar o motor de propósito.</summary>
    private sealed class ValidadorQueReprova : INfceSchemaValidator
    {
        public bool Disponivel => true;
        public string? PacoteEmUso => "fixture-de-teste";
        public IReadOnlyList<string> Validar(string xmlNfe) =>
            new[] { "O elemento 'ide' apresenta conteúdo incompleto (erro sintético de teste)." };
    }

    private sealed class ValidadorIndisponivel : INfceSchemaValidator
    {
        public bool Disponivel => false;
        public string? PacoteEmUso => null;
        public IReadOnlyList<string> Validar(string xmlNfe) => Array.Empty<string>();
    }

    [Fact]
    public async Task DocumentoInvalido_NaoVaiParaContingencia_ENaoFicaPendente()
    {
        // A regra da seção 20 do plano: erro de schema NÃO é indisponibilidade da
        // SEFAZ. Entregar ao consumidor um cupom offline que jamais será
        // autorizado é o pior desfecho possível — pior que não emitir.
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);
        var service = new NfceEmissionService(
            db, CreateEncryptionService(), NullLogger<NfceEmissionService>.Instance,
            new ConfigurableFiscalTaxEngine(), new SefazMuda(), new ValidadorQueReprova());

        var nota = await service.EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().Be(NotaFiscalStatus.Rejeitada,
            "documento inválido é destino final, não pendência a reprocessar para sempre");
        nota.Status.Should().NotBe(NotaFiscalStatus.AutorizadaContingencia);
        nota.DhContingencia.Should().BeNull("erro de leiaute não é queda de rede");
        nota.XmlContingencia.Should().BeNull("nenhum cupom offline pode ter sido entregue");
        nota.MotivoRejeicao.Should().Contain("schema",
            "o motivo precisa dizer que foi o leiaute, não deixar o lojista achar que a SEFAZ recusou");
    }

    [Fact]
    public async Task DocumentoInvalido_PreservaONumeroParaInutilizacao()
    {
        // O número foi reservado antes da validação (a chave depende dele). Ele
        // não volta ao estoque: some da sequência e precisa ser inutilizado — que
        // é exatamente a lacuna que o alerta do CON-002 detecta.
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);
        var service = new NfceEmissionService(
            db, CreateEncryptionService(), NullLogger<NfceEmissionService>.Instance,
            new ConfigurableFiscalTaxEngine(), new SefazMuda(), new ValidadorQueReprova());

        var nota = await service.EmitirParaComandaAsync(comanda.Id);

        nota.Numero.Should().Be(900, "o número reservado não pode ser reaproveitado sem inutilização");
        nota.Serie.Should().Be(1);
    }

    [Fact]
    public async Task SemPacoteDeSchemas_EmissaoSegueNormalmente()
    {
        // Quem não versionou o pacote continua operando: a SEFAZ segue sendo a
        // validadora final. O que não pode é o sistema achar que validou.
        using var db = CreateDb();
        var comanda = await SeedLojaEVendaAsync(db);
        var service = new NfceEmissionService(
            db, CreateEncryptionService(), NullLogger<NfceEmissionService>.Instance,
            new ConfigurableFiscalTaxEngine(), new SefazMuda(), new ValidadorIndisponivel());

        var nota = await service.EmitirParaComandaAsync(comanda.Id);

        nota.Status.Should().NotBe(NotaFiscalStatus.Rejeitada);
        nota.XmlTentativa.Should().NotBeNullOrWhiteSpace("o documento chegou a ser montado e transmitido");
    }

    [Fact]
    public void ValidadorReal_ApontaParaOPacoteVersionado()
    {
        // Guarda contra o modo de falha mais silencioso do XML-002: o binário
        // publicado sem os XSDs ao lado. Aí `Disponivel` seria false em produção
        // e a barreira sumiria sem ninguém perceber.
        var validador = new NfceSchemaValidator(NullLogger<NfceSchemaValidator>.Instance);

        validador.Disponivel.Should().BeTrue(
            $"os XSDs precisam ser copiados para a saída do build ({NfceSchemaValidator.CaminhoConfigurado})");
        validador.PacoteEmUso.Should().Be(NfceSchemaValidator.PacoteLeiaute);
    }

    [Fact]
    public void ValidadorReal_ReprovaDocumentoInvalido()
    {
        var validador = new NfceSchemaValidator(NullLogger<NfceSchemaValidator>.Instance);

        validador.Validar("<NFe xmlns=\"http://www.portalfiscal.inf.br/nfe\"><lixo/></NFe>")
            .Should().NotBeEmpty();
    }

    [Fact]
    public void ValidadorReal_XmlMalformado_NaoExplode()
    {
        var validador = new NfceSchemaValidator(NullLogger<NfceSchemaValidator>.Instance);

        validador.Validar("<NFe><sem fechar>")
            .Should().ContainSingle().Which.Should().Contain("malformado");
    }

    [Fact]
    public void XmlAdulterado_EhReprovadoPeloSchema()
    {
        // Prova que a validação não é decorativa: um documento inválido precisa
        // falhar. Sem isso, "zero erros" acima não significaria nada.
        const string xmlInvalido =
            """
            <NFe xmlns="http://www.portalfiscal.inf.br/nfe">
              <infNFe versao="4.00" Id="NFe35260812345678000195650010000009001234567890">
                <ide><cUF>99</cUF></ide>
              </infNFe>
            </NFe>
            """;

        ValidarContraSchemaOficial(xmlInvalido).Should().NotBeEmpty();
    }
}

/// <summary>
/// Schemas oficiais versionados no repositório (XML-002). O caminho é resolvido
/// a partir do diretório do assembly de teste para funcionar igual no CI.
/// </summary>
internal static class SchemasOficiais
{
    /// <summary>Pacote de Liberação vigente para o leiaute NF-e/NFC-e 4.00.
    /// Trocar de pacote é trocar esta constante — e registrar a versão no
    /// dossiê de homologação.</summary>
    private const string PacoteLeiaute = "PL_010e_v1.02";

    public static readonly string CaminhoNfe = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
        "CardGameStore", "Schemas", PacoteLeiaute, "NFe", "nfe_v4.00.xsd"));

    private static readonly Lazy<XmlSchemaSet> Set = new(() =>
    {
        // No .NET moderno o XmlSchemaSet nasce com XmlResolver nulo (proteção
        // contra resolução remota). Sem um resolver local, o `xs:include` de
        // leiauteNFe_v4.00.xsd é silenciosamente ignorado e a compilação falha
        // com "TNFe is not declared" — que parece erro de schema e não é.
        // Resolver de arquivo local: os XSDs estão versionados no repositório e
        // nenhuma referência sai para a rede.
        var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        set.Add("http://www.portalfiscal.inf.br/nfe", CaminhoNfe);
        set.Compile();
        return set;
    });

    public static XmlSchemaSet Nfe => Set.Value;
}
