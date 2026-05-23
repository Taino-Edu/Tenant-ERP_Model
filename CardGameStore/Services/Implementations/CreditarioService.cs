// =============================================================================
// CreditarioService.cs — Implementação do serviço de Crediário
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Services.Implementations;

public class CreditarioService : ICreditarioService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreditarioService> _logger;

    public CreditarioService(AppDbContext db, ILogger<CreditarioService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Cria um novo crediário quando o Admin fecha uma comanda com pagamento em crediário.
    /// Valida se o cliente já tem um crediário aberto.
    /// </summary>
    public async Task<CrediariosDto> CreateAsync(Guid comandaId, Guid userId, int valorEmCentavos, Guid adminId)
    {
        // Valida se o usuário já tem um crediário aberto
        var jaTemAberto = await HasOpenAsync(userId);
        if (jaTemAberto)
            throw new InvalidOperationException(
                "Este cliente já possui um crediário em aberto. Quite o anterior antes de criar um novo.");

        // Valida se a comanda existe
        var comanda = await _db.Comandas.FindAsync(comandaId);
        if (comanda == null)
            throw new InvalidOperationException($"Comanda {comandaId} não encontrada.");

        var agora = DateTime.UtcNow;
        var vencimento = agora.AddDays(30);

        var crediario = new Crediario
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ComandaId = comandaId,
            ValorEmCentavos = valorEmCentavos,
            DataAbertura = agora,
            DataVencimento = vencimento,
            Status = CrediariosStatus.Aberto,
            AbertoPorAdminId = adminId,
        };

        _db.Crediarios.Add(crediario);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Crediário {CredId} criado para usuário {UserId} — R$ {Valor:N2}, vence em {Venc:dd/MM/yyyy}",
            crediario.Id, userId, crediario.ValorEmReais, vencimento);

        return MapToDto(crediario);
    }

    /// <summary>
    /// Retorna todos os crediários de um usuário (abertos e pagos).
    /// </summary>
    public async Task<List<CrediariosDto>> GetByUserAsync(Guid userId)
    {
        var crediarios = await _db.Crediarios
            .Include(c => c.User)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        return crediarios.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Retorna TODOS os crediários (abertos e pagos).
    /// </summary>
    public async Task<List<CrediariosDto>> GetAllAsync()
    {
        var crediarios = await _db.Crediarios
            .Include(c => c.User)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        return crediarios.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Retorna todos os crediários abertos (não pagos).
    /// Útil para dashboard do admin.
    /// </summary>
    public async Task<List<CrediariosDto>> GetAbertoAsync()
    {
        var crediarios = await _db.Crediarios
            .Include(c => c.User)
            .Where(c => c.Status == CrediariosStatus.Aberto)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        return crediarios.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Retorna todos os crediários vencidos (abertos e além da data de vencimento).
    /// </summary>
    public async Task<List<CrediariosDto>> GetVencidosAsync()
    {
        var agora = DateTime.UtcNow;
        var crediarios = await _db.Crediarios
            .Include(c => c.User)
            .Where(c => c.Status == CrediariosStatus.Aberto && c.DataVencimento < agora)
            .OrderByDescending(c => c.DataVencimento)
            .ToListAsync();

        return crediarios.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Marca um crediário como pago.
    /// Usa o token do admin para rastrear quem pagou.
    /// </summary>
    public async Task<CrediariosDto> MarkAsPaidAsync(Guid creditarioId, Guid adminId, string? observacao = null)
    {
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == creditarioId)
            ?? throw new InvalidOperationException($"Crediário {creditarioId} não encontrado.");

        if (crediario.Status == CrediariosStatus.Pago)
            throw new InvalidOperationException("Este crediário já foi marcado como pago.");

        crediario.Status = CrediariosStatus.Pago;
        crediario.DataPagamento = DateTime.UtcNow;
        crediario.PagoPorAdminId = adminId;

        if (!string.IsNullOrWhiteSpace(observacao))
            crediario.Observacao = observacao;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Crediário {CredId} marcado como pago por admin {AdminId} — usuário {UserId} quitou R$ {Valor:N2}",
            crediario.Id, adminId, crediario.UserId, crediario.ValorEmReais);

        return MapToDto(crediario);
    }

    /// <summary>
    /// Retorna um crediário específico por ID.
    /// </summary>
    public async Task<CrediariosDto?> GetByIdAsync(Guid creditarioId)
    {
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == creditarioId);

        return crediario == null ? null : MapToDto(crediario);
    }

    /// <summary>
    /// Verifica se um usuário tem um crediário aberto (bloqueia nova comanda).
    /// </summary>
    public async Task<bool> HasOpenAsync(Guid userId)
    {
        return await _db.Crediarios
            .AnyAsync(c => c.UserId == userId && c.Status == CrediariosStatus.Aberto);
    }

    /// <summary>
    /// Retorna o crediário aberto de um usuário, ou null se não houver.
    /// </summary>
    public async Task<CrediariosDto?> GetOpenAsync(Guid userId)
    {
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == CrediariosStatus.Aberto);

        return crediario == null ? null : MapToDto(crediario);
    }

    /// <summary>
    /// Calcula o total devido por um usuário (todos os crediários abertos).
    /// </summary>
    public async Task<decimal> GetTotalDevidoAsync(Guid userId)
    {
        var totalEmCentavos = await _db.Crediarios
            .Where(c => c.UserId == userId && c.Status == CrediariosStatus.Aberto)
            .SumAsync(c => c.ValorEmCentavos);

        return totalEmCentavos / 100m;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Mapeia entidade Crediario para DTO.
    /// </summary>
    private static CrediariosDto MapToDto(Crediario crediario)
    {
        var agora = DateTime.UtcNow;
        var diasRestantes = (int)(crediario.DataVencimento - agora).TotalDays;

        return new CrediariosDto
        {
            Id = crediario.Id,
            UserId = crediario.UserId,
            UserName = crediario.User?.Name ?? string.Empty,
            UserEmail = crediario.User?.Email,
            ComandaId = crediario.ComandaId,
            ValorEmReais = crediario.ValorEmReais,
            DataAbertura = crediario.DataAbertura,
            DataVencimento = crediario.DataVencimento,
            DataPagamento = crediario.DataPagamento,
            Status = crediario.Status.ToString(),
            Observacao = crediario.Observacao,
            Vencido = crediario.Vencido,
            DiasRestantes = diasRestantes,
        };
    }
}
