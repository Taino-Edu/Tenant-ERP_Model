using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class ComandaService : IComandaService
{
    // Constante para evitar magic strings sensíveis a typo/case
    private const string PaymentCrediario = "Crediario";

    private readonly AppDbContext            _db;
    private readonly IEmailService           _email;
    private readonly ILogger<ComandaService> _logger;

    public ComandaService(AppDbContext db, IEmailService email, ILogger<ComandaService> logger)
    {
        _db     = db;
        _email  = email;
        _logger = logger;
    }

    public async Task<ComandaDto> OpenComandaAsync(Guid userId, string? tableIdentifier = null)
    {
        // Verifica se o cliente tem crediário em aberto — bloqueia abertura de nova comanda
        var crediarioAberto = await _db.Crediarios
            .AnyAsync(c => c.UserId == userId && c.Status == CrediariosStatus.Aberto);

        if (crediarioAberto)
            throw new InvalidOperationException(
                "Você possui um crediário em aberto. Procure o Maikon para quitar antes de abrir uma nova comanda.");

        var existing = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento));

        if (existing != null)
            return MapToDto(existing);

        var comanda = new Comanda
        {
            UserId          = userId,
            TableIdentifier = tableIdentifier,
            Status          = ComandaStatus.Aberta,
        };

        _db.Comandas.Add(comanda);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Comanda {Id} aberta para usuário {UserId}", comanda.Id, userId);
        return MapToDto(comanda);
    }

    public async Task<ComandaDto?> GetActiveComandaAsync(Guid userId)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento));

        return comanda == null ? null : MapToDto(comanda);
    }

    public async Task<Guid?> GetActiveComandaIdByUserAsync(Guid userId)
    {
        return await _db.Comandas
            .Where(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento))
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

        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento))
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
            var product = await _db.Products.FindAsync(item.ProductId.Value);
            if (product != null)
                product.StockQuantity += item.Quantity;
        }

        _db.ComandaItems.Remove(item);

        if (comanda.Items.Count(i => i.Id != itemId) == 0)
            comanda.Status = ComandaStatus.Aberta;

        await _db.SaveChangesAsync();
        return MapToDto(comanda);
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
            // Bloqueia se o cliente já tem um crediário aberto
            var jaTemCrediario = await _db.Crediarios
                .AnyAsync(c => c.UserId == comanda.UserId && c.Status == CrediariosStatus.Aberto);

            if (jaTemCrediario)
                throw new InvalidOperationException(
                    "Este cliente já possui um crediário em aberto. Quite o anterior antes de criar um novo.");

            var vencimento = DateTime.UtcNow.AddDays(30);

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

            // Envia email (não bloqueia o fluxo se falhar)
            if (!string.IsNullOrWhiteSpace(comanda.User?.Email))
                _ = _email.SendCrediarioAbertoAsync(
                    comanda.User.Email, comanda.User.Name, crediario.ValorEmReais, vencimento);
        }

        comanda.Status   = ComandaStatus.Fechada;
        comanda.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapToDto(comanda);
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
            var product = await _db.Products.FindAsync(item.ProductId!.Value);
            if (product != null)
                product.StockQuantity += item.Quantity;
        }

        comanda.Status   = ComandaStatus.Cancelada;
        comanda.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Comanda {Id} cancelada — estoque restaurado para {Count} produto(s).",
            comandaId, comanda.Items.Count(i => i.ProductId.HasValue));

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

    public async Task<IEnumerable<ComandaDto>> GetTodayHistoryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var comandas = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .Where(c => (c.Status == ComandaStatus.Fechada || c.Status == ComandaStatus.Cancelada)
                     && c.ClosedAt.HasValue && c.ClosedAt.Value.Date == today)
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
        Id              = comanda.Id,
        UserId          = comanda.UserId,
        UserName        = comanda.User?.Name ?? string.Empty,
        TableIdentifier = comanda.TableIdentifier,
        Status          = comanda.Status.ToString(),
        TotalInReais    = comanda.TotalInReais,
        PointsApplied   = comanda.PointsApplied,
        OpenedAt        = comanda.OpenedAt,
        ClosedAt        = comanda.ClosedAt,
        Items           = comanda.Items.Select(i => new ComandaItemDto
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
