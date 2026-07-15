// =============================================================================
// CsvReader.cs — Parser mínimo de CSV pra importação self-service (ver
// ImportController). Espelha o formato do CsvWriter: separador ";", campos
// entre aspas quando contêm ";"/aspas/quebra de linha, aspas internas
// dobradas (RFC 4180) — o CSV que a gente mesmo exporta volta a entrar sem
// gambiarra de formato.
// =============================================================================

using System.Text;

namespace CardGameStore.Common;

public static class CsvReader
{
    private const char Delimiter = ';';

    /// <summary>Faz o parse do texto inteiro em linhas de células cruas (string[]).
    /// A primeira linha (cabeçalho) NÃO é tratada de forma especial aqui — quem
    /// chama decide como mapear colunas por nome via <see cref="HeaderIndex"/>.</summary>
    public static List<string[]> ParseRows(string csvText)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        // Remove BOM UTF-8 se presente (o próprio CsvWriter grava um).
        if (csvText.Length > 0 && csvText[0] == '﻿')
            csvText = csvText[1..];

        void EndField() { row.Add(field.ToString()); field.Clear(); }
        void EndRow() { EndField(); rows.Add(row.ToArray()); row = new List<string>(); }

        while (i < csvText.Length)
        {
            var c = csvText[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csvText.Length && csvText[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                    inQuotes = false; i++; continue;
                }
                field.Append(c); i++; continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true; i++; break;
                case var d when d == Delimiter:
                    EndField(); i++; break;
                case '\r':
                    // Tolera \r\n e \r solto — sempre consome o \n seguinte se existir.
                    if (i + 1 < csvText.Length && csvText[i + 1] == '\n') i++;
                    EndRow(); i++; break;
                case '\n':
                    EndRow(); i++; break;
                default:
                    field.Append(c); i++; break;
            }
        }

        // Última linha sem quebra final.
        if (field.Length > 0 || row.Count > 0) EndRow();

        // Ignora linhas totalmente vazias (comuns no fim do arquivo).
        return rows.Where(r => r.Length > 1 || !string.IsNullOrWhiteSpace(r.ElementAtOrDefault(0))).ToList();
    }

    /// <summary>Mapa nome-de-coluna (do cabeçalho) → índice, case-insensitive.
    /// Deixa o import tolerante a colunas reordenadas — só falha se uma coluna
    /// obrigatória não existir pelo nome esperado.</summary>
    public static Dictionary<string, int> HeaderIndex(string[] header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Length; i++)
            map[header[i].Trim()] = i;
        return map;
    }

    /// <summary>Lê uma célula pelo nome da coluna — null se a coluna não existe
    /// no cabeçalho, string vazia se a célula está em branco.</summary>
    public static string? Cell(string[] row, Dictionary<string, int> index, string column) =>
        index.TryGetValue(column, out var i) && i < row.Length ? row[i].Trim() : null;
}
