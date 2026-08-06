// =============================================================================
// FiscalControllerErrosTests.cs — Como o controller fiscal traduz falha de
// configuração em resposta HTTP.
//
// FiscalNaoConfiguradoException herda de Exception, não de
// InvalidOperationException. Cancelamento e inutilização só capturavam a
// segunda, então loja mal configurada (CNPJ inválido, certificado ausente)
// recebia 500 "Erro interno. Tente novamente em instantes." em vez de saber o
// que corrigir — e o lojista não tem como adivinhar.
// =============================================================================

using Xunit;
using CardGameStore.Controllers;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CardGameStore.Tests.Controllers;

public class FiscalControllerErrosTests
{
    // Só o serviço de emissão participa destes casos: as demais dependências não
    // são tocadas antes da exceção subir.
    private static FiscalController CreateController(Mock<INfceEmissionService> emissao) =>
        new(null!, null!, emissao.Object, null!, null!, null!, null!, null!, null!, null!, null!);

    private const string MensagemDeConfiguracao =
        "O identificador fiscal do estabelecimento não é um CNPJ válido para a SEFAZ.";

    [Fact]
    public async Task CancelarNota_ComFiscalNaoConfigurado_Devolve400ComMotivo()
    {
        var emissao = new Mock<INfceEmissionService>();
        emissao.Setup(e => e.CancelarAsync(It.IsAny<Guid>(), It.IsAny<string>()))
               .ThrowsAsync(new FiscalNaoConfiguradoException(MensagemDeConfiguracao));

        var resultado = await CreateController(emissao)
            .CancelarNota(Guid.NewGuid(), new CancelarNotaRequest { Justificativa = new string('x', 20) });

        resultado.Should().BeOfType<BadRequestObjectResult>(
            "erro de configuração é do cliente, não falha do servidor");
        resultado.As<BadRequestObjectResult>().Value!.ToString()
                 .Should().Contain("CNPJ", "a resposta precisa dizer o que corrigir");
    }

    [Fact]
    public async Task InutilizarFaixa_ComFiscalNaoConfigurado_Devolve400ComMotivo()
    {
        var emissao = new Mock<INfceEmissionService>();
        emissao.Setup(e => e.InutilizarFaixaAsync(
                   It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
               .ThrowsAsync(new FiscalNaoConfiguradoException(MensagemDeConfiguracao));

        var resultado = await CreateController(emissao).InutilizarFaixa(new InutilizarFaixaRequest
        {
            Ano = 2026, Serie = 1, NumeroInicial = 1, NumeroFinal = 5,
            Justificativa = new string('x', 20),
        });

        resultado.Should().BeOfType<BadRequestObjectResult>();
        resultado.As<BadRequestObjectResult>().Value!.ToString().Should().Contain("CNPJ");
    }

    [Fact]
    public async Task CancelarNota_ComInvalidOperation_ContinuaDevolvendo400()
    {
        // O catch novo entra antes do que já existia; o comportamento anterior
        // não pode ter sido engolido.
        var emissao = new Mock<INfceEmissionService>();
        emissao.Setup(e => e.CancelarAsync(It.IsAny<Guid>(), It.IsAny<string>()))
               .ThrowsAsync(new InvalidOperationException("Nota já cancelada."));

        var resultado = await CreateController(emissao)
            .CancelarNota(Guid.NewGuid(), new CancelarNotaRequest { Justificativa = new string('x', 20) });

        resultado.Should().BeOfType<BadRequestObjectResult>();
        resultado.As<BadRequestObjectResult>().Value!.ToString().Should().Contain("já cancelada");
    }
}
