// =============================================================================
// IbptTabelaCsvImporterTests.cs — importar a tabela do arquivo oficial.
//
// A fixture NÃO é inventada: são linhas recortadas do
// `TabelaIBPTaxSP26.1.L.csv` que o IBPT entrega no pacote por CNPJ, incluindo
// os NCMs reais dos produtos da loja de homologação e uma linha com exceção
// fiscal (`ex=01`).
//
// Isso importa porque o formato tem armadilhas que só o arquivo real revela:
// decimal com PONTO (13.45), descrição entre aspas contendo vírgulas, CRLF, e
// alíquotas que variam mais do que se supõe — "Cartas de jogar" tem estadual de
// 25%, não os 18% da maioria. Um parser testado contra fixture imaginada
// passaria e produziria imposto errado.
// =============================================================================

using System.Text;
using CardGameStore.Services.Implementations;

namespace CardGameStore.Tests.Services;

public class IbptTabelaCsvImporterTests
{
    private static readonly string CaminhoFixture = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "Ibpt", "TabelaIBPTaxSP26.1.L.csv"));

    private static ResultadoLeituraCsvIbpt LerFixture()
    {
        using var arquivo = File.OpenRead(CaminhoFixture);
        return IbptTabelaCsvImporter.Ler(arquivo);
    }

    [Fact]
    public void Ler_ArquivoOficial_ExtraiAsAliquotasExatas()
    {
        var resultado = LerFixture();

        var camiseta = resultado.Linhas.Single(l => l.Ncm == "61091000");
        camiseta.NacionalFederal.Should().Be(13.45m);
        camiseta.ImportadoFederal.Should().Be(18.61m);
        camiseta.Estadual.Should().Be(18.00m);
        camiseta.Municipal.Should().Be(0m);
    }

    [Fact]
    public void Ler_AliquotaEstadualQueFogeDoPadrao_EhLidaComoEsta()
    {
        // "Cartas de jogar" tem estadual de 25%, e não os 18% da maioria das
        // linhas. É o tipo de detalhe que um parser validado contra fixture
        // imaginada erraria sem ninguém perceber.
        var resultado = LerFixture();

        resultado.Linhas.Single(l => l.Ncm == "95044000").Estadual.Should().Be(25.00m);
    }

    [Fact]
    public void Ler_DecimalComPonto_NaoEhInterpretadoNaCulturaDoProcesso()
    {
        // Numa máquina pt-BR, ler "13.45" na cultura corrente daria 1345.
        var resultado = LerFixture();

        resultado.Linhas.Should().OnlyContain(l => l.NacionalFederal < 100m);
    }

    [Fact]
    public void Ler_LinhaComExcecaoFiscal_EhIgnorada()
    {
        // `ex` preenchido é exceção do NCM, com regra própria que este motor não
        // aplica. Importar como se fosse a linha comum daria alíquota errada a
        // quem usa o NCM sem exceção.
        var resultado = LerFixture();

        resultado.Linhas.Should().NotContain(l => l.Ncm == "02109100");
        resultado.LinhasIgnoradas.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Ler_DescricaoComVirgulaEntreAspas_NaoQuebraAsColunas()
    {
        // "Camisetas t-shirts,etc.de malha de algodao" tem vírgula dentro das
        // aspas. Um Split(';') cru sobreviveria, mas um Split(',') ou um parser
        // que ignore aspas deslocaria todas as colunas seguintes.
        var resultado = LerFixture();

        resultado.Linhas.Single(l => l.Ncm == "61099000").Estadual.Should().Be(18.00m);
    }

    [Fact]
    public void Ler_ExtraiVersaoEVigenciaParaRastreabilidade()
    {
        var resultado = LerFixture();

        resultado.Versao.Should().Be("26.1.L");
        resultado.VigenciaInicio.Should().Be(new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc));
        resultado.VigenciaFim.Should().Be(new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc));
        resultado.Linhas.Should().OnlyContain(l => l.Chave == "42CA5A");
    }

    [Fact]
    public void Ler_ArquivoQueNaoEhATabela_RecusaComMensagemUtil()
    {
        // O mesmo pacote traz .xlsx e .pdf. Confundir arquivo é o erro mais
        // provável de quem importa, e a mensagem precisa dizer qual pegar.
        using var conteudo = new MemoryStream(Encoding.UTF8.GetBytes("qualquer coisa\nlinha 2"));

        var act = () => IbptTabelaCsvImporter.Ler(conteudo);

        act.Should().Throw<IbptIntegrationException>()
            .WithMessage("*TabelaIBPTax*");
    }

    // ── UF: só existe no nome do arquivo ─────────────────────────────────────

    [Theory]
    [InlineData("TabelaIBPTaxSP26.1.L.csv", "SP")]
    [InlineData("TabelaIBPTaxMG25.2.A.csv", "MG")]
    [InlineData("tabelaibptaxrj26.1.csv", "RJ")]
    [InlineData("tabela.csv", null)]
    [InlineData(null, null)]
    public void UfDoNomeDoArquivo_ExtraiOEstadoQuandoOPadraoEhSeguido(string? nome, string? esperado)
    {
        // A UF não está no CONTEÚDO do arquivo, só no nome. É a única defesa
        // contra uma loja de MG importar a tabela de SP e passar a emitir com
        // alíquota estadual errada sem nada denunciar.
        IbptTabelaCsvImporter.UfDoNomeDoArquivo(nome).Should().Be(esperado);
    }
}
