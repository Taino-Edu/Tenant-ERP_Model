// =============================================================================
// NfceContingenciaCupomTests.cs — Fonte do DANFE por estado da nota (RES-002).
//
// Uma nota emitida em contingência offline é um documento fiscal legítimo antes
// de a SEFAZ voltar: o consumidor já levou a via. O XML assinado offline é
// persistido (XmlContingencia) justamente para que reiniciar a aplicação ou
// perder cache não altere o que foi entregue. Estes testes fixam de qual XML o
// DANFE sai em cada estado, e que nota sem documento nenhum não vira DANFE.
// =============================================================================

using System.Runtime.CompilerServices;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CardGameStore.Tests.Services;

public class NfceContingenciaCupomTests
{
    private static AppDbContext CreateDb([CallerMemberName] string testName = "") =>
        TestDbFactory.Create($"{nameof(NfceContingenciaCupomTests)}_{testName}");

    private static NfceEmissionService CreateService(AppDbContext db)
    {
        var config = new ConfigurationBuilder().Build();
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        var enc = new EncryptionService(config, env.Object);
        return new NfceEmissionService(db, enc, NullLogger<NfceEmissionService>.Instance);
    }

    private static string Fixture(string nome) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Nfce", nome));

    private static async Task<Guid> SeedNotaAsync(
        AppDbContext db, NotaFiscalStatus status, string? xmlAutorizado, string? xmlContingencia)
    {
        var nota = new NotaFiscalEmitida
        {
            Id                = Guid.NewGuid(),
            Origem            = NotaFiscalOrigem.Comanda,
            ComandaId         = Guid.NewGuid(),
            Status            = status,
            XmlAutorizado     = xmlAutorizado,
            XmlContingencia   = xmlContingencia,
            ValorTotalEmCentavos = 4000,
        };
        db.NotasFiscaisEmitidas.Add(nota);
        await db.SaveChangesAsync();
        return nota.Id;
    }

    [Fact]
    public async Task EmContingencia_SemXmlAutorizado_GeraDanfeDoXmlOffline()
    {
        using var db = CreateDb();
        var id = await SeedNotaAsync(db, NotaFiscalStatus.AutorizadaContingencia,
            xmlAutorizado: null, xmlContingencia: Fixture("nfce-contingencia.xml"));

        var danfe = await CreateService(db).ObterCupomAsync(id);

        danfe.Should().NotBeNull("a via de contingência é um DANFE legítimo, ainda sem protocolo");
        danfe!.Situacao.Should().Be(DanfeSituacao.ContingenciaSemProtocolo);
        danfe.EmContingencia.Should().BeTrue();
        danfe.Protocolo.Should().BeNull();
    }

    [Fact]
    public async Task Autorizada_PreferindoNfeProcAoXmlDeContingencia()
    {
        // Depois que a SEFAZ autoriza, a fonte é o nfeProc — mesmo que um resíduo
        // de XML de contingência sobrevivesse, o autorizado tem precedência.
        using var db = CreateDb();
        var id = await SeedNotaAsync(db, NotaFiscalStatus.Autorizada,
            xmlAutorizado: Fixture("nfce-normal-autorizada.xml"),
            xmlContingencia: Fixture("nfce-contingencia.xml"));

        var danfe = await CreateService(db).ObterCupomAsync(id);

        danfe!.Situacao.Should().Be(DanfeSituacao.Autorizada);
        danfe.Protocolo!.Numero.Should().Be("999260000009075407");
        danfe.Numero.Should().Be(16, "veio do nfeProc autorizado, não do XML de contingência");
    }

    [Fact]
    public async Task SemNenhumXml_NaoGeraDanfe()
    {
        // Pendente ou rejeitada: não há documento fiscal para representar.
        using var db = CreateDb();
        var id = await SeedNotaAsync(db, NotaFiscalStatus.PendenteEmissao,
            xmlAutorizado: null, xmlContingencia: null);

        (await CreateService(db).ObterCupomAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Cancelada_MarcaSituacaoIndependenteDoXml()
    {
        // O cancelamento é evento posterior, fora do XML de autorização.
        using var db = CreateDb();
        var id = await SeedNotaAsync(db, NotaFiscalStatus.Cancelada,
            xmlAutorizado: Fixture("nfce-normal-autorizada.xml"), xmlContingencia: null);

        var danfe = await CreateService(db).ObterCupomAsync(id);

        danfe!.Situacao.Should().Be(DanfeSituacao.Cancelada);
    }
}
