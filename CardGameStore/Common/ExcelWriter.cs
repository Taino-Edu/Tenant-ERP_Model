// =============================================================================
// ExcelWriter.cs — Helper mínimo de geração de .xlsx pra exportação self-service
// de dados (ver ExportController). Ao contrário do CsvWriter (tudo formatado
// como texto), aqui número e data viram célula numérica/data de verdade —
// é o que torna o relatório "técnico": dá pra somar/filtrar/tabela-dinâmica
// direto no Excel, sem reconverter texto primeiro.
// =============================================================================

using ClosedXML.Excel;

namespace CardGameStore.Common;

public static class ExcelWriter
{
    /// <summary>Monta uma planilha .xlsx (cabeçalho em negrito + linhas) como bytes.</summary>
    public static byte[] Build(string[] headers, IEnumerable<object?[]> rows, string sheetName = "Dados")
    {
        using var workbook  = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeSheetName(sheetName));

        for (var col = 0; col < headers.Length; col++)
            worksheet.Cell(1, col + 1).Value = headers[col];
        worksheet.Row(1).Style.Font.Bold = true;

        var linha = 2;
        foreach (var row in rows)
        {
            for (var col = 0; col < row.Length; col++)
                SetCell(worksheet.Cell(linha, col + 1), row[col]);
            linha++;
        }

        worksheet.Columns().AdjustToContents(1, Math.Min(linha - 1, 5000));
        worksheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                break;
            case bool b:
                cell.Value = b ? "Sim" : "Não";
                break;
            case decimal d:
                cell.Value = d;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case int or long or double or float:
                cell.Value = Convert.ToDouble(value);
                break;
            default:
                cell.Value = value.ToString() ?? "";
                break;
        }
    }

    // Nome de planilha no Excel não aceita [ ] : * ? / \ e tem limite de 31 caracteres.
    private static string SanitizeSheetName(string name)
    {
        var limpo = new string(name.Where(c => !"[]:*?/\\".Contains(c)).ToArray());
        return limpo.Length > 31 ? limpo[..31] : (limpo.Length == 0 ? "Dados" : limpo);
    }
}
