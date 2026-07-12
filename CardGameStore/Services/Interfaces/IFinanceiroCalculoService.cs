using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IFinanceiroCalculoService
{
    /// <summary>
    /// Calcula receita, custo, margem e os breakdowns financeiros (dia a dia,
    /// top produtos, formas de pagamento, crediário) de Comanda+VendaAvulsa
    /// mescladas, pro período [iniUtc, endUtc).
    /// </summary>
    /// <param name="iniUtc">Início do período em UTC (inclusive).</param>
    /// <param name="endUtc">Fim do período em UTC (exclusive).</param>
    /// <param name="dataBrIni">Primeiro dia do período no calendário de Brasília (usado no breakdown dia a dia).</param>
    /// <param name="dataBrFim">Último dia do período no calendário de Brasília (inclusive).</param>
    /// <param name="filterPaymentMethod">Filtra só transações com essa forma de pagamento, se informado.</param>
    Task<FinanceiroDto> CalcularAsync(
        DateTime iniUtc, DateTime endUtc,
        DateTime dataBrIni, DateTime dataBrFim,
        string? filterPaymentMethod = null);
}
