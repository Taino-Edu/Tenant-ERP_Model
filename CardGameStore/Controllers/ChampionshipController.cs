// =============================================================================
// ChampionshipController.cs — Endpoints REST de Campeonatos/Torneios
// GET  /api/championship              → lista campeonatos (Planejado/Inscrições)
// GET  /api/championship/{id}         → detalhes de um campeonato
// GET  /api/championship/{id}/participants → lista participantes
// POST /api/championship              → cria campeonato (Admin)
// POST /api/championship/{id}/register → inscreve usuário logado
// PUT  /api/championship/{id}/status  → muda status (Admin)
// PUT  /api/championship/{id}/participants/{pid}/placement → define colocação (Admin)
// =============================================================================

using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChampionshipController : ControllerBase
{
    private readonly IChampionshipService _service;
    private readonly ILogger<ChampionshipController> _logger;

    public ChampionshipController(IChampionshipService service, ILogger<ChampionshipController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    // -------------------------------------------------------------------------
    // LEITURA — acessível por qualquer pessoa autenticada (ou anônima para listagem)
    // -------------------------------------------------------------------------

    /// <summary>Lista campeonatos planejados e com inscrições abertas.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ChampionshipDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var list = await _service.GetUpcomingAsync();
        return Ok(list.Select(ToDto));
    }

    /// <summary>Busca um campeonato pelo ID com lista de participantes.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ChampionshipDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var ch = await _service.GetByIdAsync(id);
        return ch == null ? NotFound(new { Message = "Campeonato não encontrado." }) : Ok(ToDto(ch));
    }

    /// <summary>Lista os participantes inscritos em um campeonato.</summary>
    [HttpGet("{id:guid}/participants")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ParticipantDto>), 200)]
    public async Task<IActionResult> GetParticipants(Guid id)
    {
        var participants = await _service.GetParticipantsAsync(id);
        return Ok(participants.Select(p => new ParticipantDto
        {
            Id             = p.Id,
            UserId         = p.UserId,
            UserName       = p.User?.Name ?? string.Empty,
            PlayerNumber   = p.PlayerNumber,
            DeckName       = p.DeckName,
            Placement      = p.Placement,
            RegisteredAt   = p.RegisteredAt
        }));
    }

    // -------------------------------------------------------------------------
    // CRIAÇÃO — apenas Admin
    // -------------------------------------------------------------------------

    /// <summary>Cria um novo campeonato. Apenas Admin.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ChampionshipDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateChampionshipRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var adminId = GetUserId();

        var championship = new Championship
        {
            Name                 = request.Name,
            Description          = request.Description,
            Game                 = request.Game,
            StartDate            = request.StartDate,
            EndDate              = request.EndDate,
            RegistrationDeadline = request.RegistrationDeadline,
            MaxParticipants      = request.MaxParticipants,
            EntryFeeInCents      = request.EntryFeeInCents,
            Status               = ChampionshipStatus.Planejado,
            CreatedByAdminId     = adminId
        };

        var created = await _service.CreateAsync(championship);
        _logger.LogInformation("Campeonato {Id} criado pelo admin {AdminId}", created.Id, adminId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToDto(created));
    }

    // -------------------------------------------------------------------------
    // INSCRIÇÃO — qualquer usuário autenticado
    // -------------------------------------------------------------------------

    /// <summary>Inscreve o usuário autenticado em um campeonato.</summary>
    [HttpPost("{id:guid}/register")]
    [Authorize]
    [ProducesResponseType(typeof(ParticipantDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Register(Guid id, [FromBody] RegisterChampionshipRequest? request)
    {
        var userId = GetUserId();

        // Verifica se o campeonato existe e aceita inscrições
        var ch = await _service.GetByIdAsync(id);
        if (ch == null)
            return NotFound(new { Message = "Campeonato não encontrado." });

        if (ch.Status != ChampionshipStatus.Inscricoes)
            return BadRequest(new { Message = "As inscrições para este campeonato não estão abertas." });

        if (ch.MaxParticipants.HasValue && ch.Participants.Count >= ch.MaxParticipants.Value)
            return BadRequest(new { Message = "O campeonato atingiu o número máximo de participantes." });

        ChampionshipParticipant participant;
        try
        {
            participant = await _service.RegisterParticipantAsync(id, userId, request?.DeckName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        _logger.LogInformation("Usuário {UserId} inscrito no campeonato {ChampionshipId}", userId, id);

        return StatusCode(201, new ParticipantDto
        {
            Id           = participant.Id,
            UserId       = participant.UserId,
            UserName     = string.Empty,   // sem eager load aqui
            PlayerNumber = participant.PlayerNumber,
            DeckName     = participant.DeckName,
            RegisteredAt = participant.RegisteredAt
        });
    }

    // -------------------------------------------------------------------------
    // ATUALIZAÇÃO DE STATUS — apenas Admin
    // -------------------------------------------------------------------------

    /// <summary>Muda o status de um campeonato. Apenas Admin.</summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ChampionshipDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        if (!Enum.TryParse<ChampionshipStatus>(request.Status, ignoreCase: true, out var newStatus))
            return BadRequest(new { Message = $"Status inválido: '{request.Status}'. Valores válidos: Planejado, Inscricoes, EmAndamento, Finalizado, Cancelado" });

        try
        {
            var updated = await _service.UpdateStatusAsync(id, newStatus);
            _logger.LogInformation("Status do campeonato {Id} alterado para {Status}", id, newStatus);
            return Ok(ToDto(updated));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    // -------------------------------------------------------------------------
    // COLOCAÇÃO FINAL — apenas Admin
    // -------------------------------------------------------------------------

    /// <summary>Define a colocação final de um participante. Apenas Admin.</summary>
    [HttpPut("{id:guid}/participants/{participantId:guid}/placement")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SetPlacement(Guid id, Guid participantId, [FromBody] SetPlacementRequest request)
    {
        if (request.Placement <= 0)
            return BadRequest(new { Message = "A colocação deve ser maior que zero." });

        await _service.SetPlacementAsync(participantId, request.Placement);
        return Ok(new { Message = $"Colocação {request.Placement}º definida com sucesso." });
    }

    // -------------------------------------------------------------------------
    // Helpers privados
    // -------------------------------------------------------------------------

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }

    /// <summary>Mapeia Championship → ChampionshipDto (evita circular reference).</summary>
    private static ChampionshipDto ToDto(Championship ch) => new()
    {
        Id                   = ch.Id,
        Name                 = ch.Name,
        Description          = ch.Description,
        Game                 = ch.Game,
        StartDate            = ch.StartDate,
        EndDate              = ch.EndDate,
        RegistrationDeadline = ch.RegistrationDeadline,
        MaxParticipants      = ch.MaxParticipants,
        EntryFeeInCents      = ch.EntryFeeInCents,
        EntryFeeInReais      = ch.EntryFeeInCents / 100m,
        Status               = ch.Status.ToString(),
        ParticipantCount     = ch.Participants?.Count ?? 0,
        CreatedAt            = ch.CreatedAt
    };
}

// =============================================================================
// DTOs e Request Records — definidos no mesmo arquivo para simplicidade
// =============================================================================

/// <summary>DTO de resposta de campeonato (sem circular reference).</summary>
public class ChampionshipDto
{
    public Guid      Id                   { get; init; }
    public string    Name                 { get; init; } = string.Empty;
    public string?   Description          { get; init; }
    public string    Game                 { get; init; } = string.Empty;
    public DateTime  StartDate            { get; init; }
    public DateTime? EndDate              { get; init; }
    public DateTime? RegistrationDeadline { get; init; }
    public int?      MaxParticipants      { get; init; }
    public int       EntryFeeInCents      { get; init; }
    public decimal   EntryFeeInReais      { get; init; }
    public string    Status               { get; init; } = string.Empty;
    public int       ParticipantCount     { get; init; }
    public DateTime  CreatedAt            { get; init; }
}

/// <summary>DTO de participante em um campeonato.</summary>
public class ParticipantDto
{
    public Guid      Id           { get; init; }
    public Guid      UserId       { get; init; }
    public string    UserName     { get; init; } = string.Empty;
    public int       PlayerNumber { get; init; }
    public string?   DeckName     { get; init; }
    public int?      Placement    { get; init; }
    public DateTime  RegisteredAt { get; init; }
}

/// <summary>Request para criar campeonato.</summary>
public class CreateChampionshipRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string    Name                 { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string?   Description          { get; init; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string    Game                 { get; init; } = string.Empty;

    public DateTime  StartDate            { get; init; }
    public DateTime? EndDate              { get; init; }
    public DateTime? RegistrationDeadline { get; init; }
    public int?      MaxParticipants      { get; init; }
    public int       EntryFeeInCents      { get; init; }
}

/// <summary>Request para alterar status do campeonato.</summary>
public record UpdateStatusRequest(string Status);

/// <summary>Request para inscrição em campeonato (deck é opcional).</summary>
public record RegisterChampionshipRequest(string? DeckName);

/// <summary>Request para definir colocação de participante.</summary>
public record SetPlacementRequest(int Placement);
