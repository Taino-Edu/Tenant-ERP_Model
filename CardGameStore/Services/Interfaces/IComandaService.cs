// =============================================================================
// IComandaService.cs — Interface do serviço de Comandas
// =============================================================================

using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

/// <summary>Contrato para todas as operações de negócio relacionadas a Comandas.</summary>
public interface IComandaService
{
    /// <summary>Cria e abre uma nova comanda para o usuário (chamado após login rápido).</summary>
    Task<ComandaDto> OpenComandaAsync(Guid userId, string? tableIdentifier = null);

    /// <summary>Retorna a comanda ativa (Aberta ou EmAndamento) de um usuário.</summary>
    Task<ComandaDto?> GetActiveComandaAsync(Guid userId);

    /// <summary>Retorna apenas o ID da comanda ativa (para o Hub SignalR).</summary>
    Task<Guid?> GetActiveComandaIdByUserAsync(Guid userId);

    /// <summary>Retorna uma comanda específica pelo ID (Admin).</summary>
    Task<ComandaDto?> GetByIdAsync(Guid comandaId);

    /// <summary>Adiciona um item à comanda do usuário e recalcula o total.</summary>
    Task<ComandaDto> AddItemAsync(Guid userId, AddItemToComandaRequest request);

    /// <summary>Admin adiciona item manualmente a uma comanda de cliente.</summary>
    Task<ComandaDto> AdminAddItemAsync(Guid comandaId, Guid adminId, AddItemToComandaRequest request);

    /// <summary>Remove um item de uma comanda (apenas Admin ou o próprio cliente).</summary>
    Task<ComandaDto> RemoveItemAsync(Guid comandaId, Guid itemId, Guid requestingUserId);

    /// <summary>
    /// Fecha a comanda (pagamento recebido).
    /// Se paymentMethod == "Crediario", cria um Crediario e envia email ao cliente.
    /// </summary>
    Task<ComandaDto> CloseComandaAsync(Guid comandaId, Guid adminId, string paymentMethod = "Dinheiro", string? observacao = null);

    /// <summary>Cancela a comanda sem cobrança.</summary>
    Task<ComandaDto> CancelComandaAsync(Guid comandaId, Guid adminId);

    /// <summary>Lista todas as comandas abertas/em andamento para o dashboard do Admin.</summary>
    Task<IEnumerable<ComandaDto>> GetActiveCommandasForDashboardAsync();

    /// <summary>Lista comandas fechadas e canceladas do dia especificado (padrão: hoje).</summary>
    Task<IEnumerable<ComandaDto>> GetTodayHistoryAsync(DateTime? data = null);

    /// <summary>Aplica pontos do cliente à comanda, abatendo do total a pagar.</summary>
    Task<ComandaDto> ApplyPointsAsync(Guid comandaId, Guid userId, int points);
}
