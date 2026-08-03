// =============================================================================
// GeminiLimitsTests.cs — Piso do orçamento de saída dos assistentes de IA.
//
// Teste de valor, não de comportamento — não dá pra exercitar o Gemini na suíte.
// O que ele guarda é a lição: 320 tokens cortavam a resposta no meio da palavra
// na página de vendas, e 600 já tinham cortado no assistente do painel. Quem
// baixar este número de novo esbarra aqui antes de descobrir em produção.
// =============================================================================

using CardGameStore.Services.Implementations;
using FluentAssertions;
using Xunit;

namespace CardGameStore.Tests.Services;

public class GeminiLimitsTests
{
    [Fact]
    public void OrcamentoDeSaida_TemFolgaSuficiente()
    {
        // 600 foi medido cortando no assistente interno; 320, no público. O piso
        // fica acima dos dois com margem, porque parte do orçamento é consumida
        // pelo raciocínio interno do modelo antes de sair texto.
        GeminiLimits.MaxOutputTokens.Should().BeGreaterThanOrEqualTo(1000,
            "abaixo disso a resposta em PT-BR volta a ser cortada no meio da frase");
    }
}
