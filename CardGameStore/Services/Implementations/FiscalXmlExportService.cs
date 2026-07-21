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
