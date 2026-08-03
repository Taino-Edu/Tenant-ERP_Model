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
                var nomeBase = !string.IsNullOrWhiteSpace(nota.ChaveAcesso) ? nota.ChaveAcesso : nota.Id.ToString();
                var fileName = $"saidas/{nomeBase}-{nota.Status}.xml";
                var entry    = zip.CreateEntry(fileName, CompressionLevel.Optimal);

                await using (var entryStream = entry.Open())
                await using (var writer = new StreamWriter(entryStream))
                    await writer.WriteAsync(nota.XmlAutorizado);

                if (nota.Status == NotaFiscalStatus.Cancelada && !string.IsNullOrWhiteSpace(nota.XmlEventoCancelamento))
                {
                    var eventoEntry = zip.CreateEntry($"saidas/{nomeBase}-cancelamento-procEvento.xml", CompressionLevel.Optimal);
                    await using (var eventoStream = eventoEntry.Open())
                    await using (var eventoWriter = new StreamWriter(eventoStream))
                        await eventoWriter.WriteAsync(nota.XmlEventoCancelamento);
                }
            }

            foreach (var entrada in entradas)
            {
                var entry = zip.CreateEntry(
                    $"entradas/{entrada.ChaveAcesso}-entrada-{entrada.Status}.xml",
                    CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream);
                await writer.WriteAsync(entrada.XmlProc);
            }
        }

        return ms.ToArray();
    }
}
