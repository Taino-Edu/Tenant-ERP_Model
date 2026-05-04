// =============================================================================
// ComandaService.cs — Implementação do serviço de Comandas
// TODO: Expandir na Fase 1.B com lógica de negócio completa
// =============================================================================
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class ComandaService : IComandaService
{
    private readonly AppDbContext            _db;
    private readonly ILogger<ComandaService> _logger;

    public ComandaService(AppDbContext db, ILogger<ComandaService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<ComandaDto> OpenComandaAsync(Guid userId, string? tableIdentifier = null)
    {
        // Verifica se já existe comanda aberta para este usuário
        // CORREÇÃO: Include(User) necessário para MapToDtoAsync retornar UserName corretamente
        var existing = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento));

        if (existing != null)
            return await MapToDtoAsync(existing);

        var comanda = new Comanda
        {
            UserId          = userId,
            TableIdentifier = tableIdentifier,
            Status          = ComandaStatus.Aberta
        };

        _db.Comandas.Add(comanda);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Comanda {Id} aberta para usuário {UserId}", comanda.Id, userId);
        return await MapToDtoAsync(comanda);
    }

    public async Task<ComandaDto?> GetActiveComandaAsync(Guid userId)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento));

        return comanda == null ? null : await MapToDtoAsync(comanda);
    }

    public async Task<Guid?> GetActiveComandaIdByUserAsync(Guid userId)
    {
        return await _db.Comandas
            .Where(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento))
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();
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

        // -----------------------------------------------------------------------
        // SEGURANÇA: se ProductId informado, busca nome e preço REAIS do banco.
        // Nunca confiar no preço enviado pelo cliente.
        // -----------------------------------------------------------------------
        string itemName     = request.ItemName;
        int    priceInCents = request.UnitPriceInCents;

        if (request.ProductId.HasValue)
        {
            var product = await _db.Products.FindAsync(request.ProductId.Value)
                ?? throw new InvalidOperationException("Produto não encontrado.");

            if (!product.IsActive)
                throw new InvalidOperationException("Produto inativo e não pode ser adicionado.");

            if (product.StockQuantity < request.Quantity)
                throw new InvalidOperationException(
                    $"Estoque insuficiente para '{product.Name}'. Disponível: {product.StockQuantity} un.");

            // Usa o nome e preço do banco — ignora o que veio do cliente
            itemName     = product.Name;
            priceInCents = product.PriceInCents;

            // Decrementa o estoque imediatamente
            product.StockQuantity -= request.Quantity;
        }
        else if (string.IsNullOrWhiteSpace(itemName))
        {
            throw new ArgumentException("Nome do item é obrigatório para itens sem produto cadastrado.");
        }

        var item = new ComandaItem
        {
            ComandaId          = comanda.Id,
            ProductId          = request.ProductId,
            CardCacheId        = request.CardCacheId,
            ItemNameSnapshot   = itemName,
            UnitPriceInCents   = priceInCents,
            Quantity           = request.Quantity,
            SubtotalInCents    = priceInCents * request.Quantity,
            AddedByUserId      = userId
        };

        comanda.Items.Add(item);
        comanda.Status        = ComandaStatus.EmAndamento;
        comanda.TotalInCents += item.SubtotalInCents;

        await _db.SaveChangesAsync();
        return await MapToDtoAsync(comanda);
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

        // Admin: busca nome e preço do banco se ProductId informado
        string itemName     = request.ItemName;
        int    priceInCents = request.UnitPriceInCents;

        if (request.ProductId.HasValue)
        {
            var product = await _db.Products.FindAsync(request.ProductId.Value)
                ?? throw new InvalidOperationException("Produto não encontrado.");

            if (!product.IsActive)
                throw new InvalidOperationException($"Produto '{product.Name}' está inativo.");

            if (product.StockQuantity < request.Quantity)
                throw new InvalidOperationException(
                    $"Estoque insuficiente para '{product.Name}'. Disponível: {product.StockQuantity} un.");

            itemName     = product.Name;
            priceInCents = product.PriceInCents;

            // Decrementa o estoque imediatamente
            product.StockQuantity -= request.Quantity;
        }
        else if (string.IsNullOrWhiteSpace(itemName))
        {
            throw new ArgumentException("Nome do item é obrigatório para itens sem produto cadastrado.");
        }

        var item = new ComandaItem
        {
            ComandaId          = comanda.Id,
            ProductId          = request.ProductId,
            CardCacheId        = request.CardCacheId,
            ItemNameSnapshot   = itemName,
            UnitPriceInCents   = priceInCents,
            Quantity           = request.Quantity,
            SubtotalInCents    = priceInCents * request.Quantity,
            AddedByUserId      = adminId
        };

        comanda.Items.Add(item);
        comanda.Status        = ComandaStatus.EmAndamento;
        comanda.TotalInCents += item.SubtotalInCents;

        await _db.SaveChangesAsync();
        return await MapToDtoAsync(comanda);
    }

    public async Task<ComandaDto> RemoveItemAsync(Guid comandaId, Guid itemId, Guid requestingUserId)
    {
        var item = await _db.ComandaItems.FindAsync(itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");

        // SEGURANÇA: garante que o item pertence à comanda informada.
        // Evita que um usuário mal-intencionado delete itens de outras comandas
        // passando um itemId válido com um comandaId diferente.
        if (item.ComandaId != comandaId)
            throw new InvalidOperationException("Item não pertence a esta comanda.");

        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comandaId);

        comanda.TotalInCents = Math.Max(0, comanda.TotalInCents - item.SubtotalInCents);

        // Restaura o estoque do produto quando item é removido da comanda
        if (item.ProductId.HasValue)
        {
            var product = await _db.Products.FindAsync(item.ProductId.Value);
            if (product != null)
                product.StockQuantity += item.Quantity;
        }

        _db.ComandaItems.Remove(item);

        // Se não restar nenhum outro item, volta para status Aberta
        var outrosItens = comanda.Items.Count(i => i.Id != itemId);
        if (outrosItens == 0)
            comanda.Status = ComandaStatus.Aberta;

        await _db.SaveChangesAsync();
        return await MapToDtoAsync(comanda);
    }

    public async Task<ComandaDto> CloseComandaAsync(Guid comandaId, Guid adminId)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comandaId)
            ?? throw new InvalidOperationException($"Comanda {comandaId} não encontrada.");

        comanda.Status   = ComandaStatus.Fechada;
        comanda.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await MapToDtoAsync(comanda);
    }

    public async Task<ComandaDto> CancelComandaAsync(Guid comandaId, Guid adminId)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comandaId)
            ?? throw new InvalidOperationException($"Comanda {comandaId} não encontrada.");

        // Restaura o estoque de todos os itens da comanda cancelada
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
        return await MapToDtoAsync(comanda);
    }

    public async Task<IEnumerable<ComandaDto>> GetActiveCommandasForDashboardAsync()
    {
        var comandas = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .Where(c => c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento)
            .OrderByDescending(c => c.OpenedAt)
            .ToListAsync();

        var dtos = new List<ComandaDto>();
        foreach (var c in comandas)
            dtos.Add(await MapToDtoAsync(c));
        return dtos;
    }

    // =========================================================================
    // VENDA AVULSA — venda direta no balcão, sem login de cliente
    // =========================================================================
    public async Task<ComandaDto> RegisterVendaAvulsaAsync(VendaAvulsaRequest request, Guid adminId)
    {
        if (request.Items == null || request.Items.Count == 0)
            throw new ArgumentException("Informe pelo menos um item para registrar a venda.");

        // Cria um usuário temporário para representar o cliente do balcão.
        // Evita quebrar a FK obrigatória de Comanda → User.
        var clientName = string.IsNullOrWhiteSpace(request.ClientName)
            ? "Cliente Balcão"
            : request.ClientName.Trim();

        var guestUser = new CardGameStore.Models.PostgreSQL.User
        {
            Name     = clientName,
            Role     = CardGameStore.Models.PostgreSQL.UserRole.Customer,
            IsActive = true
        };
        _db.Users.Add(guestUser);
        await _db.SaveChangesAsync();

        // Cria a comanda de balcão
        var comanda = new CardGameStore.Models.PostgreSQL.Comanda
        {
            UserId          = guestUser.Id,
            TableIdentifier = "Balcão",
            Status          = CardGameStore.Models.PostgreSQL.ComandaStatus.Aberta
        };
        _db.Comandas.Add(comanda);
        await _db.SaveChangesAsync();

        // Adiciona cada item validando estoque
        foreach (var reqItem in request.Items)
        {
            var product = await _db.Products.FindAsync(reqItem.ProductId)
                ?? throw new InvalidOperationException($"Produto '{reqItem.ProductId}' não encontrado.");

            if (!product.IsActive)
                throw new InvalidOperationException($"Produto '{product.Name}' está inativo.");

            if (product.StockQuantity < reqItem.Quantity)
                throw new InvalidOperationException(
                    $"Estoque insuficiente para '{product.Name}'. Disponível: {product.StockQuantity} un., solicitado: {reqItem.Quantity}.");

            // Decrementa o estoque imediatamente
            product.StockQuantity -= reqItem.Quantity;

            var item = new CardGameStore.Models.PostgreSQL.ComandaItem
            {
                ComandaId        = comanda.Id,
                ProductId        = product.Id,
                ItemNameSnapshot = product.Name,
                UnitPriceInCents = product.PriceInCents,
                Quantity         = reqItem.Quantity,
                SubtotalInCents  = product.PriceInCents * reqItem.Quantity,
                AddedByUserId    = adminId
            };

            comanda.TotalInCents += item.SubtotalInCents;
            _db.ComandaItems.Add(item);
        }

        // Fecha a comanda imediatamente (venda concluída no ato)
        comanda.Status   = CardGameStore.Models.PostgreSQL.ComandaStatus.Fechada;
        comanda.ClosedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "Venda avulsa registrada pelo admin {AdminId}: {Count} itens, total R$ {Total:F2}",
            adminId, request.Items.Count, comanda.TotalInCents / 100m);

        // Recarrega com relacionamentos para o DTO
        var loaded = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comanda.Id);

        return await MapToDtoAsync(loaded);
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

        // Valida e deduz o saldo do usuário
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        if (user.PointsExpiresAt.HasValue && user.PointsExpiresAt.Value < DateTime.UtcNow)
            throw new InvalidOperationException("Seus pontos estão expirados.");

        if (user.PointsBalance < points)
            throw new InvalidOperationException(
                $"Saldo insuficiente. Você tem {user.PointsBalance} pontos.");

        // Aplica — máximo = total da comanda (não pode ficar negativo)
        var pontosAplicados = Math.Min(points, comanda.TotalInCents);

        user.PointsBalance      -= pontosAplicados;
        user.UpdatedAt           = DateTime.UtcNow;
        comanda.PointsApplied    = pontosAplicados;

        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "Usuário {UserId} aplicou {Points} pontos na comanda {ComandaId}.",
            userId, pontosAplicados, comandaId);

        return await MapToDtoAsync(comanda);
    }

    // Helper de mapeamento Model → DTO
    private Task<ComandaDto> MapToDtoAsync(Comanda comanda)
    {
        var dto = new ComandaDto
        {
            Id              = comanda.Id,
            UserId          = comanda.UserId,
            UserName        = comanda.User?.Name ?? string.Empty,
            TableIdentifier = comanda.TableIdentifier,
            Status          = comanda.Status.ToString(),
            TotalInReais    = comanda.TotalInReais,
            PointsApplied   = comanda.PointsApplied,
            OpenedAt        = comanda.OpenedAt,
            Items           = comanda.Items.Select(i => new ComandaItemDto
            {
                Id               = i.Id,
                ItemNameSnapshot = i.ItemNameSnapshot,
                Quantity         = i.Quantity,
                UnitPriceInReais = i.UnitPriceInCents / 100m,
                SubtotalInReais  = i.SubtotalInReais,
                AddedAt          = i.AddedAt
            }).ToList()
        };
        return Task.FromResult(dto);
    }
}
