// =============================================================================
// CsvWriterTests.cs — Testa o escaping RFC 4180 do gerador de CSV usado pela
// exportação self-service (ExportController). Nomes/observações de clientes são
// texto livre — precisam sobreviver a ponto-e-vírgula, aspas e quebra de linha
// sem corromper o arquivo.
// =============================================================================

using System.Text;
using Xunit;
using CardGameStore.Common;
using FluentAssertions;

namespace CardGameStore.Tests.Common;

public class CsvWriterTests
{
    private static string BuildAsString(string[] headers, IEnumerable<object?[]> rows)
    {
        var bytes = CsvWriter.Build(headers, rows);
        // Remove o BOM UTF-8 (3 bytes) antes de comparar texto.
        return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
    }

    [Fact]
    public void Build_ComecaComBomUtf8()
    {
        var bytes = CsvWriter.Build(["A"], [["1"]]);
        bytes.Take(3).Should().BeEquivalentTo(Encoding.UTF8.GetPreamble());
    }

    [Fact]
    public void Build_ValorSimples_SemAspas()
    {
        var csv = BuildAsString(["Nome", "Idade"], [["Ana", 30]]);
        csv.Should().Be("Nome;Idade\r\nAna;30\r\n");
    }

    [Fact]
    public void Build_ValorComPontoEVirgula_EnvolveComAspas()
    {
        var csv = BuildAsString(["Observacao"], [["Rua A; Bairro B"]]);
        csv.Should().Be("Observacao\r\n\"Rua A; Bairro B\"\r\n");
    }

    [Fact]
    public void Build_ValorComAspas_DobraAsAspasInternas()
    {
        var csv = BuildAsString(["Nome"], [["Loja \"Top\""]]);
        csv.Should().Be("Nome\r\n\"Loja \"\"Top\"\"\"\r\n");
    }

    [Fact]
    public void Build_ValorComQuebraDeLinha_EnvolveComAspas()
    {
        var csv = BuildAsString(["Obs"], [["linha1\nlinha2"]]);
        csv.Should().Be("Obs\r\n\"linha1\nlinha2\"\r\n");
    }

    [Fact]
    public void Build_ValorNulo_ViraVazio()
    {
        var csv = BuildAsString(["A", "B"], [[null, "x"]]);
        csv.Should().Be("A;B\r\n;x\r\n");
    }

    [Fact]
    public void Build_Decimal_UsaVirgulaComoSeparadorDecimal()
    {
        var csv = BuildAsString(["Preco"], [[1234.5m]]);
        csv.Should().Be("Preco\r\n1234,50\r\n");
    }

    [Fact]
    public void Build_Bool_TraduzParaSimNao()
    {
        var csv = BuildAsString(["Ativo"], [[true], [false]]);
        csv.Should().Be("Ativo\r\nSim\r\nNão\r\n");
    }
}
