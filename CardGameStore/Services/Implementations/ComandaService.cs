using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Hubs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class ComandaService : IComandaService
{
    // Fuso horário de Brasília (UTC-3 fixo; BR não usa horário de verão desde 2019).
    // Funciona em Linux (IANA) e Windows (ID legado).
    private static readonly TimeZoneInfo BrazilZone = GetBrazilZone();
    private static TimeZoneInfo GetBrazilZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }

    /// <summary>
    /// Retorna o intervalo UTC correspondente a um dia no fuso de Brasília.
    /// Ex.: dia 29/05 BR → [29/05 03:00 UTC, 30/05 03:00 UTC)
    /// </summary>
    private static (DateTime InicioUtc, DateTime FimUtc) DiaBrasil(DateTime? dia = null)
    {
        var agora    = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilZone);
        var dataBr   = dia.HasValue ? dia.Value.Date : agora.Date;
        var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(dataBr, DateTimeKind.Unspecified), BrazilZone);
        return (inicioUtc, inicioUtc.AddDays(1));
    }

    // Constantes para evitar magic strings sensíveis a typo/case
    private const string PaymentCrediario = "Crediario";
    private const string PaymentPontos    = "Pontos";
    private const string PaymentCashback  = "Cashback";

    private readonly AppDbContext            _db;
    private readonly IEmailService           _email;
    private readonly ILogger<ComandaService> _logger;
    private readonly IServiceScopeFactory    _scopeFactory;
    private readonly IHubContext<ComandaHub> _hub;

    public ComandaService(AppDbContext db, IEmailService email, ILogger<ComandaService> logger, IServiceScopeFactory scopeFactory, IHubContext<ComandaHub> hub)
    {
        _db           = db;
        _email        = email;
        _logger       = logger;
        _scopeFactory = scopeFactory;
        _hub          = hub;
    }

    public async Task<ComandaDto> OpenComandaAsync(Guid userId, string? tableIdentifier = null)
    {
        // Verifica se já existe uma comanda aberta ou em andamento para este usuário.
        // Se existir, reutilizamos ela para evitar duplicidade (ex: cliente leu QR code duas vezes).
        var comandaExistente = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .Where(c => c.UserId == userId && (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento))
            .OrderByDescending(c => c.OpenedAt)
            .FirstOrDefaultAsync();

        if (comandaExistente != null)
        {
            _logger.LogInformation("Reutilizando comanda {Id} existente para usuário {UserId}", comandaExistente.Id, userId);
            
            // Se o identificador da mesa mudou (cliente trocou de lugar), atualizamos
            if (!string.IsNullOrEmpty(tableIdentifier) && comandaExistente.TableIdentifier != tableIdentifier)
            {
                comandaExistente.TableIdentifier = tableIdentifier;
                await _db.SaveChangesAsync();
                
                // Notifica o admin que a mesa da comanda mudou
                await _hub.Clients.Group(ComandaHub.AdminGroup)
                    .SendAsync("ComandaUpdated", new ComandaUpdateEvent
                    {
                        ComandaId       = comandaExistente.Id,
                        UserId          = userId,
                        UserName        = comandaExistente.User?.Name ?? string.Empty,
                        TableIdentifier = tableIdentifier,
                        TotalInReais    = comandaExistente.TotalInReais,
                        Status          = comandaExistente.Status.ToString(),
                        UpdatedAt       = DateTime.UtcNow,
                    });
            }

            return MapToDto(comandaExistente);
        }

        // Se não houver comanda ativa, cria uma nova
        var comanda = new Comanda
        {
            UserId          = userId,
            TableIdentifier = tableIdentifier,
            Status          = ComandaStatus.Aberta,
        };

        _db.Comandas.Add(comanda);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Comanda {Id} aberta para usuário {UserId}", comanda.Id, userId);

        // Notifica o cliente (User_{userId}) que a comanda foi aberta pelo admin
        await _hub.Clients.Group(ComandaHub.GetUserGroup(userId))
            .SendAsync("ComandaOpened", new { ComandaId = comanda.Id, TableIdentifier = comanda.TableIdentifier });

        // Notifica o admin (dashboard)
        var userName = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync() ?? string.Empty;
        await _hub.Clients.Group(ComandaHub.AdminGroup)
            .SendAsync("ComandaOpened", new { ComandaId = comanda.Id, UserId = userId, UserName = userName, TableIdentifier = comanda.TableIdentifier });

        return MapToDto(comanda);
    }

    public async Task<ComandaDto?> GetActiveComandaAsync(Guid userId)
    {
        // Se houver múltiplas abertas, retorna a mais recente
        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .Where(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento))
            .OrderByDescending(c => c.OpenedAt)
            .FirstOrDefaultAsync();

        return comanda == null ? null : MapToDto(comanda);
    }

    public async Task<Guid?> GetActiveComandaIdByUserAsync(Guid userId)
    {
        // Se houver múltiplas abertas, retorna a ID da mais recente
        return await _db.Comandas
            .Where(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento))
            .OrderByDescending(c => c.OpenedAt)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<ComandaDto?> GetByIdAsync(Guid comandaId)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comandaId);

        return comanda == null ? null : MapToDto(comanda);
    }

    public async Task<ComandaDto> AddItemAsync(Guid userId, AddItemToComandaRequest request)
    {
        if (request.Quantity <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.");

        // Se houver múltiplas abertas, adiciona à mais recente
        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .Where(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento))
            .OrderByDescending(c => c.OpenedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Comanda ativa não encontrada para este usuário.");

        var (itemName, priceInCents) = await ResolveItemAsync(request);

        var item = new ComandaItem
        {
            ComandaId        = comanda.Id,
            ProductId        = request.ProductId,
            CardCacheId      = request.CardCacheId,
            ItemNameSnapshot = itemName,
            UnitPriceInCents = priceInCents,
            Quantity         = request.Quantity,
            SubtotalInCents  = priceInCents * request.Quantity,
            AddedByUserId    = userId,
        };

        // _db.Add ensures EntityState.Added; navigation fixup populates comanda.Items.
        // Using comanda.Items.Add alone causes EF to infer Modified (not Added) for
        // entities with a pre-set client-generated GUID key.
        _db.Add(item);
        comanda.Status        = ComandaStatus.EmAndamento;
        comanda.TotalInCents += item.SubtotalInCents;

        await _db.SaveChangesAsync();

        // Notifica o admin sobre o item adicionado pelo cliente
        await _hub.Clients.Group(ComandaHub.AdminGroup)
            .SendAsync("ComandaUpdated", new ComandaUpdateEvent
            {
                ComandaId       = comanda.Id,
                UserId          = userId,
                UserName        = comanda.User?.Name ?? string.Empty,
                TableIdentifier = comanda.TableIdentifier,
                TotalInReais    = comanda.TotalInReais,
                Status          = comanda.Status.ToString(),
                LastItemAdded   = item.ItemNameSnapshot,
                UpdatedAt       = DateTime.UtcNow,
            });

        return MapToDto(comanda);
    }

    public async Task<ComandaDto> AdminAddItemAsync(Guid comandaId, Guid adminId, AddItemToComandaRequest request)
    {
        if (request.Quantity <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.");

        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comandaId)
            ?? throw new InvalidOperationException($"Comanda {comandaId} não encontrada.");

        var (itemName, priceInCents) = await ResolveItemAsync(request);

        var item = new ComandaItem
        {
            ComandaId        = comanda.Id,
            ProductId        = request.ProductId,
            CardCacheId      = request.CardCacheId,
            ItemNameSnapshot = itemName,
            UnitPriceInCents = priceInCents,
            Quantity         = request.Quantity,
            SubtotalInCents  = priceInCents * request.Quantity,
            AddedByUserId    = adminId,
        };

        _db.Add(item);
        comanda.Status        = ComandaStatus.EmAndamento;
        comanda.TotalInCents += item.SubtotalInCents;

        await _db.SaveChangesAsync();

        // Notifica o cliente na comanda que o admin adicionou um item
        await _hub.Clients.Group(ComandaHub.GetComandaGroup(comandaId))
            .SendAsync("ItemAddedByAdmin", new
            {
                ItemName        = item.ItemNameSnapshot,
                NewTotalInReais = comanda.TotalInReais,
            });
        // Notifica o admin (outros painéis)
        await _hub.Clients.Group(ComandaHub.AdminGroup)
            .SendAsync("ComandaUpdated", new ComandaUpdateEvent
            {
                ComandaId       = comanda.Id,
                UserId          = comanda.UserId,
                UserName        = comanda.User?.Name ?? string.Empty,
                TableIdentifier = comanda.TableIdentifier,
                TotalInReais    = comanda.TotalInReais,
                Status          = comanda.Status.ToString(),
                LastItemAdded   = item.ItemNameSnapshot,
                UpdatedAt       = DateTime.UtcNow,
            });

        return MapToDto(comanda);
    }

    public async Task<ComandaDto> RemoveItemAsync(Guid comandaId, Guid itemId, Guid requestingUserId)
    {
        var item = await _db.ComandaItems.FindAsync(itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");

        // Garante que o item pertence à comanda informada (evita remoção cruzada)
        if (item.ComandaId != comandaId)
            throw new InvalidOperationException("Item não pertence a esta comanda.");

        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comandaId);

        comanda.TotalInCents = Math.Max(0, comanda.TotalInCents - item.SubtotalInCents);

        if (item.ProductId.HasValue)
        {
            await _db.Products
                .Where(p => p.Id == item.ProductId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.StockQuantity, p => p.StockQuantity + item.Quantity));
        }

        // Ajusta pontos aplicados se o total ficou menor que o desconto
        if (comanda.PointsApplied > comanda.TotalInCents)
        {
            var excess = comanda.PointsApplied - comanda.TotalInCents;
            comanda.PointsApplied = comanda.TotalInCents;
            comanda.User!.PointsBalance += excess;
        }

        _db.ComandaItems.Remove(item);

        if (comanda.Items.Count(i => i.Id != itemId) == 0)
            comanda.Status = ComandaStatus.Aberta;

        await _db.SaveChangesAsync();

        var dto = MapToDto(comanda);
        // Notifica cliente e admin da remoção
        await _hub.Clients.Group(ComandaHub.GetComandaGroup(comandaId))
            .SendAsync("ComandaUpdated", new { ComandaId = comandaId, NewTotalInReais = dto.TotalInReais });
        await _hub.Clients.Group(ComandaHub.AdminGroup)
            .SendAsync("ComandaUpdated", new ComandaUpdateEvent
            {
                ComandaId       = dto.Id,
                UserId          = dto.UserId,
                UserName        = dto.UserName,
                TableIdentifier = dto.TableIdentifier,
                TotalInReais    = dto.TotalInReais,
                Status          = dto.Status,
                UpdatedAt       = DateTime.UtcNow,
            });

        return dto;
    }

    public async Task<ComandaDto> UpdateItemAsync(Guid comandaId, Guid itemId, int newQuantity, Guid adminId)
    {
        var item = await _db.ComandaItems.FindAsync(itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");

        if (item.ComandaId != comandaId)
            throw new InvalidOperationException("Item não pertence a esta comanda.");

        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comandaId);

        if (comanda.Status == ComandaStatus.Fechada || comanda.Status == ComandaStatus.Cancelada)
            throw new InvalidOperationException("Não é possível editar itens de uma comanda encerrada.");

        // Quantity == 0 → remover item
        if (newQuantity <= 0)
            return await RemoveItemAsync(comandaId, itemId, adminId);

        var oldSubtotal = item.SubtotalInCents;
        var diff        = item.UnitPriceInCents * newQuantity - oldSubtotal;

        // Ajusta estoque físico
        if (item.ProductId.HasValue)
        {
            var delta = item.Quantity - newQuantity; // positivo = devolve, negativo = retira
            await _db.Products
                .Where(p => p.Id == item.ProductId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.StockQuantity, p => p.StockQuantity + delta));
        }

        item.Quantity       = newQuantity;
        item.SubtotalInCents = item.UnitPriceInCents * newQuantity;
        comanda.TotalInCents = Math.Max(0, comanda.TotalInCents + diff);

        // Ajusta pontos se o total ficou menor que o desconto aplicado
        if (comanda.PointsApplied > comanda.TotalInCents)
        {
            var excess = comanda.PointsApplied - comanda.TotalInCents;
            comanda.PointsApplied  = comanda.TotalInCents;
            comanda.User!.PointsBalance += excess;
        }

        _logger.LogInformation(
            "Item {ItemId} da comanda {ComandaId} atualizado para qty={Qty} pelo admin {AdminId}",
            itemId, comandaId, newQuantity, adminId);

        await _db.SaveChangesAsync();

        var dto = MapToDto(comanda);
        // Notifica cliente e admin da atualização de quantidade
        await _hub.Clients.Group(ComandaHub.GetComandaGroup(comandaId))
            .SendAsync("ComandaUpdated", new { ComandaId = comandaId, NewTotalInReais = dto.TotalInReais });
        await _hub.Clients.Group(ComandaHub.AdminGroup)
            .SendAsync("ComandaUpdated", new ComandaUpdateEvent
            {
                ComandaId       = dto.Id,
                UserId          = dto.UserId,
                UserName        = dto.UserName,
                TableIdentifier = dto.TableIdentifier,
                TotalInReais    = dto.TotalInReais,
                Status          = dto.Status,
                UpdatedAt       = DateTime.UtcNow,
            });

        return dto;
    }

    public async Task<ComandaDto> CloseComandaAsync(Guid comandaId, Guid adminId, string paymentMethod = "Dinheiro", string? observacao = null)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comandaId)
            ?? throw new InvalidOperationException($"Comanda {comandaId} não encontrada.");

        // ── Crediário ─────────────────────────────────────────────────────────
        if (paymentMethod == PaymentCrediario)
        {
            var crediarioExistente = await _db.Crediarios
                .FirstOrDefaultAsync(c => c.UserId == comanda.UserId && c.Status == CrediariosStatus.Aberto);

            var vencimento = DateTime.UtcNow.AddDays(30);

            if (crediarioExistente != null)
            {
                // Acumula no crediário já aberto (cliente com conta corrente/aba)
                crediarioExistente.ValorEmCentavos += comanda.TotalInCents;
                crediarioExistente.DataVencimento   = vencimento;
                if (!string.IsNullOrWhiteSpace(observacao))
                    crediarioExistente.Observacao = observacao;

                _logger.LogInformation(
                    "Comanda {ComandaId} acumulada no crediário {CredId} do usuário {UserId} — novo total R$ {Valor:N2}, vence em {Venc:dd/MM/yyyy}",
                    comandaId, crediarioExistente.Id, comanda.UserId,
                    crediarioExistente.ValorEmCentavos / 100m, vencimento);
            }
            else
            {
                // Cria novo crediário
                var crediario = new Crediario
                {
                    UserId           = comanda.UserId,
                    ComandaId        = comanda.Id,
                    ValorEmCentavos  = comanda.TotalInCents,
                    DataAbertura     = DateTime.UtcNow,
                    DataVencimento   = vencimento,
                    Status           = CrediariosStatus.Aberto,
                    AbertoPorAdminId = adminId,
                    Observacao       = observacao,
                };

                _db.Crediarios.Add(crediario);
                _logger.LogInformation(
                    "Crediário {CredId} criado para usuário {UserId} — R$ {Valor:N2}, vence em {Venc:dd/MM/yyyy}",
                    crediario.Id, comanda.UserId, crediario.ValorEmReais, vencimento);

                // Envia email em background com escopo próprio (evita uso de Scoped service após dispose)
                if (!string.IsNullOrWhiteSpace(comanda.User?.Email))
                {
                    var emailAddr  = comanda.User.Email;
                    var userName   = comanda.User.Name;
                    var valorReais = crediario.ValorEmReais;
                    var venc       = vencimento;
                    _ = Task.Run(async () =>
                    {
                        using var scope        = _scopeFactory.CreateScope();
                        var emailService       = scope.ServiceProvider.GetRequiredService<IEmailService>();
                        await emailService.SendCrediarioAbertoAsync(emailAddr, userName, valorReais, venc);
                    });
                }
            }
        }

        // ── Pontos como pagamento ─────────────────────────────────────────────
        if (paymentMethod == PaymentPontos)
        {
            if (comanda.User == null)
                throw new InvalidOperationException("Usuário não encontrado.");

            var totalRestante = Math.Max(0, comanda.TotalInCents - comanda.PointsApplied);
            if (comanda.User.PointsBalance < totalRestante)
                throw new InvalidOperationException(
                    $"Saldo de pontos insuficiente. Cliente tem {comanda.User.PointsBalance} pts, faltam {totalRestante} pts.");

            comanda.User.PointsBalance -= totalRestante;
            comanda.PointsApplied       = comanda.TotalInCents; // comanda totalmente coberta
            comanda.User.UpdatedAt      = DateTime.UtcNow;
            _logger.LogInformation(
                "Comanda {Id} quitada com {Pts} pontos do usuário {UserId}. Saldo restante: {Saldo}",
                comandaId, totalRestante, comanda.UserId, comanda.User.PointsBalance);
        }

        // ── Cashback como pagamento ───────────────────────────────────────────
        if (paymentMethod == PaymentCashback)
        {
            if (comanda.User == null)
                throw new InvalidOperationException("Usuário não encontrado.");

            var totalRestante = Math.Max(0, comanda.TotalInCents - comanda.PointsApplied);
            if (comanda.User.BalanceInCents < totalRestante)
                throw new InvalidOperationException(
                    $"Saldo insuficiente. Cliente tem R$ {comanda.User.BalanceInCents / 100m:N2}, falta R$ {totalRestante / 100m:N2}.");

            comanda.User.BalanceInCents -= totalRestante;
            comanda.User.UpdatedAt       = DateTime.UtcNow;
            _logger.LogInformation(
                "Comanda {Id} quitada com R$ {Valor:N2} de cashback do usuário {UserId}. Saldo restante: R$ {Saldo:N2}",
                comandaId, totalRestante / 100m, comanda.UserId, comanda.User.BalanceInCents / 100m);
        }

        comanda.Status        = ComandaStatus.Fechada;
        comanda.ClosedAt      = DateTime.UtcNow;
        comanda.PaymentMethod = paymentMethod;

        // ── Pontos de fidelidade ──────────────────────────────────────────────
        // Regra: 1 ponto por R$1 gasto (após desconto de pontos aplicados)
        // Não acumula pontos quando o próprio pagamento é via pontos ou cashback
        if (comanda.User != null && paymentMethod != PaymentCrediario
                                 && paymentMethod != PaymentPontos
                                 && paymentMethod != PaymentCashback)
        {
            var valorPago   = Math.Max(0, comanda.TotalInCents - comanda.PointsApplied);
            var pontosGanhos = valorPago / 100; // 1 ponto por real
            if (pontosGanhos > 0)
            {
                comanda.User.PointsBalance  += pontosGanhos;
                comanda.User.PointsExpiresAt = DateTime.UtcNow.AddYears(1);
                _logger.LogInformation(
                    "Usuário {UserId} ganhou {Pontos} pontos na comanda {ComandaId}.",
                    comanda.UserId, pontosGanhos, comandaId);
            }
        }

        await _db.SaveChangesAsync();

        var dto = MapToDto(comanda);
        // Notifica o cliente que a comanda foi fechada
        await _hub.Clients.Group(ComandaHub.GetComandaGroup(comandaId))
            .SendAsync("ComandaClosed", new { ComandaId = comandaId, PaymentMethod = paymentMethod });
        // Notifica o admin
        await _hub.Clients.Group(ComandaHub.AdminGroup)
            .SendAsync("ComandaClosed", new
            {
                ComandaId     = comandaId,
                UserId        = comanda.UserId,
                UserName      = dto.UserName,
                TotalInReais  = dto.TotalInReais,
                PaymentMethod = paymentMethod,
            });

        return dto;
    }

    public async Task<ComandaDto> CancelComandaAsync(Guid comandaId, Guid adminId)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comandaId)
            ?? throw new InvalidOperationException($"Comanda {comandaId} não encontrada.");

        foreach (var item in comanda.Items.Where(i => i.ProductId.HasValue))
        {
            await _db.Products
                .Where(p => p.Id == item.ProductId!.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.StockQuantity, p => p.StockQuantity + item.Quantity));
        }

        comanda.Status   = ComandaStatus.Cancelada;
        comanda.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Comanda {Id} cancelada — estoque restaurado para {Count} produto(s).",
            comandaId, comanda.Items.Count(i => i.ProductId.HasValue));

        // Notifica o cliente que a comanda foi cancelada
        await _hub.Clients.Group(ComandaHub.GetComandaGroup(comandaId))
            .SendAsync("ComandaCancelled", new { ComandaId = comandaId });
        // Notifica o admin
        await _hub.Clients.Group(ComandaHub.AdminGroup)
            .SendAsync("ComandaCancelled", new { ComandaId = comandaId, UserId = comanda.UserId });

        return MapToDto(comanda);
    }

    public async Task<IEnumerable<ComandaDto>> GetActiveCommandasForDashboardAsync()
    {
        var comandas = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .Where(c => c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento)
            .OrderByDescending(c => c.OpenedAt)
            .ToListAsync();

        return comandas.Select(MapToDto).ToList();
    }

    public async Task<IEnumerable<ComandaDto>> GetUserHistoryAsync(Guid userId, int limit = 20)
    {
        var comandas = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .Where(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Fechada || c.Status == ComandaStatus.Cancelada))
            .OrderByDescending(c => c.ClosedAt)
            .Take(limit)
            .ToListAsync();

        return comandas.Select(MapToDto).ToList();
    }

    public async Task<IEnumerable<ComandaDto>> GetTodayHistoryAsync(DateTime? data = null)
    {
        // Usa intervalo UTC calculado a partir do fuso de Brasília.
        // Sem isso, uma comanda fechada às 22h30 BR (= 01h30 UTC do dia seguinte)
        // aparecia incorretamente como "hoje" no dashboard.
        var (inicioUtc, fimUtc) = DiaBrasil(data);

        var comandas = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .Where(c => (c.Status == ComandaStatus.Fechada || c.Status == ComandaStatus.Cancelada)
                     && c.ClosedAt.HasValue
                     && c.ClosedAt.Value >= inicioUtc
                     && c.ClosedAt.Value <  fimUtc)
            .OrderByDescending(c => c.ClosedAt)
            .ToListAsync();

        return comandas.Select(MapToDto).ToList();
    }

    public async Task<ComandaDto> ApplyPointsAsync(Guid comandaId, Guid userId, int points)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");

        if (comanda.UserId != userId)
            throw new InvalidOperationException("Você só pode usar pontos na sua própria comanda.");

        if (comanda.Status == ComandaStatus.Fechada || comanda.Status == ComandaStatus.Cancelada)
            throw new InvalidOperationException("Não é possível aplicar pontos em comanda encerrada.");

        if (comanda.PointsApplied > 0)
            throw new InvalidOperationException("Pontos já foram aplicados nesta comanda.");

        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        if (user.PointsExpiresAt.HasValue && user.PointsExpiresAt.Value < DateTime.UtcNow)
            throw new InvalidOperationException("Seus pontos estão expirados.");

        if (user.PointsBalance < points)
            throw new InvalidOperationException($"Saldo insuficiente. Você tem {user.PointsBalance} pontos.");

        // Não permite abater mais do que o total da comanda
        var pontosAplicados = Math.Min(points, comanda.TotalInCents);

        user.PointsBalance   -= pontosAplicados;
        user.UpdatedAt        = DateTime.UtcNow;
        comanda.PointsApplied = pontosAplicados;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Usuário {UserId} aplicou {Points} pontos na comanda {ComandaId}.",
            userId, pontosAplicados, comandaId);

        return MapToDto(comanda);
    }

    public async Task<ComandaDto> RemovePointsAsync(Guid comandaId, Guid requestingUserId)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");

        if (comanda.Status == ComandaStatus.Fechada || comanda.Status == ComandaStatus.Cancelada)
            throw new InvalidOperationException("Não é possível remover pontos de comanda encerrada.");

        if (comanda.PointsApplied == 0)
            throw new InvalidOperationException("Não há pontos aplicados nesta comanda.");

        // Valida permissão: deve ser o dono da comanda ou um Admin
        var isOwner = comanda.UserId == requestingUserId;
        if (!isOwner)
        {
            var requestingUser = await _db.Users.FindAsync(requestingUserId)
                ?? throw new UnauthorizedAccessException("Usuário solicitante não encontrado.");
            if (requestingUser.Role != UserRole.Admin)
                throw new UnauthorizedAccessException("Sem permissão para remover pontos desta comanda.");
        }

        var user = await _db.Users.FindAsync(comanda.UserId)
            ?? throw new InvalidOperationException("Usuário da comanda não encontrado.");

        var pontosDevolvidos   = comanda.PointsApplied;
        user.PointsBalance    += pontosDevolvidos;
        user.UpdatedAt         = DateTime.UtcNow;
        comanda.PointsApplied  = 0;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Pontos removidos da comanda {ComandaId} por {RequestingUserId}. {Points} pts devolvidos ao usuário {UserId}.",
            comandaId, requestingUserId, pontosDevolvidos, comanda.UserId);

        var dto = MapToDto(comanda);

        // Notifica o cliente que os pontos foram removidos
        await _hub.Clients.Group(ComandaHub.GetComandaGroup(comandaId))
            .SendAsync("ComandaUpdated", new { ComandaId = comandaId, NewTotalInReais = dto.TotalInReais });
        // Notifica o admin (dashboard)
        await _hub.Clients.Group(ComandaHub.AdminGroup)
            .SendAsync("ComandaUpdated", new ComandaUpdateEvent
            {
                ComandaId  = dto.Id,
                UserId     = dto.UserId,
                UserName   = comanda.User?.Name ?? string.Empty,
                Status     = dto.Status,
                LastItemAdded = "(pontos removidos)",
                UpdatedAt  = DateTime.UtcNow,
            });

        return dto;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve nome e preço do item. Se vier ProductId, busca do banco e ignora
    /// o que o cliente enviou — nunca confiar em preço vindo da requisição.
    /// </summary>
    private async Task<(string name, int priceInCents)> ResolveItemAsync(AddItemToComandaRequest request)
    {
        if (!request.ProductId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(request.ItemName))
                throw new ArgumentException("Nome do item é obrigatório para itens sem produto cadastrado.");
            return (request.ItemName, request.UnitPriceInCents);
        }

        var product = await _db.Products.FindAsync(request.ProductId.Value)
            ?? throw new InvalidOperationException("Produto não encontrado.");

        if (!product.IsActive)
            throw new InvalidOperationException("Produto inativo e não pode ser adicionado.");

        if (product.StockQuantity < request.Quantity)
            throw new InvalidOperationException(
                $"Estoque insuficiente para '{product.Name}'. Disponível: {product.StockQuantity} un.");

        // Decremento atômico: UPDATE ... WHERE stock >= qty
        // Evita race condition em vendas simultâneas (estoque negativo)
        var updated = await _db.Products
            .Where(p => p.Id == product.Id && p.StockQuantity >= request.Quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(
                p => p.StockQuantity, p => p.StockQuantity - request.Quantity));

        if (updated == 0)
            throw new InvalidOperationException(
                $"Estoque insuficiente para '{product.Name}' (atualizado por outra venda simultânea).");

        // Mantém o objeto local sincronizado para o SaveChanges final
        product.StockQuantity -= request.Quantity;
        return (product.Name, product.PriceInCents);
    }

    private static ComandaDto MapToDto(Comanda comanda) => new()
    {
        Id                  = comanda.Id,
        UserId              = comanda.UserId,
        UserName            = comanda.User?.Name ?? string.Empty,
        TableIdentifier     = comanda.TableIdentifier,
        Status              = comanda.Status.ToString(),
        TotalInReais        = comanda.TotalInReais,
        PointsApplied       = comanda.PointsApplied,
        OpenedAt            = comanda.OpenedAt,
        ClosedAt            = comanda.ClosedAt,
        PaymentMethod       = comanda.PaymentMethod,
        UserPointsBalance   = comanda.User?.PointsBalance  ?? 0,
        UserBalanceInCents  = comanda.User?.BalanceInCents ?? 0,
        Items               = comanda.Items.Select(i => new ComandaItemDto
        {
            Id               = i.Id,
            ItemNameSnapshot = i.ItemNameSnapshot,
            Quantity         = i.Quantity,
            UnitPriceInReais = i.UnitPriceInCents / 100m,
            SubtotalInReais  = i.SubtotalInReais,
            AddedAt          = i.AddedAt,
        }).ToList(),
    };
}
