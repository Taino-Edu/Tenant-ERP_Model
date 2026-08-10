// =============================================================================
// FiscalXmlExportService.cs — Gera o ZIP de XMLs (autorizados + cancelados)
// de um período, para exportação manual ou envio automático ao contador.
// =============================================================================

using System.IO.Compression;
using CardGameStore.Common;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class FiscalXmlExportService
{
    private readonly AppDbContext _db;

    public FiscalXmlExportService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Converte datas informadas pela UI como intervalo inclusivo de dias em
    /// Brasília para o intervalo UTC semiaberto usado pela consulta.</summary>
    internal static (DateTime InicioUtc, DateTime FimExclusivoUtc) NormalizarPeriodoInclusivo(
        DateTime inicio, DateTime fim)
    {
        if (fim.Date < inicio.Date)
            throw new ArgumentException("O período final não pode ser anterior ao inicial.", nameof(fim));

        return (
            BrazilTime.DateToUtcStart(inicio),
            BrazilTime.DateToUtcStart(fim.Date.AddDays(1)));
    }

    /// <summary>
    /// Remove do texto o que não pode virar nome de arquivo dentro do ZIP e o
    /// encurta — nome de fornecedor entra no nome do XML de entrada.
    /// </summary>
    private static string Sanitizar(string? texto, int maxLength = 40)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "sem-identificacao";

        var limpo = new string(texto.Trim()
            .Select(c => char.IsLetterOrDigit(c) ? c : (c is ' ' or '-' or '_' ? '-' : '\0'))
            .Where(c => c != '\0')
            .ToArray());

        while (limpo.Contains("--")) limpo = limpo.Replace("--", "-");
        limpo = limpo.Trim('-');

        if (limpo.Length > maxLength) limpo = limpo[..maxLength].Trim('-');
        return limpo.Length == 0 ? "sem-identificacao" : limpo;
    }

    /// <summary>
    /// Nome identificável de um XML de saída: data, série/número, status e chave.
    /// O contador acha a nota pelo número no nome do arquivo, sem abrir o XML —
    /// era o que a chave de 44 dígitos sozinha não permitia.
    /// Ex.: "2026-08-04_NFCe-001-000123_Autorizada_35260812345678000190...xml"
    /// </summary>
    private static string NomeArquivoSaida(NotaFiscalEmitida nota, string sufixo = "")
    {
        var dataBr = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(nota.EmitidoEm ?? nota.CreatedAt, DateTimeKind.Utc), BrazilTime.Zone);

        // Numero só existe depois de autorizada; nota sem número (rejeitada,
        // contingência não transmitida) ainda precisa de nome único no ZIP.
        var identificacao = nota.Numero is > 0
            ? $"NFCe-{nota.Serie ?? 0:000}-{nota.Numero!.Value:000000}"
            : $"NFCe-sem-numero-{nota.Id.ToString("N")[..8]}";

        var chave = string.IsNullOrWhiteSpace(nota.ChaveAcesso) ? nota.Id.ToString("N") : nota.ChaveAcesso;

        return $"{dataBr:yyyy-MM-dd}_{identificacao}_{nota.Status}{sufixo}_{chave}.xml";
    }

    /// <summary>
    /// Nome identificável de um XML de entrada: data, fornecedor e chave.
    /// Ex.: "2026-08-03_ATACADO-CENTRAL-LTDA_35260899...xml"
    /// </summary>
    private static string NomeArquivoEntrada(NotaDestinada nota)
    {
        var dataBr = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(nota.DataEmissao ?? nota.CreatedAt, DateTimeKind.Utc), BrazilTime.Zone);

        return $"{dataBr:yyyy-MM-dd}_{Sanitizar(nota.EmitenteNome)}_{nota.ChaveAcesso}.xml";
    }

    /// <summary>
    /// Pacote de fechamento: os mesmos XMLs do período mais os relatórios já
    /// montados pelo chamador (DRE, notas, apuração), num único download —
    /// é o que o contador leva pra escrituração sem precisar juntar arquivos.
    /// </summary>
    /// <param name="inicio">Início do período em UTC (inclusive).</param>
    /// <param name="fimExclusivo">Fim do período em UTC (exclusive).</param>
    /// <param name="relatorios">Pares (nome do arquivo, conteúdo em texto) gravados em "relatorios/" dentro do ZIP.</param>
    public async Task<byte[]> GerarPacoteMensalAsync(
        DateTime inicio, DateTime fimExclusivo, IEnumerable<(string Nome, string Conteudo)> relatorios)
    {
        var zipXmls = await GerarZipAsync(inicio, fimExclusivo);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Reabre o ZIP de XMLs e copia as entradas, em vez de refazer as
            // consultas — o conteúdo e a nomenclatura ficam idênticos ao que o
            // botão "Exportar XMLs" entrega.
            using (var origemMs = new MemoryStream(zipXmls))
            using (var origem = new ZipArchive(origemMs, ZipArchiveMode.Read))
            {
                foreach (var entrada in origem.Entries)
                {
                    var destino = zip.CreateEntry(entrada.FullName, CompressionLevel.Optimal);
                    await using var origemStream  = entrada.Open();
                    await using var destinoStream = destino.Open();
                    await origemStream.CopyToAsync(destinoStream);
                }
            }

            foreach (var (nome, conteudo) in relatorios)
            {
                var entry = zip.CreateEntry($"relatorios/{nome}", CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                // BOM UTF-8: sem ele o Excel abre CSV com acento quebrado, e é
                // nele que o contador vai abrir.
                await using var writer = new StreamWriter(entryStream, new System.Text.UTF8Encoding(true));
                await writer.WriteAsync(conteudo);
            }
        }

        return ms.ToArray();
    }

    /// <summary>Gera um .zip com saídas (NFC-e/eventos) e entradas (NF-e de fornecedores) do período.</summary>
    public async Task<byte[]> GerarZipAsync(DateTime inicio, DateTime fimExclusivo)
    {
        var notas = await _db.NotasFiscaisEmitidas
            .Where(n => (n.Status == NotaFiscalStatus.Autorizada || n.Status == NotaFiscalStatus.Cancelada)
                     && n.EmitidoEm != null
                     && n.EmitidoEm >= inicio && n.EmitidoEm < fimExclusivo
                     && n.XmlAutorizado != null)
            .OrderBy(n => n.EmitidoEm)
            .ToListAsync();
        var entradas = await _db.NotasDestinadas.AsNoTracking()
            .Where(n => n.XmlProc != null &&
                        (n.DataEmissao ?? n.CreatedAt) >= inicio &&
                        (n.DataEmissao ?? n.CreatedAt) < fimExclusivo)
            .OrderBy(n => n.DataEmissao ?? n.CreatedAt)
            .ToListAsync();

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var nota in notas)
            {
                var entry = zip.CreateEntry($"saidas/{NomeArquivoSaida(nota)}", CompressionLevel.Optimal);

                await using (var entryStream = entry.Open())
                await using (var writer = new StreamWriter(entryStream))
                    await writer.WriteAsync(nota.XmlAutorizado);

                if (nota.Status == NotaFiscalStatus.Cancelada && !string.IsNullOrWhiteSpace(nota.XmlEventoCancelamento))
                {
                    var eventoEntry = zip.CreateEntry(
                        $"saidas/{NomeArquivoSaida(nota, "-procEventoCancelamento")}", CompressionLevel.Optimal);
                    await using (var eventoStream = eventoEntry.Open())
                    await using (var eventoWriter = new StreamWriter(eventoStream))
                        await eventoWriter.WriteAsync(nota.XmlEventoCancelamento);
                }
            }

            foreach (var entrada in entradas)
            {
                var entry = zip.CreateEntry(
                    $"entradas/{NomeArquivoEntrada(entrada)}",
                    CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream);
                await writer.WriteAsync(entrada.XmlProc);
            }
        }

        return ms.ToArray();
    }
}
