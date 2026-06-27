// =============================================================================
// DeckController.cs — CRUD de Decks de Cartas (por usuário autenticado)
// GET    /api/deck              → meus decks
// GET    /api/deck/{id}         → detalhe de um deck
// POST   /api/deck              → criar deck
// PUT    /api/deck/{id}         → atualizar deck
// DELETE /api/deck/{id}         → excluir deck
// GET    /api/deck/public/{userId} → decks públicos de outro usuário (Admin)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class DeckController : ControllerBase
{
    private readonly AppDbContext _db;

    public DeckController(AppDbContext db) => _db = db;

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token inválido.");
        return id;
    }

    // ── Listagem dos meus decks ───────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetMyDecks([FromQuery] string? game = null)
    {
        var userId = GetUserId();
        var query  = _db.Decks.Where(d => d.UserId == userId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(game))
            query = query.Where(d => d.Game.ToLower() == game.ToLower());

        var decks = await query.OrderByDescending(d => d.UpdatedAt)
            .Select(d => new DeckListDto
            {
                Id        = d.Id,
                Name      = d.Name,
                Game      = d.Game,
                Format    = d.Format,
                IsPublic  = d.IsPublic,
                CardCount = 0,           // calculado abaixo
                UpdatedAt = d.UpdatedAt,
            }).ToListAsync();

        // Conta cartas a partir do JSON (evita deserializar tudo no banco)
        foreach (var deck in decks)
        {
            var full = await _db.Decks.FindAsync(deck.Id);
            deck.CardCount = CountCards(full?.CardsJson);
        }

        return Ok(decks);
    }

    // ── Detalhe de um deck ────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetUserId();
        var deck   = await _db.Decks.FindAsync(id);

        if (deck == null) return NotFound(new { Message = "Deck não encontrado." });

        // Acesso: dono sempre pode; público qualquer um; Admin vê todos
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Operator");
        if (deck.UserId != userId && !deck.IsPublic && !isAdmin)
            return Forbid();

        return Ok(ToDeckDto(deck));
    }

    // ── Criar deck ────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveDeckRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();
        var deck   = new Deck
        {
            UserId    = userId,
            Name      = request.Name,
            Game      = request.Game,
            Format    = request.Format ?? "Standard",
            CardsJson = request.CardsJson,
            IsPublic  = request.IsPublic,
        };
        _db.Decks.Add(deck);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = deck.Id }, ToDeckDto(deck));
    }

    // ── Atualizar deck ────────────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveDeckRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();
        var deck   = await _db.Decks.FindAsync(id);

        if (deck == null) return NotFound(new { Message = "Deck não encontrado." });
        if (deck.UserId != userId) return Forbid();

        deck.Name      = request.Name;
        deck.Game      = request.Game;
        deck.Format    = request.Format ?? deck.Format;
        deck.CardsJson = request.CardsJson;
        deck.IsPublic  = request.IsPublic;
        deck.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToDeckDto(deck));
    }

    // ── Excluir deck ──────────────────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var deck   = await _db.Decks.FindAsync(id);

        if (deck == null) return NotFound(new { Message = "Deck não encontrado." });
        if (deck.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        _db.Decks.Remove(deck);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Decks públicos de um usuário (Admin para ver decks de jogadores) ──────

    [HttpGet("user/{userId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetByUser(Guid userId)
    {
        var decks = await _db.Decks
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync();

        return Ok(decks.Select(ToDeckDto));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int CountCards(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Sum(e => e.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number
                    ? q.GetInt32() : 1);
        }
        catch { return 0; }
    }

    private static DeckDto ToDeckDto(Deck d) => new()
    {
        Id        = d.Id,
        UserId    = d.UserId,
        Name      = d.Name,
        Game      = d.Game,
        Format    = d.Format,
        CardsJson = d.CardsJson,
        IsPublic  = d.IsPublic,
        CardCount = CountCards(d.CardsJson),
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
    };
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class SaveDeckRequest
{
    [Required, MaxLength(100)]
    public string  Name      { get; init; } = string.Empty;

    [Required, MaxLength(50)]
    public string  Game      { get; init; } = "Pokemon";

    [MaxLength(20)]
    public string? Format    { get; init; }

    [Required]
    public string  CardsJson { get; init; } = "[]";

    public bool    IsPublic  { get; init; } = false;
}

public class DeckDto
{
    public Guid     Id        { get; init; }
    public Guid     UserId    { get; init; }
    public string   Name      { get; init; } = string.Empty;
    public string   Game      { get; init; } = string.Empty;
    public string   Format    { get; init; } = string.Empty;
    public string   CardsJson { get; init; } = "[]";
    public bool     IsPublic  { get; init; }
    public int      CardCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public class DeckListDto
{
    public Guid     Id        { get; init; }
    public string   Name      { get; init; } = string.Empty;
    public string   Game      { get; init; } = string.Empty;
    public string   Format    { get; init; } = string.Empty;
    public bool     IsPublic  { get; init; }
    public int      CardCount { get; set; }
    public DateTime UpdatedAt { get; init; }
}
