using System.Globalization;
using System.Text;
using CardGameStore.Services.Interfaces;

namespace CardGameStore.Services.Implementations;

/// <summary>Gera um PDF/A4 autocontido usando as fontes padrão do formato PDF.</summary>
public sealed class ReferralContractPdfService : IReferralContractPdfService
{
    private const double PageWidth = 595.28;
    private const double PageHeight = 841.89;
    private const double Margin = 48;
    private const double LineHeight = 14;

    public byte[] Generate(ReferralContractPdfData data)
    {
        var lines = BuildLines(data);
        var pages = Paginate(lines, 50);
        var objects = new List<byte[]> { Array.Empty<byte>(), Array.Empty<byte>(), Pdf("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"), Pdf("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>") };

        var pageObjectNumbers = new List<int>();
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var pageObject = objects.Count + 1;
            var contentObject = pageObject + 1;
            pageObjectNumbers.Add(pageObject);
            var content = BuildContent(pages[pageIndex], pageIndex + 1, pages.Count);
            var contentBytes = Pdf(content);
            objects.Add(Pdf($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth.ToString(CultureInfo.InvariantCulture)} {PageHeight.ToString(CultureInfo.InvariantCulture)}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObject} 0 R >>"));
            objects.Add(Concat(Pdf($"<< /Length {contentBytes.Length} >>\nstream\n"), contentBytes, Pdf("\nendstream")));
        }

        objects[0] = Pdf("<< /Type /Catalog /Pages 2 0 R >>");
        objects[1] = Pdf($"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(' ', pageObjectNumbers.Select(n => $"{n} 0 R"))}] >>");
        return Assemble(objects);
    }

    private static List<ContractLine> BuildLines(ReferralContractPdfData d)
    {
        var brt = new DateTimeOffset(DateTime.SpecifyKind(d.AcceptedAtUtc, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(-3));
        var lines = new List<ContractLine>
        {
            new("TERMO DE ADESÃO AO PROGRAMA DE PARCERIAS", true, 15),
            new("3ESYSTEN", true, 12),
            new("", false, 10),
            new($"Versão do regulamento: {d.ContractVersion}", false, 10),
            new($"Parceiro de indicação: {d.PartnerName}", false, 10),
            new($"{(d.PersonType == "PJ" ? "CNPJ" : "CPF")}: {d.Document}  |  E-mail confirmado: {d.Email}", false, 10),
            new($"Telefone: {d.Phone ?? "não informado"}  |  Categoria: {d.PartnerKind}", false, 10),
            new($"Condições: {d.SetupCommissionPercent:0.##}% sobre implantação aplicável e {d.MonthlyCommissionPercent:0.##}% sobre mensalidades pagas; disponibilidade após {d.PaymentGraceDays} dia(s).", false, 10),
            new("", false, 10),
            new("REGULAMENTO ACEITO", true, 12),
        };
        foreach (var paragraph in d.ContractText.Replace("\r", string.Empty).Split('\n'))
            lines.AddRange(Wrap(paragraph, 92, false, 10));

        lines.AddRange([
            new("", false, 10),
            new("EVIDÊNCIAS DO ACEITE ELETRÔNICO", true, 12),
            new($"Aceite confirmado por código de uso único enviado a {d.Email}.", false, 10),
            new($"Data e hora: {brt:dd/MM/yyyy HH:mm:ss} (UTC-03:00) / {d.AcceptedAtUtc:yyyy-MM-dd HH:mm:ss} UTC", false, 10),
            new($"Identificador da evidência: {d.EvidenceId}", false, 10),
            new($"SHA-256 da evidência: {d.EvidenceSha256}", false, 10),
            new($"IP anonimizado (SHA-256 com salt): {d.IpHash}", false, 9),
            new($"Navegador/dispositivo: {d.UserAgent}", false, 9),
            new("", false, 10),
            new("Este documento eletrônico preserva o texto integral aceito e as evidências de autoria e integridade armazenadas pela CONTRATANTE.", false, 10),
        ]);
        return lines.SelectMany(l => Wrap(l.Text, l.Bold ? 82 : 92, l.Bold, l.Size)).ToList();
    }

    private static List<ContractLine> Wrap(string text, int width, bool bold, int size)
    {
        if (string.IsNullOrWhiteSpace(text)) return [new("", bold, size)];
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<ContractLine>();
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length > 0 && current.Length + word.Length + 1 > width)
            {
                result.Add(new(current.ToString(), bold, size));
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > 0) result.Add(new(current.ToString(), bold, size));
        return result;
    }

    private static List<List<ContractLine>> Paginate(List<ContractLine> lines, int perPage) =>
        lines.Chunk(perPage).Select(chunk => chunk.ToList()).ToList();

    private static string BuildContent(List<ContractLine> lines, int page, int total)
    {
        var builder = new StringBuilder();
        var y = PageHeight - Margin;
        foreach (var line in lines)
        {
            builder.Append("BT /").Append(line.Bold ? "F2" : "F1").Append(' ').Append(line.Size)
                .Append(" Tf 1 0 0 1 ").Append(Margin.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(y.ToString(CultureInfo.InvariantCulture)).Append(" Tm (").Append(Escape(line.Text)).Append(") Tj ET\n");
            y -= LineHeight;
        }
        builder.Append("BT /F1 8 Tf 1 0 0 1 ").Append(Margin.ToString(CultureInfo.InvariantCulture)).Append(" 24 Tm (Documento eletrônico 3ESYSTEN - página ")
            .Append(page).Append(" de ").Append(total).Append(") Tj ET");
        return builder.ToString();
    }

    private static byte[] Assemble(List<byte[]> objects)
    {
        using var output = new MemoryStream();
        Write(output, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            Write(output, $"{i + 1} 0 obj\n");
            output.Write(objects[i]);
            Write(output, "\nendobj\n");
        }
        var xref = output.Position;
        Write(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Write(output, $"{offset:0000000000} 00000 n \n");
        Write(output, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static string Escape(string value) => Sanitize(value).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    private static string Sanitize(string value) => value.Replace('–', '-').Replace('—', '-').Replace('“', '"').Replace('”', '"').Replace('’', '\'');
    private static byte[] Pdf(string value) => value.Select(c => (byte)(c <= 255 ? c : '?')).ToArray();
    private static void Write(Stream stream, string value) => stream.Write(Pdf(value));
    private static byte[] Concat(params byte[][] parts) { using var stream = new MemoryStream(); foreach (var part in parts) stream.Write(part); return stream.ToArray(); }
    private sealed record ContractLine(string Text, bool Bold, int Size);
}
