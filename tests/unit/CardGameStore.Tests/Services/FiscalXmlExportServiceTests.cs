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
            new NotaFiscalEmitida { Status = NotaFiscalStatus.Autorizada, EmitidoEm = dentroDoPeriodo, Serie = 1, Numero = 123, ChaveAcesso = "CHAVE-AUTORIZADA", XmlAutorizado = "<xml>autorizada</xml>" },
            new NotaFiscalEmitida { Status = NotaFiscalStatus.Cancelada,  EmitidoEm = dentroDoPeriodo, Serie = 1, Numero = 124, ChaveAcesso = "CHAVE-CANCELADA",  XmlAutorizado = "<xml>cancelada</xml>", XmlEventoCancelamento = "<procEventoNFe />" },
            new NotaFiscalEmitida { Status = NotaFiscalStatus.PendenteEmissao, EmitidoEm = dentroDoPeriodo, XmlAutorizado = null },
            new NotaFiscalEmitida { Status = NotaFiscalStatus.Autorizada, EmitidoEm = foraDoPeriodo, Serie = 1, Numero = 99, ChaveAcesso = "CHAVE-FORA", XmlAutorizado = "<xml>fora</xml>" }
        );
        await db.SaveChangesAsync();

        var service = new FiscalXmlExportService(db);
        var zipBytes = await service.GerarZipAsync(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        using var ms  = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var nomes = zip.Entries.Select(e => e.Name).ToList();

        zip.Entries.Should().HaveCount(3);
        nomes.Should().Contain(n => n.Contains("CHAVE-AUTORIZADA"));
        nomes.Should().Contain(n => n.Contains("CHAVE-CANCELADA") && n.Contains("procEventoCancelamento"));
        nomes.Should().NotContain(n => n.Contains("CHAVE-FORA"));
    }

    [Fact]
    public async Task GerarZipAsync_NomeDoArquivo_TrazDataSerieNumeroEStatus()
    {
        // O contador acha a nota pelo NOME do arquivo, sem abrir o XML — a chave
        // de 44 dígitos sozinha (nome antigo) não permitia isso. A data usada é a
        // de Brasília: 12:00 UTC do dia 15 é 09:00 do dia 15 aqui, e uma emissão
        // de madrugada não pode aparecer no arquivo com a data do dia seguinte.
        using var db = CreateDb();

        db.NotasFiscaisEmitidas.AddRange(
            new NotaFiscalEmitida
            {
                Status = NotaFiscalStatus.Autorizada,
                EmitidoEm = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc),
                Serie = 1, Numero = 123, ChaveAcesso = "CHAVE-COM-NUMERO",
                XmlAutorizado = "<xml/>",
            },
            // 01:30 UTC do dia 16 ainda é 22:30 do dia 15 em Brasília.
            new NotaFiscalEmitida
            {
                Status = NotaFiscalStatus.Autorizada,
                EmitidoEm = new DateTime(2026, 6, 16, 1, 30, 0, DateTimeKind.Utc),
                Serie = 2, Numero = 7, ChaveAcesso = "CHAVE-VIRADA",
                XmlAutorizado = "<xml/>",
            });
        await db.SaveChangesAsync();

        var service = new FiscalXmlExportService(db);
        var zipBytes = await service.GerarZipAsync(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        using var ms  = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var nomes = zip.Entries.Select(e => e.FullName).ToList();

        nomes.Should().Contain("saidas/2026-06-15_NFCe-001-000123_Autorizada_CHAVE-COM-NUMERO.xml");
        nomes.Should().Contain("saidas/2026-06-15_NFCe-002-000007_Autorizada_CHAVE-VIRADA.xml");
    }

    [Fact]
    public async Task GerarPacoteMensalAsync_JuntaXmlsERelatorios()
    {
        using var db = CreateDb();
        db.NotasFiscaisEmitidas.Add(new NotaFiscalEmitida
        {
            Status = NotaFiscalStatus.Autorizada,
            EmitidoEm = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            Serie = 1, Numero = 5, ChaveAcesso = "CHAVE-PACOTE", XmlAutorizado = "<xml/>",
        });
        await db.SaveChangesAsync();

        var service = new FiscalXmlExportService(db);
        var zipBytes = await service.GerarPacoteMensalAsync(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new[] { ("dre.csv", "Linha;Valor\r\nReceita bruta;100,00") });

        using var ms  = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var nomes = zip.Entries.Select(e => e.FullName).ToList();

        nomes.Should().Contain(n => n.StartsWith("saidas/") && n.Contains("CHAVE-PACOTE"));
        nomes.Should().Contain("relatorios/dre.csv");

        // O CSV precisa sair com BOM: sem ele o Excel em pt-BR come os acentos,
        // e é nele que o contador vai abrir o relatório.
        using var relatorio = zip.GetEntry("relatorios/dre.csv")!.Open();
        using var buffer = new MemoryStream();
        await relatorio.CopyToAsync(buffer);
        buffer.ToArray().Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
    }
}
