// =============================================================================
// FiscalConfigServiceTests.cs — a configuração fiscal não pode responder "erro
// interno" para dado que o usuário digitou.
//
// Motivador real: salvar o CSC devolvia "Erro interno. Tente novamente em
// instantes" com um trace id. A mensagem é falsa em dois sentidos — não é erro
// interno (é o campo grande demais para a coluna) e tentar de novo dá
// exatamente o mesmo resultado, porque é determinístico.
//
// O admin não tem como adivinhar qual campo está errado a partir disso.
// =============================================================================

using System.Runtime.CompilerServices;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGameStore.Tests.Services;

public class FiscalConfigServiceTests
{
    private static AppDbContext CreateDb([CallerMemberName] string testName = "") =>
        TestDbFactory.Create($"{nameof(FiscalConfigServiceTests)}_{testName}");

    private static FiscalConfigService CreateService(AppDbContext db)
    {
        var config = new ConfigurationBuilder().Build();
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        var enc = new EncryptionService(config, env.Object);

        return new FiscalConfigService(
            db, enc, new FiscalCertificadoService(), NullLogger<FiscalConfigService>.Instance);
    }

    [Fact]
    public async Task Salvar_CscIdMaiorQueAColuna_ExplicaOCampoEOLimite()
    {
        // `csc_id` é varchar(10). Antes disto, 11 caracteres chegavam intactos ao
        // PostgreSQL, que recusava com 22001 dentro do SaveChanges — e o admin
        // via "Erro interno" sem saber que o problema era o CSC.
        using var db = CreateDb();

        var resultado = await CreateService(db).SalvarAsync(new SaveFiscalConfigRequest
        {
            CscId = new string('9', 11),
        });

        resultado.Ok.Should().BeFalse();
        resultado.Erro.Should().Contain("ID do CSC").And.Contain("10",
            "a mensagem precisa nomear o campo e o limite — é o que permite corrigir");
    }

    [Fact]
    public async Task Salvar_CscIdNoLimite_EhAceito()
    {
        using var db = CreateDb();

        var resultado = await CreateService(db).SalvarAsync(new SaveFiscalConfigRequest
        {
            CscId = new string('9', 10),
        });

        resultado.Ok.Should().BeTrue("10 caracteres cabem na coluna");
    }

    [Theory]
    [InlineData("Uf", 3, "UF")]
    [InlineData("CodigoMunicipioIbge", 8, "IBGE")]
    [InlineData("Cep", 10, "CEP")]
    public async Task Salvar_CamposCurtos_ExplicamOProblema(string campo, int tamanho, string trecho)
    {
        // Os demais campos de coluna curta caem na mesma armadilha; a checagem é
        // por tabela, não caso a caso, então basta um exemplo de cada faixa.
        using var db = CreateDb();
        var valor = new string('1', tamanho);
        var req = campo switch
        {
            "Uf"                  => new SaveFiscalConfigRequest { Uf = valor },
            "CodigoMunicipioIbge" => new SaveFiscalConfigRequest { CodigoMunicipioIbge = valor },
            _                     => new SaveFiscalConfigRequest { Cep = valor },
        };

        var resultado = await CreateService(db).SalvarAsync(req);

        resultado.Ok.Should().BeFalse();
        resultado.Erro.Should().Contain(trecho);
    }

    [Fact]
    public async Task Salvar_CscIdComEspacos_NaoContaEspacoContraOLimite()
    {
        // Copiar e colar do portal da SEFAZ traz espaço em volta com frequência.
        // Recusar por causa disso seria recusar um valor que é válido.
        using var db = CreateDb();

        var resultado = await CreateService(db).SalvarAsync(new SaveFiscalConfigRequest
        {
            CscId = "  000001  ",
        });

        resultado.Ok.Should().BeTrue();
        var salvo = await db.FiscalConfigs.FindAsync(FiscalConfig.SingletonId);
        salvo!.CscId.Should().Be("000001", "o valor guardado é o útil, sem o espaço colado junto");
    }
}
