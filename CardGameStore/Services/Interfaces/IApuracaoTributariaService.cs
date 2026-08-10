using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IApuracaoTributariaService
{
    /// <summary>
    /// Apura o período no Simples Nacional e no Lucro Presumido e devolve os dois
    /// lado a lado. Datas no calendário de Brasília, fim inclusive.
    /// </summary>
    Task<ApuracaoTributariaDto> ApurarAsync(DateTime inicioBr, DateTime fimBr);

    /// <summary>Receita bruta mês a mês (calendário de Brasília) num intervalo de competências.</summary>
    Task<List<ReceitaMensalDto>> ReceitaMensalAsync(DateTime primeiroMesBr, int meses);
}
