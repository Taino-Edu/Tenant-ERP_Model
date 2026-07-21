// =============================================================================
// FiscalXmlExportServiceTests.cs — Testes da geração do ZIP de XMLs
// autorizados/cancelados para exportação ao contador.
// =============================================================================

using System.IO.Compression;
using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Tests.Services;

public class FiscalXmlExportServiceTests
{
    private static AppDbContext CreateDb() => TestDbFactory.Create(nameof(FiscalXmlExportServiceTests));

    [Fact]
    public void NormalizarPeriodoInclusivo_MesmoDia_CobreODiaInteiroEmBrasilia()
    {
        var data = new DateTime(2026, 7, 21);

        var (inicioUtc, fimExclusivoUtc) = FiscalXmlExportService.NormalizarPeriodoInclusivo(data, data);

        inicioUtc.Should().Be(new DateTime(2026, 7, 21, 3, 0, 0, DateTimeKind.Utc));
        fimExclusivoUtc.Should().Be(new DateTime(2026, 7, 22, 3, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NormalizarPeriodoInclusivo_FimAnterior_LancaErroClaro()
    {
        var act = () => FiscalXmlExportService.NormalizarPeriodoInclusivo(
            new DateTime(2026, 7, 22), new DateTime(2026, 7, 21));

        act.Should().Throw<ArgumentException>().WithMessage("*anterior*");
    }

    [Fact]
    public async Task GerarZipAsync_IncluiApenasNotasAutorizadasECanceladasDoPeriodo()
    {
        using var db = CreateDb();
        var dentroDoPeriodo = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var foraDoPeriodo    = new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);

        db.NotasFiscaisEmitidas.AddRange(
            new NotaFiscalEmitida { Status = NotaFiscalStatus.Autorizada, EmitidoEm = dentroDoPeriodo, ChaveAcesso = "CHAVE-AUTORIZADA", XmlAutorizado = "<xml>autorizada</xml>" },
            new NotaFiscalEmitida { Status = NotaFiscalStatus.Cancelada,  EmitidoEm = dentroDoPeriodo, ChaveAcesso = "CHAVE-CANCELADA",  XmlAutorizado = "<xml>cancelada</xml>" },
            new NotaFiscalEmitida { Status = NotaFiscalStatus.PendenteEmissao, EmitidoEm = dentroDoPeriodo, XmlAutorizado = null },
            new NotaFiscalEmitida { Status = NotaFiscalStatus.Autorizada, EmitidoEm = foraDoPeriodo, ChaveAcesso = "CHAVE-FORA", XmlAutorizado = "<xml>fora</xml>" }
        );
        await db.SaveChangesAsync();

        var service = new FiscalXmlExportService(db);
        var zipBytes = await service.GerarZipAsync(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        using var ms  = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        zip.Entries.Should().HaveCount(2);
        zip.Entries.Select(e => e.Name).Should().Contain(n => n.StartsWith("CHAVE-AUTORIZADA"));
        zip.Entries.Select(e => e.Name).Should().Contain(n => n.StartsWith("CHAVE-CANCELADA"));
        zip.Entries.Select(e => e.Name).Should().NotContain(n => n.StartsWith("CHAVE-FORA"));
    }
}
