using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IVendaAvulsaService
{
    /// <summary>
    /// Registra uma venda avulsa: valida estoque, decrementa no PostgreSQL e
    /// persiste o evento de caixa no MongoDB. Operação atômica no lado PG.
    /// </summary>
    Task<VendaAvulsaDto> RegisterAsync(VendaAvulsaRequest request, Guid adminId, string adminName);

    /// <summary>Retorna as vendas avulsas mais recentes (padrão: últimas 50). Se <paramref name="desde"/> for informado, filtra direto no MongoDB.</summary>
    Task<IEnumerable<VendaAvulsaDto>> GetRecentAsync(int limit = 50, DateTime? desde = null);

    /// <summary>Retorna todas as vendas avulsas de um dia específico (fuso de Brasília). Padrão: hoje BR.</summary>
    Task<IEnumerable<VendaAvulsaDto>> GetByDateAsync(DateTime? date = null);

    /// <summary>Retorna todas as vendas avulsas vinculadas a um cliente específico.</summary>
    Task<IEnumerable<VendaAvulsaDto>> GetByUserAsync(Guid userId);

    /// <summary>
    /// Preenche UnitCostInCents=0 em itens de vendas avulsas usando o custo atual do produto no PostgreSQL.
    /// Retorna quantos itens foram atualizados.
    /// </summary>
    Task<int> BackfillCostsAsync();
}
