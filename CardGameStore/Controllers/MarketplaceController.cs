// =============================================================================
// MarketplaceController.cs — Marketplace de cartas entre usuários (C2C)
//
// GET    /api/marketplace                → lista cartas disponíveis (público)
// GET    /api/marketplace/mine           → minhas listagens (auth)
// POST   /api/marketplace                → criar listagem — só Admin/Operator (é vitrine do Maikon, não C2C)
// PUT    /api/marketplace/{id}           → editar listagem própria ou qualquer uma (admin) (auth)
// DELETE /api/marketplace/{id}           → remover listagem própria ou qualquer uma (admin) (auth)
// POST   /api/marketplace/{id}/interest  → toggle interesse (auth)
// GET    /api/marketplace/{id}/interests → lista interessados — só dono/admin (auth)
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MarketplaceController : ControllerBase
{
    private readonly AppDbContext _db;

    public MarketplaceController(AppDbContext db) => _db = db;

    private Guid? TryGetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            return null;
        return id;
    }

    private Guid GetUserId()
    {
        var id = TryGetUserId();
        if (id is null) throw new UnauthorizedAccessException("Token inválido.");
        return id.Value;
    }

    // ── GET /api/marketplace — lista pública com paginação ───────────────────

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
        [FromQuery] int    page     = 1,
        [FromQuery] int    pageSize = 20,
        [FromQuery] string? game   = null,
        [FromQuery] string? search = null)
    {
        if (page     < 1) page     = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var currentUserId = TryGetUserId();

        var query = _db.CardListings
            .Include(l => l.User)
            .Include(l => l.Interests)
            .Where(l => l.Status == "Available")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(game))
            query = query.Where(l => l.CardGame != null && l.CardGame.ToLower() == game.ToLower());

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(l => l.CardName.ToLower().Contains(search.ToLower()));

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(l => ToDto(l, currentUserId, includeInterests: false)).ToList();

        return Ok(new { items = dtos, totalCount, totalPages });
    }

    // ── GET /api/marketplace/mine — minhas listagens ─────────────────────────

    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetUserId();

        var listings = await _db.CardListings
            .Include(l => l.User)
            .Include(l => l.Interests)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return Ok(listings.Select(l => ToDto(l, userId, includeInterests: true)));
    }

    // ── POST /api/marketplace — criar listagem (só Admin/Operator) ───────────
    // Marketplace é vitrine do Maikon, não C2C entre clientes — clientes só
    // navegam e marcam interesse (ToggleInterest), nunca criam anúncio.

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateListingRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();

        var listing = new CardListing
        {
            UserId       = userId,
            CardName     = req.CardName,
            CardGame     = req.CardGame,
            CardImageUrl = req.CardImageUrl,
            PriceInCents = req.PriceInCents,
            Condition    = req.Condition ?? "NM",
            Description  = req.Description,
            Status       = "Available",
        };

        _db.CardListings.Add(listing);
        await _db.SaveChangesAsync();

        // Recarrega com navegação
        await _db.Entry(listing).Reference(l => l.User).LoadAsync();

        return CreatedAtAction(nameof(GetAll), new { }, ToDto(listing, userId, includeInterests: false));
    }

    // ── PUT /api/marketplace/{id} — editar listagem própria ─────────────────

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateListingRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId  = GetUserId();
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Operator");

        var listing = await _db.CardListings
            .Include(l => l.User)
            .Include(l => l.Interests)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (listing is null)        return NotFound(new { Message = "Listagem não encontrada." });
        if (listing.UserId != userId && !isAdmin) return Forbid();

        listing.CardName     = req.CardName     ?? listing.CardName;
        listing.CardGame     = req.CardGame     ?? listing.CardGame;
        listing.CardImageUrl = req.CardImageUrl ?? listing.CardImageUrl;
        listing.PriceInCents = req.PriceInCents ?? listing.PriceInCents;
        listing.Condition    = req.Condition    ?? listing.Condition;
        listing.Description  = req.Description  ?? listing.Description;
        listing.Status       = req.Status       ?? listing.Status;
        listing.UpdatedAt    = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToDto(listing, userId, includeInterests: isAdmin || listing.UserId == userId));
    }

    // ── DELETE /api/marketplace/{id} — remover listagem ─────────────────────

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId  = GetUserId();
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Operator");

        var listing = await _db.CardListings.FindAsync(id);

        if (listing is null)                              return NotFound(new { Message = "Listagem não encontrada." });
        if (listing.UserId != userId && !isAdmin)         return Forbid();

        _db.CardListings.Remove(listing);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── POST /api/marketplace/{id}/interest — toggle interesse ───────────────

    [HttpPost("{id:guid}/interest")]
    [Authorize]
    public async Task<IActionResult> ToggleInterest(Guid id, [FromBody] InterestRequest? req)
    {
        var userId = GetUserId();

        var listing = await _db.CardListings
            .Include(l => l.Interests)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (listing is null) return NotFound(new { Message = "Listagem não encontrada." });
        if (listing.Status != "Available")
            return BadRequest(new { Message = "Esta carta não está mais disponível." });

        var existing = listing.Interests.FirstOrDefault(i => i.UserId == userId);
        bool interested;

        if (existing is not null)
        {
            // Desmarca interesse
            _db.ListingInterests.Remove(existing);
            listing.Interests.Remove(existing);
            interested = false;
        }
        else
        {
            // Marca interesse
            var interest = new ListingInterest
            {
                ListingId     = id,
                UserId        = userId,
                Message       = req?.Message,
                ShareContact  = req?.ShareContact ?? false,
            };
            _db.ListingInterests.Add(interest);
            listing.Interests.Add(interest);
            interested = true;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            interested,
            interestCount = listing.Interests.Count,
        });
    }

    // ── GET /api/marketplace/{id}/interests — lista interessados ─────────────

    [HttpGet("{id:guid}/interests")]
    [Authorize]
    public async Task<IActionResult> GetInterests(Guid id)
    {
        var userId  = GetUserId();
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Operator");

        var listing = await _db.CardListings
            .Include(l => l.Interests).ThenInclude(i => i.User)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (listing is null) return NotFound(new { Message = "Listagem não encontrada." });
        if (listing.UserId != userId && !isAdmin) return Forbid();

        var interests = listing.Interests.Select(i => new
        {
            i.Id,
            i.UserId,
            userName         = i.User?.Name,
            userProfileImage = i.User?.ProfileImageUrl,
            // WhatsApp só exposto se o comprador deu consentimento explícito (LGPD art. 7)
            userWhatsApp     = i.ShareContact ? i.User?.WhatsApp : null,
            i.ShareContact,
            i.Message,
            i.CreatedAt,
        });

        return Ok(interests);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CardListingDto ToDto(CardListing l, Guid? currentUserId, bool includeInterests)
    {
        var interestCount = l.Interests?.Count ?? 0;
        var myInterest    = currentUserId.HasValue && (l.Interests?.Any(i => i.UserId == currentUserId.Value) ?? false);

        return new CardListingDto
        {
            Id             = l.Id,
            CardName       = l.CardName,
            CardGame       = l.CardGame,
            CardImageUrl   = l.CardImageUrl,
            PriceInCents   = l.PriceInCents,
            PriceInReais   = l.PriceInCents / 100m,
            Condition      = l.Condition,
            Description    = l.Description,
            Status         = l.Status,
            CreatedAt      = l.CreatedAt,
            SellerId       = l.UserId,
            SellerName     = l.User?.Name,
            SellerImageUrl = l.User?.ProfileImageUrl,
            InterestCount  = interestCount,
            MyInterest     = myInterest,
            Interests      = includeInterests
                ? l.Interests?.Select(i => new InterestDto
                  {
                      Id              = i.Id,
                      UserId          = i.UserId,
                      UserName        = i.User?.Name,
                      UserProfileImage = i.User?.ProfileImageUrl,
                      Message         = i.Message,
                      CreatedAt       = i.CreatedAt,
                  }).ToList()
                : null,
        };
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class CardListingDto
{
    public Guid        Id             { get; init; }
    public string      CardName       { get; init; } = "";
    public string?     CardGame       { get; init; }
    public string?     CardImageUrl   { get; init; }
    public int         PriceInCents   { get; init; }
    public decimal     PriceInReais   { get; init; }
    public string      Condition      { get; init; } = "NM";
    public string?     Description    { get; init; }
    public string      Status         { get; init; } = "Available";
    public DateTime    CreatedAt      { get; init; }
    public Guid        SellerId       { get; init; }
    public string?     SellerName     { get; init; }
    public string?     SellerImageUrl { get; init; }
    public int         InterestCount  { get; init; }
    public bool        MyInterest     { get; init; }
    public List<InterestDto>? Interests { get; init; }
}

public class InterestDto
{
    public Guid     Id               { get; init; }
    public Guid     UserId           { get; init; }
    public string?  UserName         { get; init; }
    public string?  UserProfileImage { get; init; }
    public string?  Message          { get; init; }
    public DateTime CreatedAt        { get; init; }
}

public class CreateListingRequest
{
    [Required, MaxLength(200)]
    public string  CardName     { get; init; } = "";

    [MaxLength(100)]
    public string? CardGame     { get; init; }

    [MaxLength(500)]
    public string? CardImageUrl { get; init; }

    public int     PriceInCents { get; init; }

    [MaxLength(50)]
    public string? Condition    { get; init; }

    [MaxLength(1000)]
    public string? Description  { get; init; }
}

public class UpdateListingRequest
{
    [MaxLength(200)]
    public string? CardName     { get; init; }

    [MaxLength(100)]
    public string? CardGame     { get; init; }

    [MaxLength(500)]
    public string? CardImageUrl { get; init; }

    public int?    PriceInCents { get; init; }

    [MaxLength(50)]
    public string? Condition    { get; init; }

    [MaxLength(1000)]
    public string? Description  { get; init; }

    [MaxLength(20)]
    public string? Status       { get; init; }
}

public class InterestRequest
{
    [MaxLength(500)]
    public string? Message { get; init; }

    /// <summary>
    /// Consentimento explícito do comprador para expor o WhatsApp ao vendedor (LGPD art. 7, I).
    /// Sem este campo como true, o número nunca é retornado na API.
    /// </summary>
    public bool ShareContact { get; init; } = false;
}
