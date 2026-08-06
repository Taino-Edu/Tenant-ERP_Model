// =============================================================================
// DanfeXmlParserTests.cs — O DANFE precisa ser o retrato do XML autorizado.
//
// O defeito que originou este parser (DFE-001 do plano de go-live) era sutil e
// invisível em teste funcional: o cupom era remontado a partir da comanda e da
// configuração ATUAIS. A tela parecia certa, a nota estava autorizada, e ainda
// assim a reimpressão de uma venda antiga passava a divergir do documento que a
// SEFAZ recebeu assim que alguém corrigisse o endereço da loja ou o nome de um
// produto.
//
// Por isso o teste central aqui não é de formatação: é o de invariância. Ele
// prova que a representação depende só do XML — nada de banco, cadastro ou
// configuração pode influenciá-la.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class DanfeXmlParserTests
{
    private static string Fixture(string nome) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Nfce", nome));

    private static DanfeFiscalDto Parse(string nome) => DanfeXmlParser.Parse(Fixture(nome));

    // ── Identificação e emitente ─────────────────────────────────────────────

    [Fact]
    public void NotaAutorizada_LeIdentificacaoCompleta()
    {
        var danfe = Parse("nfce-normal-autorizada.xml");

        danfe.ChaveAcesso.Should().Be("99260800000000000191650010000000161123456789",
            "a chave vem do atributo Id sem o prefixo NFe");
        danfe.Serie.Should().Be(1);
        danfe.Numero.Should().Be(16);
        danfe.Ambiente.Should().Be(DanfeAmbiente.Producao);
        danfe.TipoEmissao.Should().Be(DanfeTipoEmissao.Normal);
        danfe.Situacao.Should().Be(DanfeSituacao.Autorizada);
        danfe.NaturezaOperacao.Should().Be("VENDA AO CONSUMIDOR");
        // 16:34:58 em Brasília (-03:00) é 19:34:58 UTC.
        danfe.EmitidoEm.Should().Be(new DateTime(2026, 8, 4, 19, 34, 58, DateTimeKind.Utc));
    }

    [Fact]
    public void Emitente_VemDoXmlEnaoDoCadastro()
    {
        var danfe = Parse("nfce-normal-autorizada.xml");

        danfe.Emitente.Cnpj.Should().Be("00000000000191");
        danfe.Emitente.RazaoSocial.Should().Be("LOJA FIXTURE DE TESTE LTDA");
        danfe.Emitente.Endereco.Municipio.Should().Be("Cidade Fixture");
        danfe.Emitente.Endereco.Linha.Should().Be("Rua das Fixtures 100, Centro, Cidade Fixture/SP");
    }

    [Fact]
    public void Protocolo_EhLidoDoGrupoProtNfe()
    {
        var danfe = Parse("nfce-normal-autorizada.xml");

        danfe.Protocolo.Should().NotBeNull();
        danfe.Protocolo!.Numero.Should().Be("999260000009075407");
        danfe.Protocolo.Status.Should().Be("100");
    }

    // ── Itens ────────────────────────────────────────────────────────────────

    [Fact]
    public void Item_TrazCodigoUnidadeEQuantidade_QueOCupomAgregadoNaoMostrava()
    {
        var danfe = Parse("nfce-normal-autorizada.xml");

        var item = danfe.Itens.Should().ContainSingle().Subject;
        item.Numero.Should().Be(1);
        item.Codigo.Should().Be("SKU-0001");
        item.Descricao.Should().Be("Baralho de teste");
        item.UnidadeComercial.Should().Be("UN");
        item.Quantidade.Should().Be(1m);
        item.ValorUnitario.Should().Be(25m);
        item.ValorTotal.Should().Be(25m);
        item.Ncm.Should().Be("95044000");
        item.TributosAproximados.Should().Be(3.75m);
    }

    [Fact]
    public void SemGtin_NaoViraCodigoDeBarras()
    {
        // "SEM GTIN" é preenchimento obrigatório do leiaute, não um GTIN.
        // Imprimir esse literal como código de barras seria informação falsa.
        var semGtin = Parse("nfce-normal-autorizada.xml").Itens[0];
        semGtin.Gtin.Should().BeNull();

        var comGtin = Parse("nfce-homologacao.xml").Itens[1];
        comGtin.Gtin.Should().Be("7891234567895");
    }

    [Fact]
    public void Totais_ContamOsItensEUsamOsValoresDoXml()
    {
        var danfe = Parse("nfce-homologacao.xml");

        danfe.Totais.QuantidadeItens.Should().Be(2, "é divisão obrigatória do manual");
        danfe.Totais.ValorProdutos.Should().Be(55m);
        danfe.Totais.ValorTotal.Should().Be(55m);
    }

    // ── Consumidor ───────────────────────────────────────────────────────────

    [Fact]
    public void SemGrupoDest_ConsumidorFicaExplicitamenteNaoIdentificado()
    {
        // Ausência do grupo não pode virar divisão omitida: o manual exige a
        // indicação de consumidor não identificado.
        var danfe = Parse("nfce-normal-autorizada.xml");

        danfe.Consumidor.Identificado.Should().BeFalse();
        danfe.Consumidor.Cpf.Should().BeNull();
    }

    [Fact]
    public void ComCpf_ConsumidorEhIdentificado()
    {
        var danfe = Parse("nfce-pagamentos-mistos.xml");

        danfe.Consumidor.Identificado.Should().BeTrue();
        danfe.Consumidor.Cpf.Should().Be("00000000191");
        danfe.Consumidor.Nome.Should().Be("CONSUMIDOR FIXTURE");
    }

    // ── Pagamentos ───────────────────────────────────────────────────────────

    [Fact]
    public void PagamentosMistos_PreservamCadaMeioComSeuValorETroco()
    {
        var danfe = Parse("nfce-pagamentos-mistos.xml");

        danfe.Pagamentos.Should().HaveCount(3);
        danfe.Pagamentos.Select(p => p.CodigoTPag).Should().Equal("17", "05", "19");
        danfe.Pagamentos.Sum(p => p.Valor).Should().Be(460m);
        danfe.Pagamentos[1].DescricaoXPag.Should().Be("Crediario proprio da loja");
        danfe.Troco.Should().Be(10m);
    }

    [Fact]
    public void Parser_NaoTraduzOCodigoDePagamento()
    {
        // Traduzir "19" para "Cashback" aqui esconderia um tPag errado atrás de
        // um rótulo bonito. O código cru sobe; a tela decide como rotular.
        var danfe = Parse("nfce-pagamentos-mistos.xml");

        danfe.Pagamentos.Select(p => p.CodigoTPag).Should().OnlyContain(c => c.All(char.IsDigit));
    }

    [Fact]
    public void DadosDoCartao_SaoLidosQuandoPresentes()
    {
        var pagamento = Parse("nfce-ibscbs.xml").Pagamentos.Should().ContainSingle().Subject;

        pagamento.CodigoTPag.Should().Be("03");
        pagamento.Bandeira.Should().Be("01");
    }

    // ── Ambiente e contingência ──────────────────────────────────────────────

    [Fact]
    public void Homologacao_ExigeAvisoDeDocumentoSemValorFiscal()
    {
        var danfe = Parse("nfce-homologacao.xml");

        danfe.Ambiente.Should().Be(DanfeAmbiente.Homologacao);
        danfe.ExigeAvisoSemValorFiscal.Should().BeTrue();
        // O xProd especial do primeiro item é exigência do XML e continua lá —
        // é adicional ao aviso da via impressa, não substituto dele.
        danfe.Itens[0].Descricao.Should().Contain("SEM VALOR FISCAL");
    }

    [Fact]
    public void Contingencia_SemProtocolo_TrazMomentoEJustificativa()
    {
        var danfe = Parse("nfce-contingencia.xml");

        danfe.TipoEmissao.Should().Be(DanfeTipoEmissao.ContingenciaOffline);
        danfe.EmContingencia.Should().BeTrue();
        danfe.Situacao.Should().Be(DanfeSituacao.ContingenciaSemProtocolo);
        danfe.Protocolo.Should().BeNull();
        danfe.Contingencia.Should().NotBeNull();
        danfe.Contingencia!.Justificativa.Should().Contain("Falha de comunicacao");
        danfe.Contingencia.DataHora.Should().Be(new DateTime(2026, 8, 4, 23, 4, 30, DateTimeKind.Utc));
    }

    [Fact]
    public void ContingenciaAutorizadaDepois_MantemODocumentoEntregueEAcrescentaOProtocolo()
    {
        // O consumidor já levou a via offline. A reimpressão posterior não pode
        // reescrever o documento: mesma chave, mesma emissão, mesma justificativa
        // — muda apenas o protocolo, que passa a existir.
        var offline = Parse("nfce-contingencia.xml");
        var autorizada = Parse("nfce-contingencia-autorizada.xml");

        autorizada.ChaveAcesso.Should().Be(offline.ChaveAcesso);
        autorizada.EmitidoEm.Should().Be(offline.EmitidoEm);
        autorizada.TipoEmissao.Should().Be(DanfeTipoEmissao.ContingenciaOffline);
        autorizada.Contingencia!.Justificativa.Should().Be(offline.Contingencia!.Justificativa);
        autorizada.Itens.Should().BeEquivalentTo(offline.Itens);
        autorizada.Totais.Should().Be(offline.Totais);

        autorizada.Situacao.Should().Be(DanfeSituacao.Autorizada);
        autorizada.Protocolo!.Numero.Should().Be("999260000009075410");
    }

    // ── QR Code e complementares ─────────────────────────────────────────────

    [Fact]
    public void QrCode_VemDoXmlSemSerRemontado()
    {
        // Remontar a URL exigiria CSC e assinatura — e qualquer divergência entre
        // o QR impresso e o do XML é a rejeição 397.
        var danfe = Parse("nfce-normal-autorizada.xml");

        danfe.QrCodeUrl.Should().StartWith("https://").And.Contain("99260800000000000191650010000000161123456789");
        danfe.UrlConsultaChave.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void InformacoesComplementares_SaoPreservadas()
    {
        Parse("nfce-normal-autorizada.xml").InformacoesComplementares
            .Should().Contain("Lei 12.741/2012");
    }

    // ── Reforma tributária ───────────────────────────────────────────────────

    [Fact]
    public void GruposIbsCbs_NaoQuebramOParser()
    {
        // Compatibilidade estrutural: as tags novas são toleradas. Enquanto o
        // manual do DANFE não exigir destaque, elas não entram na representação
        // — e não devem ser inventadas pelo frontend (DFE-010).
        var danfe = Parse("nfce-ibscbs.xml");

        danfe.Itens.Should().ContainSingle();
        danfe.Totais.ValorTotal.Should().Be(100m);
        danfe.Totais.TributosAproximados.Should().Be(15m);
    }

    // ── Invariância: o motivo de tudo isto existir ───────────────────────────

    [Fact]
    public void MesmoXml_ProduzDocumentoIdentico_IndependenteDeQualquerEstadoExterno()
    {
        // DFE-001: a representação é função pura do XML. Se este teste passar a
        // falhar, alguém religou uma dependência de cadastro/venda no caminho.
        var xml = Fixture("nfce-pagamentos-mistos.xml");

        var primeira = DanfeXmlParser.Parse(xml);
        var segunda = DanfeXmlParser.Parse(xml);

        segunda.Should().BeEquivalentTo(primeira);
    }

    [Fact]
    public void XmlComEspacosEQuebras_NaoAlteraOConteudoFiscal()
    {
        var original = Parse("nfce-normal-autorizada.xml");
        var reformatado = DanfeXmlParser.Parse(
            Fixture("nfce-normal-autorizada.xml").Replace("\n", "\r\n  "));

        reformatado.Should().BeEquivalentTo(original);
    }

    // ── Entradas inválidas ───────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void XmlVazio_ExplicaOProblema(string xml)
    {
        var act = () => DanfeXmlParser.Parse(xml);

        act.Should().Throw<DanfeXmlInvalidoException>().WithMessage("*vazio*");
    }

    [Fact]
    public void XmlMalformado_NaoVazaExcecaoDeParser()
    {
        var act = () => DanfeXmlParser.Parse("<nfeProc><NFe>");

        act.Should().Throw<DanfeXmlInvalidoException>().WithMessage("*malformado*");
    }

    [Fact]
    public void OutroDocumento_NaoEhAceitoComoNfce()
    {
        var act = () => DanfeXmlParser.Parse("<?xml version=\"1.0\"?><pedido><item/></pedido>");

        act.Should().Throw<DanfeXmlInvalidoException>().WithMessage("*não é uma NFC-e*");
    }

    [Fact]
    public void Modelo55_EhRecusadoPorqueOManualEhOutro()
    {
        var xml = Fixture("nfce-normal-autorizada.xml").Replace("<mod>65</mod>", "<mod>55</mod>");

        var act = () => DanfeXmlParser.Parse(xml);

        act.Should().Throw<DanfeXmlInvalidoException>().WithMessage("*modelo 55*");
    }

    [Fact]
    public void EntidadeExterna_NaoEhResolvida()
    {
        // XXE: sem DtdProcessing.Prohibit, este XML leria um arquivo do servidor.
        // O parser é a porta por onde XML de terceiro entra no sistema.
        const string ataque = """
            <?xml version="1.0"?>
            <!DOCTYPE nfeProc [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe"><NFe>&xxe;</NFe></nfeProc>
            """;

        var act = () => DanfeXmlParser.Parse(ataque);

        act.Should().Throw<DanfeXmlInvalidoException>();
    }
}
