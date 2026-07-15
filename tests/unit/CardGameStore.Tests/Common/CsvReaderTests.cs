// =============================================================================
// CsvReaderTests.cs — Round-trip com o CsvWriter (o que exportamos tem que
// voltar a entrar sem perder/corromper dado) + casos isolados de parsing.
// =============================================================================

using System.Text;
using Xunit;
using CardGameStore.Common;
using FluentAssertions;

namespace CardGameStore.Tests.Common;

public class CsvReaderTests
{
    private static string BuildAsString(string[] headers, IEnumerable<object?[]> rows)
    {
        var bytes = CsvWriter.Build(headers, rows);
        return Encoding.UTF8.GetString(bytes); // mantém o BOM — CsvReader precisa tolerá-lo
    }

    [Fact]
    public void RoundTrip_ComAspasEPontoEVirgula_VoltaIntacto()
    {
        var csv = BuildAsString(["Nome", "Obs"], [
            ["Loja \"Top\"", "Rua A; Bairro B"],
            ["Ana", "linha1\nlinha2"],
        ]);

        var rows = CsvReader.ParseRows(csv);

        rows.Should().HaveCount(3); // cabeçalho + 2 linhas
        rows[0].Should().Equal("Nome", "Obs");
        rows[1].Should().Equal("Loja \"Top\"", "Rua A; Bairro B");
        rows[2].Should().Equal("Ana", "linha1\nlinha2");
    }

    [Fact]
    public void RoundTrip_ValorNulo_VoltaComoStringVazia()
    {
        var csv = BuildAsString(["A", "B"], [[null, "x"]]);
        var rows = CsvReader.ParseRows(csv);
        rows[1].Should().Equal("", "x");
    }

    [Fact]
    public void ParseRows_IgnoraLinhasVaziasNoFim()
    {
        var csv = "Nome;Preco\r\nProduto A;10,00\r\n\r\n";
        var rows = CsvReader.ParseRows(csv);
        rows.Should().HaveCount(2);
    }

    [Fact]
    public void HeaderIndex_EhCaseInsensitiveEToleraOrdemDeColunas()
    {
        var header = new[] { "preco", "NOME", "Estoque" };
        var index = CsvReader.HeaderIndex(header);

        index["Nome"].Should().Be(1);
        index["PRECO"].Should().Be(0);
        index["estoque"].Should().Be(2);
    }

    [Fact]
    public void Cell_ColunaAusente_RetornaNull()
    {
        var index = CsvReader.HeaderIndex(["Nome"]);
        var valor = CsvReader.Cell(["Ana"], index, "CPF");
        valor.Should().BeNull();
    }

    [Fact]
    public void Cell_ColunaPresenteMasVazia_RetornaStringVazia()
    {
        var index = CsvReader.HeaderIndex(["Nome", "Email"]);
        var valor = CsvReader.Cell(["Ana", ""], index, "Email");
        valor.Should().Be("");
    }

    [Fact]
    public void ParseRows_SemAspas_SeparaPorPontoEVirgula()
    {
        var csv = "Nome;Preco;Estoque\nProduto A;10,50;5\nProduto B;20,00;3";
        var rows = CsvReader.ParseRows(csv);

        rows.Should().HaveCount(3);
        rows[1].Should().Equal("Produto A", "10,50", "5");
        rows[2].Should().Equal("Produto B", "20,00", "3");
    }
}
