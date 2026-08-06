using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IConciliacaoFiscalService
{
    /// <summary>
    /// Cruza todas as vendas tributáveis do período com os documentos fiscais
    /// existentes. Datas no calendário de Brasília, fim inclusive.
    /// </summary>
    Task<ConciliacaoFiscalDto> ConciliarAsync(DateTime inicioBr, DateTime fimBr);
}
