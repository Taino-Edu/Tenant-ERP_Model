// =============================================================================
// FiscalXmlExportService.cs — Gera o ZIP de XMLs (autorizados + cancelados)
// de um período, para exportação manual ou envio automático ao contador.
// =============================================================================

using System.IO.Compression;
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

    /// <summary>Gera um .zip em memória com os XMLs autorizados e cancelados emitidos no período [inicio, fimExclusivo).</summary>
    public async Task<byte[]> GerarZipAsync(DateTime inicio, DateTime fimExclusivo)
    {
        var notas = await _db.NotasFiscaisEmitidas
            .Where(n => (n.Status == NotaFiscalStatus.Autorizada || n.Status == NotaFiscalStatus.Cancelada)
                     && n.EmitidoEm != null
                     && n.EmitidoEm >= inicio && n.EmitidoEm < fimExclusivo
                     && n.XmlAutorizado != null)
            .OrderBy(n => n.EmitidoEm)
            .ToListAsync();

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var nota in notas)
            {
                var nomeBase = !string.IsNullOrWhiteSpace(nota.ChaveAcesso) ? nota.ChaveAcesso : nota.Id.ToString();
                var fileName = $"{nomeBase}-{nota.Status}.xml";
                var entry    = zip.CreateEntry(fileName, CompressionLevel.Optimal);

                await using var entryStream = entry.Open();
                await using var writer      = new StreamWriter(entryStream);
                await writer.WriteAsync(nota.XmlAutorizado);
            }
        }

        return ms.ToArray();
    }
}
