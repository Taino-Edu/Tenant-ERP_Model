using System.Text.RegularExpressions;

namespace CardGameStore.Services.Implementations;

public class OfxTransaction
{
    public string?  ExternalId  { get; set; }
    public string   TrnType     { get; set; } = "DEBIT"; // CREDIT | DEBIT
    public DateTime Date        { get; set; }
    public decimal  Amount      { get; set; }
    public string?  Description { get; set; }
    public string?  Memo        { get; set; }
}

public class OfxParserService
{
    public IReadOnlyList<OfxTransaction> Parse(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        var result  = new List<OfxTransaction>();
        var blocks  = Regex.Matches(content, @"<STMTTRN>(.*?)</STMTTRN>",
                          RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match m in blocks)
        {
            var block = m.Groups[1].Value;
            var tx = new OfxTransaction
            {
                TrnType     = Extract(block, "TRNTYPE") ?? "DEBIT",
                Description = Extract(block, "NAME"),
                Memo        = Extract(block, "MEMO"),
                ExternalId  = Extract(block, "FITID"),
            };

            var dateStr = Extract(block, "DTPOSTED");
            if (dateStr?.Length >= 8 &&
                DateTime.TryParseExact(dateStr[..8], "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
                tx.Date = dt;
            else
                tx.Date = DateTime.UtcNow;

            var amtStr = Extract(block, "TRNAMT");
            if (decimal.TryParse(amtStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var amt))
                tx.Amount = Math.Abs(amt);

            result.Add(tx);
        }

        return result;
    }

    private static string? Extract(string block, string tag)
    {
        var m = Regex.Match(block, $@"<{tag}>\s*([^<\r\n]+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }
}
