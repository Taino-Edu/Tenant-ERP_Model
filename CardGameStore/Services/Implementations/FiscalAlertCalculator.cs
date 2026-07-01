// =============================================================================
// FiscalAlertCalculator.cs — Lógica pura de quando disparar o próximo alerta
// de vencimento do certificado (30/15/7/1 dias). Sem dependências de banco/DI
// para ser facilmente testável.
// =============================================================================

namespace CardGameStore.Services.Implementations;

public static class FiscalAlertCalculator
{
    public static readonly int[] Limiares = { 30, 15, 7, 1 };

    /// <summary>
    /// Retorna o próximo limiar (30/15/7/1) que deve ser alertado dado quantos dias faltam
    /// e qual foi o último limiar já alertado — ou null se nenhum alerta é devido agora.
    /// Garante que o mesmo limiar nunca seja alertado duas vezes.
    /// </summary>
    public static int? ProximoLimiarParaAlertar(int diasRestantes, int? ultimoAlertaLimiar) =>
        Limiares
            .Where(t => diasRestantes <= t && (ultimoAlertaLimiar is null || t < ultimoAlertaLimiar.Value))
            .OrderBy(t => t)
            .Cast<int?>()
            .FirstOrDefault();
}
