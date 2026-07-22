// =============================================================================
// ComandaHub.cs — Hub SignalR para comunicação em tempo real de Comandas
//
// GRUPOS:
//   AdminDashboard_{tenantId} → Admins da loja — recebe tudo daquele tenant
//   User_{userId}        → Cliente sempre entra aqui (mesmo sem comanda ativa)
//   Comanda_{comandaId}  → Grupo específico de uma comanda aberta
//
// EVENTOS emitidos pelo servidor:
//   ComandaOpened        → User_{userId}        quando admin abre comanda pro cliente
//   ComandaUpdated       → Comanda_{id}+Admin   quando item é adicionado/removido/alterado
//   ItemAddedByAdmin     → Comanda_{id}         quando admin adiciona item manualmente
//   ComandaClosed        → Comanda_{id}+Admin   quando comanda é fechada
//   ComandaCancelled     → Comanda_{id}+Admin   quando comanda é cancelada
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CardGameStore.Hubs;

[Authorize]
public class ComandaHub : Hub
{
    private readonly IComandaService _comandaService;
    private readonly ITenantContext  _tenant;
    private readonly ILogger<ComandaHub> _logger;

    // Antes era uma constante única ("AdminDashboard") compartilhada por TODOS
    // os tenants — qualquer admin conectado em qualquer loja recebia as
    // atualizações de comanda em tempo real de todas as outras lojas também
    // (vazamento cross-tenant). Agora é escopada por tenant.
    public static string GetAdminGroup(Guid tenantId) => $"AdminDashboard_{tenantId}";

    public ComandaHub(IComandaService comandaService, ITenantContext tenant, ILogger<ComandaHub> logger)
    {
        _comandaService = comandaService;
        _tenant         = tenant;
        _logger         = logger;
    }

    // =========================================================================
    // CICLO DE VIDA
    // =========================================================================

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var role   = GetUserRole();

        _logger.LogInformation("Usuário {UserId} ({Role}) conectado ao ComandaHub", userId, role);

        // B3: Operator tinha o mesmo acesso administrativo via REST (policy AdminOnly aceita
        // os dois papéis em todo o resto do sistema) mas caía no branch de Customer aqui —
        // sem tempo real do dashboard, precisava recarregar a página pra ver atualização.
        if (role == "Admin" || role == "Operator")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetAdminGroup(_tenant.TenantId));
        }
        else
        {
            // Cliente SEMPRE entra no grupo pessoal — recebe ComandaOpened mesmo sem comanda
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId));

            // Se já tem comanda ativa, entra no grupo dela também
            var comandaId = await _comandaService.GetActiveComandaIdByUserAsync(userId);
            if (comandaId.HasValue)
                await Groups.AddToGroupAsync(Context.ConnectionId, GetComandaGroup(comandaId.Value));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Usuário {UserId} desconectado do ComandaHub", GetUserId());
        await base.OnDisconnectedAsync(exception);
    }

    // =========================================================================
    // CLIENTE → HUB: adicionar item
    // =========================================================================

    public async Task AddItemToComanda(AddItemToComandaRequest request)
    {
        var userId = GetUserId();
        try
        {
            var updated = await _comandaService.AddItemAsync(userId, request);
            // O serviço emite ComandaUpdated para o AdminGroup automaticamente

            await Clients.Caller.SendAsync("ItemAddedConfirmation", new
            {
                Success         = true,
                ComandaId       = updated.Id,
                NewTotalInReais = updated.TotalInReais,
                Message         = $"'{request.ItemName}' adicionado com sucesso!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar item para usuário {UserId}", userId);
            await Clients.Caller.SendAsync("Error", new { Message = "Não foi possível adicionar o item." });
        }
    }

    // =========================================================================
    // ADMIN → HUB: fechar comanda (via Hub — mantido para compatibilidade)
    // =========================================================================

    // B3: alinhado com a policy AdminOnly do REST (aceita Admin e Operator) — antes só Admin
    // conseguia usar este RPC do hub, inconsistente com o endpoint REST equivalente.
    [Authorize(Roles = "Admin,Operator")]
    public async Task CloseComanda(Guid comandaId)
    {
        var adminId = GetUserId();
        try
        {
            await _comandaService.CloseComandaAsync(comandaId, adminId);
            // O serviço agora emite os eventos SignalR automaticamente
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fechar comanda {ComandaId} via Hub", comandaId);
            await Clients.Caller.SendAsync("Error", new { Message = "Erro ao fechar comanda." });
        }
    }

    // =========================================================================
    // ADMIN → HUB: adicionar item manualmente (via Hub — mantido para compatibilidade)
    // =========================================================================

    [Authorize(Roles = "Admin,Operator")]
    public async Task AdminAddItemToComanda(Guid comandaId, AddItemToComandaRequest request)
    {
        var adminId = GetUserId();
        await _comandaService.AdminAddItemAsync(comandaId, adminId, request);
        // O serviço emite os eventos SignalR automaticamente
    }

    // =========================================================================
    // CLIENTE → HUB: entrar no grupo de uma comanda após receber ComandaOpened
    // =========================================================================

    public async Task JoinComandaGroup(Guid comandaId)
    {
        var userId = GetUserId();
        // Valida que a comanda pertence ao usuário antes de adicionar ao grupo
        var comanda = await _comandaService.GetByIdAsync(comandaId);
        if (comanda != null && comanda.UserId == userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetComandaGroup(comandaId));
            _logger.LogInformation("Cliente {UserId} entrou no grupo Comanda_{ComandaId}", userId, comandaId);
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst("sub")
                 ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim == null || !Guid.TryParse(claim.Value, out var id))
            throw new HubException("Usuário não autenticado.");
        return id;
    }

    private string GetUserRole() =>
        Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Customer";

    public static string GetComandaGroup(Guid comandaId) => $"Comanda_{comandaId}";
    public static string GetUserGroup(Guid userId)       => $"User_{userId}";
}

// =============================================================================
// DTOs de eventos SignalR
// =============================================================================

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
