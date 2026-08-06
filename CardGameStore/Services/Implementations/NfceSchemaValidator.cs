// =============================================================================
// NfceSchemaValidator.cs — XML-002: valida o documento contra o XSD oficial da
// SEFAZ antes de transmitir.
//
// Por que a validação é NOSSA e não a da lib: o `ValidarSchemas` do DFe.NET
// resolve o arquivo pelo serviço (enviNFe_v4.00.xsd para autorização) num
// diretório achatado. Dois problemas concretos com isso:
//
//   1. os pacotes publicados hoje pelo portal são INCREMENTAIS — 010e_v1.02 tem
//      cinco arquivos e não inclui `enviNFe_v4.00.xsd`, então a validação da
//      lib falharia por arquivo ausente, não por documento inválido;
//   2. `tiposBasico_v4.00.xsd` tem conteúdo DIFERENTE entre as pastas `Evento/`
//      e `NFe/` do mesmo pacote oficial. Achatar tudo num diretório só —
//      que é o que a lib espera — sobrescreveria um pelo outro em silêncio, e
//      uma validação fiscal errada é pior do que validação nenhuma.
//
// Validando a `<NFe>` assinada contra `nfe_v4.00.xsd` com o XmlSchemaSet do
// .NET, cada pacote é lido na sua própria pasta, os `xs:include` relativos
// resolvem como a SEFAZ publicou, e o erro que sai é o do documento.
//
// Ausência de schema NÃO impede emitir: quem não versionou o pacote continua
// operando como antes (a SEFAZ segue sendo a validadora final). O que não pode
// acontecer é o sistema achar que validou quando não validou — daí `Disponivel`
// ser público e aparecer no /api/fiscal/saude.
// =============================================================================

using System.Xml;
using System.Xml.Schema;

namespace CardGameStore.Services.Implementations;

public interface INfceSchemaValidator
{
    /// <summary>Há schema oficial carregado? Falso = nenhuma validação local acontece.</summary>
    bool Disponivel { get; }

    /// <summary>Pacote de liberação em uso — evidência exigida pela seção 17.1 do plano.</summary>
    string? PacoteEmUso { get; }

    /// <summary>
    /// Pasta dos schemas de EVENTO (cancelamento), ou null se não versionada.
    ///
    /// Aqui, diferente da autorização, quem valida é a própria lib: o
    /// `RecepcaoEventoCancelamento` monta e transmite numa chamada só, sem expor
    /// o XML antes. E dá para usar o `DiretorioSchemas` dela justamente porque a
    /// pasta `Evento/` do pacote oficial já é autocontida — tem o seu próprio
    /// `tiposBasico`, então não existe o conflito de nomes que impediu esse
    /// caminho na autorização.
    /// </summary>
    string? DiretorioEventos { get; }

    /// <summary>
    /// Pasta dos schemas de INUTILIZAÇÃO, ou null se não versionada. Mesma
    /// mecânica do evento: `NfeInutilizacao` monta e transmite numa chamada só,
    /// então quem valida é a lib.
    /// </summary>
    string? DiretorioInutilizacao { get; }

    /// <summary>
    /// Pasta dos schemas de AUTORIZAÇÃO, para a lib validar o LOTE (`enviNFe`) —
    /// o envelope que a nossa validação própria não alcança, porque ela vê a
    /// `&lt;NFe&gt;` e o envelope só existe dentro da lib.
    /// </summary>
    string? DiretorioAutorizacao { get; }

    /// <summary>Erros do documento. Lista vazia = válido (ou validação indisponível).</summary>
    IReadOnlyList<string> Validar(string xmlNfe);
}

public sealed class NfceSchemaValidator : INfceSchemaValidator
{
    private const string NamespaceNfe = "http://www.portalfiscal.inf.br/nfe";

    /// <summary>Pacote de liberação vigente. Trocar de pacote é trocar esta
    /// constante e a pasta correspondente — e registrar a versão no dossiê de
    /// homologação, porque é ela que dá procedência à validação.</summary>
    internal const string PacoteLeiaute = "PL_010e_v1.02";

    /// <summary>Sobrescreve o caminho do XSD (caminho completo do
    /// nfe_v4.00.xsd). Existe para o contêiner poder montar os schemas fora da
    /// imagem sem recompilar.</summary>
    private const string VariavelDeAmbiente = "FISCAL_SCHEMA_NFE";

    private readonly ILogger<NfceSchemaValidator> _logger;
    private readonly Lazy<XmlSchemaSet?> _schemas;

    public NfceSchemaValidator(ILogger<NfceSchemaValidator> logger)
    {
        _logger  = logger;
        _schemas = new Lazy<XmlSchemaSet?>(Carregar, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool Disponivel => _schemas.Value is not null;

    /// <summary>
    /// Só devolve a pasta se o arquivo que a lib efetivamente procura estiver
    /// lá. Apontar `DiretorioSchemas` para uma pasta sem `envEvento_v1.00.xsd`
    /// faria a lib falhar por arquivo ausente na hora de cancelar — trocaria uma
    /// validação que não existe por um cancelamento que não acontece.
    /// </summary>
    public string? DiretorioEventos =>
        File.Exists(Path.Combine(CaminhoDiretorioEventos, ArquivoEnvEvento))
            ? CaminhoDiretorioEventos
            : null;

    /// <inheritdoc />
    public string? DiretorioInutilizacao =>
        File.Exists(Path.Combine(CaminhoDiretorioInutilizacao, ArquivoInutNfe))
            ? CaminhoDiretorioInutilizacao
            : null;

    /// <inheritdoc />
    public string? DiretorioAutorizacao =>
        File.Exists(Path.Combine(CaminhoDiretorioAutorizacao, ArquivoEnviNfe))
            ? CaminhoDiretorioAutorizacao
            : null;

    public string? PacoteEmUso => Disponivel ? PacoteLeiaute : null;

    internal static string CaminhoPadrao => Path.Combine(
        AppContext.BaseDirectory, "Schemas", PacoteLeiaute, "NFe", "nfe_v4.00.xsd");

    /// <summary>Pacote que traz os schemas de evento e de consulta. Separado do
    /// leiaute de propósito: os arquivos não são intercambiáveis (ver LEIA-ME).</summary>
    internal const string PacoteEventos = "PL_010d_v1.03";

    /// <summary>Arquivos que a lib procura, por serviço. Sondados na própria lib
    /// (ver NfceSchemaValidacaoTests) — não são convenção adivinhada.</summary>
    private const string ArquivoEnvEvento = "envEvento_v1.00.xsd";
    private const string ArquivoInutNfe   = "inutNFe_v4.00.xsd";
    private const string ArquivoEnviNfe   = "enviNFe_v4.00.xsd";

    private static string CaminhoDiretorioEventos => Path.Combine(
        AppContext.BaseDirectory, "Schemas", PacoteEventos, "Evento");

    private static string CaminhoDiretorioInutilizacao => Path.Combine(
        AppContext.BaseDirectory, "Schemas", PacoteEventos, "NFe");

    private static string CaminhoDiretorioAutorizacao => Path.Combine(
        AppContext.BaseDirectory, "Schemas", PacoteLeiaute, "NFe");

    internal static string CaminhoConfigurado
    {
        get
        {
            var custom = Environment.GetEnvironmentVariable(VariavelDeAmbiente);
            return string.IsNullOrWhiteSpace(custom) ? CaminhoPadrao : custom;
        }
    }

    private XmlSchemaSet? Carregar()
    {
        var caminho = CaminhoConfigurado;

        if (!File.Exists(caminho))
        {
            // Warning, não Error: operar sem o pacote é um estado legítimo (a
            // SEFAZ continua validando). O que seria grave é isso passar
            // despercebido, e por isso também sai no /api/fiscal/saude.
            _logger.LogWarning(
                "Validação de schema XSD desabilitada: {Caminho} não existe. As NFC-e continuam " +
                "sendo emitidas, mas erros de leiaute só serão descobertos pela rejeição da SEFAZ. " +
                "Versione o pacote de liberação em CardGameStore/Schemas ou aponte {Variavel}.",
                caminho, VariavelDeAmbiente);
            return null;
        }

        try
        {
            // O XmlSchemaSet do .NET moderno nasce com XmlResolver nulo (proteção
            // contra resolução remota). Sem um resolver, o `xs:include` de
            // leiauteNFe_v4.00.xsd é ignorado e a compilação falha com
            // "TNFe is not declared" — que parece schema corrompido e não é.
            // Os XSDs são locais e versionados; nada sai para a rede.
            var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
            set.Add(NamespaceNfe, caminho);
            set.Compile();

            _logger.LogInformation(
                "Validação de schema XSD ativa — pacote {Pacote} ({Caminho}).", PacoteLeiaute, caminho);
            return set;
        }
        catch (Exception ex)
        {
            // Schema ilegível é problema de instalação, não do documento: degrada
            // para "sem validação" em vez de impedir a loja de emitir.
            _logger.LogError(ex,
                "Falha ao compilar o schema oficial em {Caminho}. A validação local ficará desabilitada.",
                caminho);
            return null;
        }
    }

    public IReadOnlyList<string> Validar(string xmlNfe)
    {
        var schemas = _schemas.Value;
        if (schemas is null || string.IsNullOrWhiteSpace(xmlNfe)) return Array.Empty<string>();

        var erros = new List<string>();
        try
        {
            var documento = new XmlDocument();
            documento.LoadXml(xmlNfe);
            documento.Schemas = schemas;
            documento.Validate((_, e) => erros.Add(e.Message));
        }
        catch (XmlException ex)
        {
            // XML malformado não chega a ser questão de schema, mas é igualmente
            // impeditivo — e precisa aparecer com a mesma clareza.
            erros.Add($"XML malformado: {ex.Message}");
        }
        catch (XmlSchemaValidationException ex)
        {
            // Validate() lança em vez de acumular quando o erro é na raiz.
            erros.Add(ex.Message);
        }

        return erros;
    }
}
