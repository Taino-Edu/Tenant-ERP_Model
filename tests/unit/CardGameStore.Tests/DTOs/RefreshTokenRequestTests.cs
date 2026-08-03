// =============================================================================
// RefreshTokenRequestTests.cs — Trava o contrato que mantém a renovação de sessão viva.
//
// O corpo de POST /api/auth/refresh é opcional de propósito: o token vai no
// cookie HttpOnly e o frontend manda {}. Quando o campo era obrigatório, a
// validação automática do [ApiController] respondia 400 ANTES da action, e a
// linha que lê o cookie nunca rodava — nenhuma sessão do sistema conseguia se
// renovar, e todo usuário caía fora quando o access token expirava.
//
// O detalhe que torna este teste necessário: não basta olhar se tem [Required].
// Com nullable habilitado, `string` NÃO-anulável já é tratado como obrigatório
// pelo ASP.NET sozinho, sem atributo nenhum. Por isso aqui se verifica as duas
// coisas — ausência do atributo E o tipo ser anulável.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CardGameStore.DTOs;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.DTOs;

public class RefreshTokenRequestTests
{
    private static ParameterInfo RefreshTokenParametro() =>
        typeof(RefreshTokenRequest)
            .GetConstructors().Single()
            .GetParameters().Single(p => p.Name == "RefreshToken");

    [Fact]
    public void RefreshToken_NaoPodeSerObrigatorio_PorAtributo()
    {
        var propriedade = typeof(RefreshTokenRequest).GetProperty(nameof(RefreshTokenRequest.RefreshToken))!;

        propriedade.GetCustomAttribute<RequiredAttribute>().Should().BeNull(
            "com [Required], o corpo vazio que o frontend envia vira 400 antes da action e " +
            "o cookie nunca é lido — a renovação de sessão para de funcionar pro sistema inteiro");
    }

    [Fact]
    public void RefreshToken_PrecisaSerAnulavel()
    {
        // `string` não-anulável é tratado como obrigatório pela validação do
        // ASP.NET mesmo sem [Required] — remover só o atributo não bastaria.
        var nulabilidade = new NullabilityInfoContext().Create(RefreshTokenParametro());

        nulabilidade.WriteState.Should().Be(NullabilityState.Nullable,
            "tipo não-anulável reintroduz a obrigatoriedade implícita e o 400 volta");
    }

    [Fact]
    public void RefreshToken_TemValorPadrao_ParaAceitarCorpoVazio()
    {
        RefreshTokenParametro().HasDefaultValue.Should().BeTrue(
            "o frontend chama o endpoint com {} — sem valor padrão o binder não consegue " +
            "materializar o record a partir de um corpo sem o campo");
    }

    [Fact]
    public void PodeSerConstruidoSemArgumento()
    {
        var acao = () => new RefreshTokenRequest();

        acao.Should().NotThrow();
        new RefreshTokenRequest().RefreshToken.Should().BeNull();
    }
}
