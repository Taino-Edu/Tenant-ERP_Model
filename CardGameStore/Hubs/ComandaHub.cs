// =============================================================================
// ComandaHub.cs — Hub SignalR para comunicação em tempo real de Comandas
//
// FLUXO PRINCIPAL:
//   1. Cliente escaneia QR Code → faz login → conecta ao Hub com JWT
//   2. Cliente adiciona item → chama AddItemToComanda → Hub notifica o Admin
//   3. Admin está no grupo "AdminDashboard" → recebe evento instantaneamente
//   4. Admin pode responder: fechar comanda, adicionar item manualmente, etc.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CardGameStore.Hubs;

/// <summary>
/// Hub central para gestão de comandas em tempo real.
/// URL de conexão: /hubs/comanda (configurado no Program.cs)
/// </summary>
[Authorize] // Exige JWT válido para qualquer conexão
public class ComandaHub : Hub
{
    private readonly IComandaService _comandaService;
    private readonly ILogger<ComandaHub> _logger;

    // Constante para o grupo do painel do Admin (Maikon)
    private const string AdminGroup = "AdminDashboard";

    public ComandaHub(IComandaService comandaService, ILogger<ComandaHub> logger)
    {
        _comandaService = comandaService;
        _logger         = logger;
    }

    // =========================================================================
    // CICLO DE VIDA DA CONEXÃO
    // =========================================================================

    /// <summary>
    /// Chamado automaticamente quando um cliente conecta.
    /// Admin entra no grupo exclusivo do dashboard.
    /// Clientes são adicionados a um grupo por comanda ativa.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var role   = GetUserRole();

        _logger.LogInformation("Usuário {UserId} ({Role}) conectado ao ComandaHub", userId, role);

        if (role == "Admin")
        {
            // Admin entra no grupo que recebe TODAS as atualizações de comandas
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
            _logger.LogInformation("Admin adicionado ao grupo {Group}", AdminGroup);
        }
        else if (role == "Customer")
        {
            // Cliente entra no grupo específico da sua comanda ativa
            var comandaId = await _comandaService.GetActiveComandaIdByUserAsync(userId);
            if (comandaId.HasValue)
            {
                var comandaGroup = GetComandaGroup(comandaId.Value);
                await Groups.AddToGroupAsync(Context.ConnectionId, comandaGroup);
            }
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Chamado automaticamente quando um cliente desconecta.
    /// Importante: não fechamos a comanda aqui — o cliente pode reconectar.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        _logger.LogInformation("Usuário {UserId} desconectado do ComandaHub", userId);

        if (exception != null)
        {
            _logger.LogWarning(exception, "Desconexão com erro para usuário {UserId}", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // =========================================================================
    // MÉTODOS CHAMADOS PELO CLIENTE (Customer → Hub → Admin)
    // =========================================================================

    /// <summary>
    /// Cliente adiciona um item à sua comanda.
    ///
    /// FLUXO:
    ///   1. Cliente chama este método via SignalR
    ///   2. Hub persiste o item no banco (via ComandaService)
    ///   3. Hub notifica o Admin com os dados atualizados
    ///   4. Hub confirma a operação de volta para o cliente
    /// </summary>
    /// <param name="request">Dados do item a ser adicionado.</param>
    public async Task AddItemToComanda(AddItemToComandaRequest request)
    {
        var userId = GetUserId();

        try
        {
            // Persiste no banco e recalcula o total
            var updatedComanda = await _comandaService.AddItemAsync(userId, request);

            // ---------------------------------------------------------------
            // Notifica o Admin no dashboard em tempo real
            // ---------------------------------------------------------------
            await Clients.Group(AdminGroup)
                         .SendAsync("ComandaUpdated", new ComandaUpdateEvent
                         {
                             ComandaId        = updatedComanda.Id,
                             UserId           = userId,
                             UserName         = updatedComanda.UserName,
                             TableIdentifier  = updatedComanda.TableIdentifier,
                             TotalInReais     = updatedComanda.TotalInReais,
                             Status           = updatedComanda.Status,
                             LastItemAdded    = request.ItemName,
                             UpdatedAt        = DateTime.UtcNow
                         });

            // ---------------------------------------------------------------
            // Confirma a operação para o próprio cliente
            // ---------------------------------------------------------------
            await Clients.Caller
                         .SendAsync("ItemAddedConfirmation", new
                         {
                             Success          = true,
                             ComandaId        = updatedComanda.Id,
                             NewTotalInReais  = updatedComanda.TotalInReais,
                             Message          = $"'{request.ItemName}' adicionado com sucesso!"
                         });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar item para usuário {UserId}", userId);

            // Notifica o cliente sobre o erro
            await Clients.Caller
                         .SendAsync("Error", new { Message = "Não foi possível adicionar o item. Tente novamente." });
        }
    }

    // =========================================================================
    // MÉTODOS CHAMADOS PELO ADMIN (Admin → Hub → Cliente/Todos)
    // =========================================================================

    /// <summary>
    /// Admin fecha uma comanda (pagamento recebido).
    /// Notifica o cliente que sua comanda foi encerrada.
    /// </summary>
    /// <param name="comandaId">ID da comanda a ser fechada.</param>
    [Authorize(Roles = "Admin")]
    public async Task CloseComanda(Guid comandaId)
    {
        var adminId = GetUserId();

        try
        {
            var closedComanda = await _comandaService.CloseComandaAsync(comandaId, adminId);

            // Notifica o cliente específico daquela comanda
            var comandaGroup = GetComandaGroup(comandaId);
            await Clients.Group(comandaGroup)
                         .SendAsync("ComandaClosed", new
                         {
                             ComandaId     = comandaId,
                             TotalInReais  = closedComanda.TotalInReais,
                             ClosedAt      = DateTime.UtcNow,
                             Message       = "Sua comanda foi fechada. Obrigado pela visita!"
                         });

            // Atualiza o dashboard do Admin (remove da lista de abertas)
            await Clients.Group(AdminGroup)
                         .SendAsync("ComandaClosed", new { ComandaId = comandaId });

            _logger.LogInformation("Comanda {ComandaId} fechada pelo Admin {AdminId}", comandaId, adminId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fechar comanda {ComandaId}", comandaId);
            await Clients.Caller.SendAsync("Error", new { Message = "Erro ao fechar comanda." });
        }
    }

    /// <summary>
    /// Admin adiciona um item manualmente a uma comanda de cliente.
    /// Útil quando o pedido é feito verbalmente ao balcão.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task AdminAddItemToComanda(Guid comandaId, AddItemToComandaRequest request)
    {
        var adminId = GetUserId();

        var updatedComanda = await _comandaService.AdminAddItemAsync(comandaId, adminId, request);

        // Notifica o grupo desta comanda (o cliente na mesa)
        var comandaGroup = GetComandaGroup(comandaId);
        await Clients.Group(comandaGroup)
                     .SendAsync("ItemAddedByAdmin", new
                     {
                         ItemName        = request.ItemName,
                         Quantity        = request.Quantity,
                         NewTotalInReais = updatedComanda.TotalInReais
                     });

        // Atualiza o próprio dashboard do Admin
        await Clients.Group(AdminGroup)
                     .SendAsync("ComandaUpdated", new ComandaUpdateEvent
                     {
                         ComandaId       = updatedComanda.Id,
                         TotalInReais    = updatedComanda.TotalInReais,
                         LastItemAdded   = request.ItemName,
                         UpdatedAt       = DateTime.UtcNow
                     });
    }

    // =========================================================================
    // HELPERS PRIVADOS
    // =========================================================================

    /// <summary>Retorna o ID do usuário autenticado a partir do JWT.</summary>
    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst("sub")
                 ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            throw new HubException("Usuário não autenticado ou token inválido.");

        return userId;
    }

    /// <summary>Retorna o perfil (Role) do usuário autenticado.</summary>
    private string GetUserRole()
    {
        return Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Customer";
    }

    /// <summary>Gera o nome do grupo SignalR de uma comanda específica.</summary>
    private static string GetComandaGroup(Guid comandaId) => $"Comanda_{comandaId}";
}

// =============================================================================
// DTOs específicos do Hub (mensagens de eventos)
// =============================================================================

/// <summary>Evento emitido ao Admin quando uma comanda é atualizada.</summary>
public class ComandaUpdateEvent
{
    public Guid     ComandaId       { get; set; }
    public Guid     UserId          { get; set; }
    public string   UserName        { get; set; } = string.Empty;
    public string?  TableIdentifier { get; set; }
    public decimal  TotalInReais    { get; set; }
    public string   Status          { get; set; } = string.Empty;
    public string?  LastItemAdded   { get; set; }
    public DateTime UpdatedAt       { get; set; }
}
