// =============================================================================
// CsvWriter.cs — Helper mínimo de geração de CSV pra exportação self-service
// de dados (ver ExportController). Ponto e vírgula como separador (não vírgula)
// porque valores monetários no sistema já são formatados como "1234,56"
// (vírgula decimal, padrão BR) — vírgula como separador de coluna quebraria
// a abertura no Excel em qualquer máquina configurada em pt-BR.
// =============================================================================

using System.Text;

namespace CardGameStore.Common;

public static class CsvWriter
{
    private const char Delimiter = ';';

    /// <summary>Monta um CSV completo (cabeçalho + linhas) como bytes UTF-8 com BOM —
    /// sem o BOM, o Excel abre acentuação (ç, ã, é...) corrompida.</summary>
    public static byte[] Build(string[] headers, IEnumerable<object?[]> rows)
    {
        var sb = new StringBuilder();
        WriteRow(sb, headers);
        foreach (var row in rows)
            WriteRow(sb, row);

        var bom = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[bom.Length + body.Length];
        bom.CopyTo(result, 0);
        body.CopyTo(result, bom.Length);
        return result;
    }

    private static void WriteRow(StringBuilder sb, IReadOnlyList<object?> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(Delimiter);
            sb.Append(Escape(values[i]));
        }
        sb.Append("\r\n");
    }

    private static string Escape(object? value)
    {
        var s = value switch
        {
            null                  => "",
            DateTime dt           => dt.ToString("yyyy-MM-dd HH:mm"),
            bool b                => b ? "Sim" : "Não",
            decimal d             => d.ToString("F2").Replace('.', ','),
            _                     => value.ToString() ?? "",
        };

        // Precisa de aspas se contém o delimitador, aspas, ou quebra de linha —
        // e aspas internas dobram (padrão RFC 4180).
        if (s.IndexOfAny([Delimiter, '"', '\n', '\r']) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";

        return s;
    }
}
