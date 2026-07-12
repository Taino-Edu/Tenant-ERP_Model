using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;

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

    /// <summary>
    /// Fecha (ou refecha, se já existir) uma janela de período — grava um
    /// snapshot congelado em FechamentoPeriodo. Upsert por (Tipo, DataInicio,
    /// DataFim): rodar de novo sobre uma janela já fechada recalcula e
    /// sobrescreve — é assim que "reabrir fechamento" funciona, não existe
    /// um endpoint de "unlock" separado.
    /// </summary>
    /// <param name="tipo">Granularidade da janela (Dia, Semana ou Mês).</param>
    /// <param name="dataInicioBr">Primeiro dia da janela, calendário de Brasília.</param>
    /// <param name="dataFimBrInclusive">Último dia da janela (inclusive), calendário de Brasília.</param>
    Task<FechamentoPeriodo> FecharJanelaAsync(TipoFechamento tipo, DateTime dataInicioBr, DateTime dataFimBrInclusive);
}
