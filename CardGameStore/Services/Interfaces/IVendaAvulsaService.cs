using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IVendaAvulsaService
{
    /// <summary>
    /// Registra uma venda avulsa: valida estoque, decrementa no PostgreSQL e
    /// persiste o evento de caixa no MongoDB. Operação atômica no lado PG.
    /// </summary>
    Task<VendaAvulsaDto> RegisterAsync(VendaAvulsaRequest request, Guid adminId, string adminName);

    /// <summary>Retorna as vendas avulsas mais recentes (padrão: últimas 50).</summary>
    Task<IEnumerable<VendaAvulsaDto>> GetRecentAsync(int limit = 50);

    /// <summary>Retorna todas as vendas avulsas de um dia específico (fuso de Brasília). Padrão: hoje BR.</summary>
    Task<IEnumerable<VendaAvulsaDto>> GetByDateAsync(DateTime? date = null);
}
