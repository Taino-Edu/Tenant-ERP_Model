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
        var existing = await _db.Comandas
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
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(request));
        if (request.UnitPriceInCents < 0)
            throw new ArgumentException("Preço não pode ser negativo.", nameof(request));

        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId &&
                (c.Status == ComandaStatus.Aberta || c.Status == ComandaStatus.EmAndamento))
            ?? throw new InvalidOperationException("Comanda ativa não encontrada para este usuário.");

        var item = new ComandaItem
        {
            ComandaId          = comanda.Id,
            ProductId          = request.ProductId,
            CardCacheId        = request.CardCacheId,
            ItemNameSnapshot   = request.ItemName,
            UnitPriceInCents   = request.UnitPriceInCents,
            Quantity           = request.Quantity,
            SubtotalInCents    = request.UnitPriceInCents * request.Quantity,
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
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(request));
        if (request.UnitPriceInCents < 0)
            throw new ArgumentException("Preço não pode ser negativo.", nameof(request));

        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comandaId)
            ?? throw new InvalidOperationException($"Comanda {comandaId} não encontrada.");

        var item = new ComandaItem
        {
            ComandaId          = comanda.Id,
            ProductId          = request.ProductId,
            CardCacheId        = request.CardCacheId,
            ItemNameSnapshot   = request.ItemName,
            UnitPriceInCents   = request.UnitPriceInCents,
            Quantity           = request.Quantity,
            SubtotalInCents    = request.UnitPriceInCents * request.Quantity,
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

        var comanda = await _db.Comandas
            .Include(c => c.Items)
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comandaId);

        comanda.TotalInCents = Math.Max(0, comanda.TotalInCents - item.SubtotalInCents);
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

        comanda.Status   = ComandaStatus.Cancelada;
        comanda.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
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
