// =============================================================================
// ICreditarioService.cs — Interface do serviço de Crediário
// =============================================================================

using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface ICreditarioService
{
    /// <summary>
    /// Cria um novo crediário quando o Admin fecha uma comanda com pagamento em crediário.
    /// Valida se o cliente já tem um crediário aberto.
    /// </summary>
    Task<CrediariosDto> CreateAsync(Guid comandaId, Guid userId, int valorEmCentavos, Guid adminId);

    /// <summary>
    /// Retorna TODOS os crediários (abertos e pagos).
    /// </summary>
    Task<List<CrediariosDto>> GetAllAsync();

    /// <summary>
    /// Retorna todos os crediários de um usuário (abertos e pagos).
    /// </summary>
    Task<List<CrediariosDto>> GetByUserAsync(Guid userId);

    /// <summary>
    /// Retorna todos os crediários abertos (não pagos) e vencidos.
    /// Útil para dashboard do admin.
    /// </summary>
    Task<List<CrediariosDto>> GetAbertoAsync();

    /// <summary>
    /// Retorna todos os crediários vencidos (abertos e além da data de vencimento).
    /// </summary>
    Task<List<CrediariosDto>> GetVencidosAsync();

    /// <summary>
    /// Marca um crediário como pago.
    /// Usa o token do admin para rastrear quem pagou.
    /// </summary>
    Task<CrediariosDto> MarkAsPaidAsync(Guid creditarioId, Guid adminId, string? observacao = null);

    /// <summary>
    /// Retorna um crediário específico por ID.
    /// </summary>
    Task<CrediariosDto?> GetByIdAsync(Guid creditarioId);

    /// <summary>
    /// Verifica se um usuário tem um crediário aberto (bloqueia nova comanda).
    /// </summary>
    Task<bool> HasOpenAsync(Guid userId);

    /// <summary>
    /// Retorna o crediário aberto de um usuário, ou null se não houver.
    /// </summary>
    Task<CrediariosDto?> GetOpenAsync(Guid userId);

    /// <summary>
    /// Calcula o total devido por um usuário (todos os crediários abertos).
    /// </summary>
    Task<decimal> GetTotalDevidoAsync(Guid userId);
}
