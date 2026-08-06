// =============================================================================
// DanfeXmlParser.cs — Converte o XML fiscal no DTO que a via do consumidor usa.
//
// Regra única e inegociável deste arquivo: a saída depende SOMENTE do XML de
// entrada. Não há acesso a banco, cadastro, configuração ou venda. É isso que
// torna a reimpressão estável — o mesmo XML produz o mesmo documento hoje e
// daqui a cinco anos, mesmo que a loja mude de nome, endereço ou catálogo
// (DFE-001 do plano de go-live).
//
// Aceita as duas formas do artefato fiscal:
//   • <nfeProc> — nota autorizada, com protocolo;
//   • <NFe>     — XML assinado entregue em contingência offline, ainda sem protocolo.
//
// O parser é deliberadamente tolerante a campos ausentes e intolerante a
// entrada malformada: um grupo opcional que não veio vira null (a representação
// decide como mostrar a ausência), mas um XML que não é NFC-e é recusado na
// hora, com mensagem que diz o que veio no lugar.
// =============================================================================

using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using CardGameStore.DTOs;

namespace CardGameStore.Services.Implementations;

/// <summary>Entrada que não é um XML de NFC-e utilizável.</summary>
public class DanfeXmlInvalidoException : Exception
{
    public DanfeXmlInvalidoException(string message) : base(message) { }
}

public static class DanfeXmlParser
{
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    /// <summary>Modelo 65 é a NFC-e; 55 é a NF-e e não é representada por este DANFE.</summary>
    private const string ModeloNfce = "65";

    /// <summary>
    /// Teto de tamanho do XML aceito. Uma NFC-e real com centenas de itens não
    /// passa de algumas centenas de KB; o limite existe para que um arquivo
    /// corrompido ou hostil não vire consumo de memória no servidor.
    /// </summary>
    private const int TamanhoMaximoBytes = 2 * 1024 * 1024;

    public static DanfeFiscalDto Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new DanfeXmlInvalidoException("XML fiscal vazio — não há documento a representar.");
        if (xml.Length > TamanhoMaximoBytes)
            throw new DanfeXmlInvalidoException(
                $"XML fiscal acima do limite aceito ({TamanhoMaximoBytes / 1024} KB).");

        var doc = CarregarSeguro(xml);
        var raiz = doc.Root ?? throw new DanfeXmlInvalidoException("XML fiscal sem elemento raiz.");

        // nfeProc envelopa NFe + protNFe; um XML assinado offline traz NFe direto.
        var nfe = raiz.Name == Nfe + "nfeProc" ? raiz.Element(Nfe + "NFe") : raiz;
        if (nfe is null || nfe.Name != Nfe + "NFe")
            throw new DanfeXmlInvalidoException(
                $"XML fiscal não é uma NFC-e: elemento raiz \"{raiz.Name.LocalName}\" inesperado.");

        var infNFe = nfe.Element(Nfe + "infNFe")
            ?? throw new DanfeXmlInvalidoException("XML fiscal sem o grupo infNFe.");
        var ide = infNFe.Element(Nfe + "ide")
            ?? throw new DanfeXmlInvalidoException("XML fiscal sem o grupo ide.");

        var modelo = Texto(ide, "mod");
        if (modelo is not null && modelo != ModeloNfce)
            throw new DanfeXmlInvalidoException(
                $"XML fiscal é do modelo {modelo}; o DANFE NFC-e representa apenas o modelo 65.");

        var protocolo = LerProtocolo(raiz);
        var tipoEmissao = LerTipoEmissao(ide);
        var contingencia = tipoEmissao == DanfeTipoEmissao.ContingenciaOffline
            ? new DanfeContingenciaDto(DataHora(ide, "dhCont"), Texto(ide, "xJust"))
            : null;

        var itens = LerItens(infNFe);

        return new DanfeFiscalDto(
            ChaveAcesso:   LerChave(infNFe),
            Ambiente:      Texto(ide, "tpAmb") == "2" ? DanfeAmbiente.Homologacao : DanfeAmbiente.Producao,
            TipoEmissao:   tipoEmissao,
            Situacao:      protocolo is null
                               ? DanfeSituacao.ContingenciaSemProtocolo
                               : DanfeSituacao.Autorizada,
            Serie:         Inteiro(ide, "serie") ?? 0,
            Numero:        Inteiro(ide, "nNF") ?? 0,
            EmitidoEm:     DataHora(ide, "dhEmi"),
            NaturezaOperacao: Texto(ide, "natOp"),
            Emitente:      LerEmitente(infNFe),
            Consumidor:    LerConsumidor(infNFe),
            Itens:         itens,
            Totais:        LerTotais(infNFe, itens.Count),
            Pagamentos:    LerPagamentos(infNFe),
            Troco:         LerTroco(infNFe),
            Protocolo:     protocolo,
            Contingencia:  contingencia,
            InformacoesComplementares: Texto(infNFe.Element(Nfe + "infAdic"), "infCpl"),
            QrCodeUrl:         Texto(nfe.Element(Nfe + "infNFeSupl"), "qrCode"),
            UrlConsultaChave:  Texto(nfe.Element(Nfe + "infNFeSupl"), "urlChave"));
    }

    /// <summary>
    /// Carrega o XML com DTD e resolução de entidades externas desligadas. Sem
    /// isso, um XML de origem não confiável poderia ler arquivos do servidor ou
    /// forçar expansão de entidades (XXE / billion laughs) — e este parser é
    /// justamente o ponto onde XML de terceiro entra no sistema.
    /// </summary>
    private static XDocument CarregarSeguro(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };

        try
        {
            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);
            return XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new DanfeXmlInvalidoException($"XML fiscal malformado: {ex.Message}");
        }
    }

    // ── Grupos ────────────────────────────────────────────────────────────────

    /// <summary>A chave está no atributo Id do infNFe, prefixada por "NFe".</summary>
    private static string? LerChave(XElement infNFe)
    {
        var id = infNFe.Attribute("Id")?.Value;
        if (string.IsNullOrWhiteSpace(id)) return null;
        return id.StartsWith("NFe", StringComparison.OrdinalIgnoreCase) ? id[3..] : id;
    }

    private static DanfeTipoEmissao LerTipoEmissao(XElement ide) => Texto(ide, "tpEmis") switch
    {
        "1" => DanfeTipoEmissao.Normal,
        "9" => DanfeTipoEmissao.ContingenciaOffline,
        null => DanfeTipoEmissao.Normal,
        _ => DanfeTipoEmissao.Outra,
    };

    private static DanfeProtocoloDto? LerProtocolo(XElement raiz)
    {
        var infProt = raiz.Element(Nfe + "protNFe")?.Element(Nfe + "infProt");
        if (infProt is null) return null;

        var numero = Texto(infProt, "nProt");
        // protNFe presente mas sem número de protocolo não é autorização.
        if (string.IsNullOrWhiteSpace(numero)) return null;

        return new DanfeProtocoloDto(
            numero, DataHora(infProt, "dhRecbto"), Texto(infProt, "cStat"), Texto(infProt, "xMotivo"));
    }

    private static DanfeEmitenteDto LerEmitente(XElement infNFe)
    {
        var emit = infNFe.Element(Nfe + "emit");
        return new DanfeEmitenteDto(
            Cnpj:              Texto(emit, "CNPJ"),
            RazaoSocial:       Texto(emit, "xNome"),
            NomeFantasia:      Texto(emit, "xFant"),
            InscricaoEstadual: Texto(emit, "IE"),
            Endereco:          LerEndereco(emit?.Element(Nfe + "enderEmit")));
    }

    private static DanfeConsumidorDto LerConsumidor(XElement infNFe)
    {
        var dest = infNFe.Element(Nfe + "dest");
        if (dest is null) return DanfeConsumidorDto.NaoIdentificado;

        return new DanfeConsumidorDto(
            Cpf:      Texto(dest, "CPF"),
            Cnpj:     Texto(dest, "CNPJ"),
            Nome:     Texto(dest, "xNome"),
            Endereco: dest.Element(Nfe + "enderDest") is { } ender ? LerEndereco(ender) : null);
    }

    private static DanfeEnderecoDto LerEndereco(XElement? ender) => new(
        Logradouro:  Texto(ender, "xLgr"),
        Numero:      Texto(ender, "nro"),
        Complemento: Texto(ender, "xCpl"),
        Bairro:      Texto(ender, "xBairro"),
        Municipio:   Texto(ender, "xMun"),
        Uf:          Texto(ender, "UF"),
        Cep:         Texto(ender, "CEP"));

    private static List<DanfeItemDto> LerItens(XElement infNFe)
    {
        var itens = new List<DanfeItemDto>();
        foreach (var det in infNFe.Elements(Nfe + "det"))
        {
            var prod = det.Element(Nfe + "prod");
            var gtin = Texto(prod, "cEAN");

            itens.Add(new DanfeItemDto(
                Numero:           int.TryParse(det.Attribute("nItem")?.Value, out var n) ? n : itens.Count + 1,
                Codigo:           Texto(prod, "cProd"),
                Descricao:        Texto(prod, "xProd"),
                Ncm:              Texto(prod, "NCM"),
                Cfop:             Texto(prod, "CFOP"),
                UnidadeComercial: Texto(prod, "uCom"),
                Quantidade:       Decimal(prod, "qCom") ?? 0m,
                ValorUnitario:    Decimal(prod, "vUnCom") ?? 0m,
                ValorTotal:       Decimal(prod, "vProd") ?? 0m,
                Desconto:         Decimal(prod, "vDesc") ?? 0m,
                // "SEM GTIN" é o preenchimento obrigatório quando o produto não
                // tem código de barras — não é um GTIN e não deve ser impresso.
                Gtin:             string.Equals(gtin, "SEM GTIN", StringComparison.OrdinalIgnoreCase) ? null : gtin,
                TributosAproximados: Decimal(det.Element(Nfe + "imposto"), "vTotTrib")));
        }
        return itens;
    }

    private static DanfeTotaisDto LerTotais(XElement infNFe, int quantidadeItens)
    {
        var icmsTot = infNFe.Element(Nfe + "total")?.Element(Nfe + "ICMSTot");
        return new DanfeTotaisDto(
            QuantidadeItens:     quantidadeItens,
            ValorProdutos:       Decimal(icmsTot, "vProd") ?? 0m,
            ValorDesconto:       Decimal(icmsTot, "vDesc") ?? 0m,
            ValorTotal:          Decimal(icmsTot, "vNF") ?? 0m,
            TributosAproximados: Decimal(icmsTot, "vTotTrib"));
    }

    private static List<DanfePagamentoDto> LerPagamentos(XElement infNFe)
    {
        var pagamentos = new List<DanfePagamentoDto>();
        foreach (var pag in infNFe.Elements(Nfe + "pag"))
        foreach (var detPag in pag.Elements(Nfe + "detPag"))
        {
            var card = detPag.Element(Nfe + "card");
            pagamentos.Add(new DanfePagamentoDto(
                CodigoTPag:    Texto(detPag, "tPag") ?? "",
                Valor:         Decimal(detPag, "vPag") ?? 0m,
                DescricaoXPag: Texto(detPag, "xPag"),
                Bandeira:      Texto(card, "tBand"),
                Autorizacao:   Texto(card, "cAut")));
        }
        return pagamentos;
    }

    private static decimal? LerTroco(XElement infNFe) =>
        infNFe.Elements(Nfe + "pag").Select(pag => Decimal(pag, "vTroco")).FirstOrDefault(v => v is not null);

    // ── Leitura primitiva ─────────────────────────────────────────────────────

    private static string? Texto(XElement? pai, string nome)
    {
        var valor = pai?.Element(Nfe + nome)?.Value;
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    private static int? Inteiro(XElement? pai, string nome) =>
        int.TryParse(Texto(pai, nome), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>
    /// O leiaute da NF-e usa ponto como separador decimal, independente da
    /// cultura do servidor — daí InvariantCulture fixa.
    /// </summary>
    private static decimal? Decimal(XElement? pai, string nome) =>
        decimal.TryParse(Texto(pai, nome), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>
    /// Datas no leiaute vêm com offset (ex.: 2026-08-04T16:34:58-03:00). Converte
    /// para o instante UTC e deixa a apresentação decidir o fuso de exibição.
    /// </summary>
    private static DateTime? DataHora(XElement? pai, string nome)
    {
        var texto = Texto(pai, nome);
        if (texto is null) return null;
        return DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var v)
            ? v.UtcDateTime
            : null;
    }
}
