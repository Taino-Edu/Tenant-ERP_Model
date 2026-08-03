// =============================================================================
// GeminiLimits.cs — Limites compartilhados pelos assistentes de IA.
//
// Existe um assistente interno (GeminiChatService, painel do lojista) e um
// público (PublicSalesAssistantService, site institucional). Os dois falam com o
// mesmo modelo, e os dois já sofreram do MESMO defeito de resposta cortada — mas
// cada um tinha seu próprio número solto no código, então a correção de um não
// chegou no outro. O interno foi subido de 600 pra 1200; o público ficou em 320
// e passou meses cortando resposta na cara do visitante, que é justamente onde
// mais dói: a página de vendas.
//
// A constante mora aqui pra que a próxima vez que alguém precise ajustar,
// ajuste UMA vez.
// =============================================================================

namespace CardGameStore.Services.Implementations;

public static class GeminiLimits
{
    /// <summary>
    /// Teto de tokens de saída por resposta.
    ///
    /// Precisa de folga bem maior do que o tamanho do texto sugere, por dois
    /// motivos que se somam: PT-BR com acento gasta mais tokens por caractere
    /// que inglês, e o modelo da linha Flash consome parte deste MESMO orçamento
    /// com raciocínio interno antes de escrever. É por isso que o sintoma era
    /// contraintuitivo — quanto mais difícil a pergunta, MENOR a resposta, porque
    /// o orçamento acabava antes de sair texto.
    ///
    /// Medido em produção com 320: pergunta sobre planos devolvia 43 caracteres,
    /// cortada no meio da palavra ("três planos mens").
    /// </summary>
    public const int MaxOutputTokens = 1200;
}
