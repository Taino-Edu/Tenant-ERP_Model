// =============================================================================
// UserController.cs — Endpoints de Usuários e Pontos
// GET    /api/user                  → lista clientes (Admin)
// POST   /api/user                  → Admin cria conta de cliente
// GET    /api/user/me               → perfil do usuário logado
// PUT    /api/user/me               → titular corrige seus dados (LGPD retificação)
// DELETE /api/user/me               → titular solicita exclusão/anonimização (LGPD Art. 18)
// GET    /api/user/{id}             → detalhe de um cliente (Admin)
// POST   /api/user/{id}/points      → adiciona pontos (Admin)
// POST   /api/user/{id}/balance     → ajusta saldo (Admin)
// PUT    /api/user/{id}/reset-password → Admin redefine senha do cliente
// PUT    /api/user/{id}/perfil         → Admin atribui/remove perfil de operador
// DELETE /api/user/{id}               → Admin exclui operador
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class UserController : ControllerBase
{
    private readonly IUserService        _service;
    private readonly IAuditService       _audit;
    private readonly AppDbContext        _db;
    private readonly IVendaAvulsaService _vendaService;

    public UserController(IUserService service, IAuditService audit, AppDbContext db, IVendaAvulsaService vendaService)
    {
        _service      = service;
        _audit        = audit;
        _db           = db;
        _vendaService = vendaService;
    }

    /// <summary>Lista todos os clientes ativos. Admin pode buscar por nome/CPF/WhatsApp.</summary>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<UserSummaryDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? role)
    {
        var users = await _service.GetAllAsync(search, role);
        return Ok(users);
    }

    /// <summary>Admin cria diretamente uma conta de cliente.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AdminCreate([FromBody] AdminCreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = GetUserId();
            var result  = await _service.AdminCreateUserAsync(request, adminId);
            await _audit.LogAsync("CriouCliente", "User", result.Id.ToString(), httpContext: HttpContext);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Perfil completo do usuário logado (pontos, dados pessoais).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMe()
    {
        var userId  = GetUserId();
        var profile = await _service.GetProfileAsync(userId);
        return profile == null ? NotFound() : Ok(profile);
    }

    /// <summary>
    /// Permite ao titular corrigir seus próprios dados pessoais.
    /// LGPD — Direito de retificação (Art. 18, IV).
    /// </summary>
    [HttpPut("me")]
    [Authorize(Policy = "CustomerOrAdmin")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetUserId();
            var result = await _service.UpdateMeAsync(userId, request);

            // Audit log — LGPD: retificação de dados pelo titular
            await _audit.LogAsync("Editou", "User", userId.ToString(), httpContext: HttpContext);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Anonimiza os dados do titular (exclusão lógica).
    /// O registro é mantido para preservar o histórico de comandas e crediários,
    /// mas todos os dados pessoais identificáveis são removidos.
    /// LGPD — Direito de exclusão (Art. 18, VI).
    /// </summary>
    [HttpDelete("me")]
    [Authorize(Policy = "CustomerOrAdmin")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteMe()
    {
        try
        {
            var userId = GetUserId();

            // Audit log ANTES da anonimização — depois o userId ainda existe no banco
            await _audit.LogAsync("Exclusao", "User", userId.ToString(),
                details: "{\"motivo\":\"SolicitacaoTitular\"}", httpContext: HttpContext);

            await _service.AnonimizarAsync(userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>Detalhes de um cliente específico (Admin).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _service.GetByIdAsync(id);
        if (user == null)
            return NotFound(new { Message = "Usuário não encontrado." });

        // Audit log — Admin visualizando dados pessoais de cliente (LGPD rastreabilidade)
        await _audit.LogAsync("Visualizou", "User", id.ToString(), httpContext: HttpContext);

        return Ok(user);
    }

    /// <summary>Adiciona pontos ao saldo de um cliente (Admin).</summary>
    [HttpPost("{id:guid}/points")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddPoints(Guid id, [FromBody] AddPointsRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = GetUserId();
            var result  = await _service.AddPointsAsync(id, request, adminId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Ajusta o saldo monetário de um cliente (Admin).
    /// Positivo = crédito (recarga), negativo = débito (uso).
    /// </summary>
    [HttpPost("{id:guid}/balance")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AdjustBalance(Guid id, [FromBody] AdjustBalanceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = GetUserId();
            var result  = await _service.AdjustBalanceAsync(id, request, adminId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Admin redefine a senha de um cliente (sem e-mail, imediato).</summary>
    [HttpPut("{id:guid}/reset-password")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AdminResetPassword(Guid id, [FromBody] AdminResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = GetUserId();
            await _service.AdminResetPasswordAsync(id, request, adminId);
            await _audit.LogAsync("RedefinirSenha", "User", id.ToString(), httpContext: HttpContext);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Histórico completo de um cliente: comandas, vendas avulsas, crediários e campeonatos.
    /// GET /api/user/{id}/historico
    /// </summary>
    [HttpGet("{id:guid}/historico")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ClienteHistoricoDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetHistorico(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(new { Message = "Usuário não encontrado." });

        // Comandas
        var comandas = await _db.Comandas
            .Include(c => c.Items)
            .Where(c => c.UserId == id)
            .OrderByDescending(c => c.OpenedAt)
            .ToListAsync();

        // Crediários
        var crediarios = await _db.Crediarios
            .Where(c => c.UserId == id)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        // Campeonatos
        var campeonatos = await _db.ChampionshipParticipants
            .Include(p => p.Championship)
            .Where(p => p.UserId == id)
            .OrderByDescending(p => p.Championship.StartDate)
            .ToListAsync();

        // Vendas avulsas (apenas as que têm UserId — vendas com cliente identificado a partir de agora)
        var vendasAvulsas = await _vendaService.GetByUserAsync(id);

        // Estatísticas de visitas (comandas fechadas = visita à loja)
        var visitasClosed = comandas.Where(c => c.Status == ComandaStatus.Fechada).ToList();
        var totalGasto    = visitasClosed.Sum(c => c.TotalInCents) / 100m
                          + vendasAvulsas.Sum(v => v.TotalInReais);

        var historico = new ClienteHistoricoDto
        {
            UserId       = user.Id,
            UserName     = user.Name,
            TotalVisitas  = visitasClosed.Count,
            TotalGasto    = totalGasto,
            PrimeiraVisita = visitasClosed.MinBy(c => c.ClosedAt)?.ClosedAt,
            UltimaVisita   = visitasClosed.MaxBy(c => c.ClosedAt)?.ClosedAt,

            Comandas = comandas.Select(c => new ComandaHistoricoDto
            {
                Id              = c.Id,
                Status          = c.Status.ToString(),
                TotalInReais    = c.TotalInCents / 100m,
                PaymentMethod   = c.PaymentMethod,
                SecondPaymentMethod = c.SecondPaymentMethod,
                OpenedAt        = c.OpenedAt,
                ClosedAt        = c.ClosedAt,
                TableIdentifier = c.TableIdentifier,
                Items           = c.Items.Select(i => new ComandaItemHistoricoDto
                {
                    ItemName         = i.ItemNameSnapshot,
                    Quantity         = i.Quantity,
                    UnitPriceInReais = i.UnitPriceInCents / 100m,
                    SubtotalInReais  = i.SubtotalInCents / 100m,
                }).ToList(),
            }).ToList(),

            VendasAvulsas = vendasAvulsas.Select(v => new VendaAvulsaHistoricoDto
            {
                Id           = v.Id,
                TotalInReais = v.TotalInReais,
                PaymentMethod = v.PaymentMethod,
                SoldAt       = v.SoldAt,
                Items        = v.Items.Select(i => new VendaAvulsaItemHistoricoDto
                {
                    ProductName      = i.ProductName,
                    Quantity         = i.Quantity,
                    UnitPriceInReais = i.UnitPriceInReais,
                    SubtotalInReais  = i.SubtotalInReais,
                }).ToList(),
            }).ToList(),

            Crediarios = crediarios.Select(c => new CrediariosHistoricoDto
            {
                Id             = c.Id,
                ValorEmReais   = c.ValorEmReais,
                SaldoRestante  = c.SaldoRestanteEmReais,
                Status         = c.Status.ToString(),
                Vencido        = c.Vencido,
                DataAbertura   = c.DataAbertura,
                DataVencimento = c.DataVencimento,
                DataPagamento  = c.DataPagamento,
                Observacao     = c.Observacao,
            }).ToList(),

            Campeonatos = campeonatos.Select(p => new CampeonatoHistoricoDto
            {
                ChampionshipId   = p.ChampionshipId,
                ChampionshipName = p.Championship.Name,
                Game             = p.Championship.Game,
                Status           = p.Championship.Status.ToString(),
                StartDate        = p.Championship.StartDate,
                PlayerNumber     = p.PlayerNumber,
                DeckName         = p.DeckName,
                Placement        = p.Placement,
                RegisteredAt     = p.RegisteredAt,
            }).ToList(),
        };

        return Ok(historico);
    }

    // =========================================================================
    // ADMIN — Atribuir / remover perfil de operador
    // =========================================================================

    /// <summary>Muda o perfil de acesso de um operador. Envie perfilId=null para desatribuir.</summary>
    [HttpPut("{id:guid}/perfil")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AtualizarPerfil(Guid id, [FromBody] AtualizarPerfilOperadorRequest request)
    {
        var user = await _db.Users.Include(u => u.Perfil).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
            return NotFound(new { Message = "Usuário não encontrado." });

        if (user.Role != "Operator")
            return BadRequest(new { Message = "Apenas operadores podem ter perfil atribuído." });

        if (request.PerfilId.HasValue)
        {
            var perfil = await _db.Perfis.FindAsync(request.PerfilId.Value);
            if (perfil == null)
                return NotFound(new { Message = "Perfil não encontrado." });
            user.PerfilId  = perfil.Id;
            user.Perfil    = perfil;
        }
        else
        {
            user.PerfilId = null;
            user.Perfil   = null;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("AtualizouPerfilOperador", "User", id.ToString(),
            $"{{\"perfilId\":\"{request.PerfilId}\"}}",
            HttpContext);

        return Ok(UserService.MapToSummary(user));
    }

    // =========================================================================
    // ADMIN — Excluir operador
    // =========================================================================

    /// <summary>
    /// Remove ou anonimiza um usuário.
    /// — Customer: anonimização LGPD (preserva histórico financeiro).
    /// — Operator: exclusão permanente.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var adminId = GetUserId();
        if (adminId == id)
            return BadRequest(new { Message = "Você não pode excluir a própria conta por aqui." });

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { Message = "Usuário não encontrado." });

        if (user.Role == "Admin")
            return BadRequest(new { Message = "Contas de administrador não podem ser excluídas." });

        if (user.Role == "Customer")
        {
            // LGPD Art. 18 VI — dados pessoais removidos, histórico financeiro preservado
            await _audit.LogAsync("AnonimizouCliente", "User", id.ToString(),
                $"{{\"nome\":\"{user.Name}\",\"motivo\":\"SolicitacaoAdmin\"}}",
                HttpContext);
            await _service.AnonimizarAsync(id);
            return NoContent();
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("ExcluiuOperador", "User", id.ToString(),
            $"{{\"nome\":\"{user.Name}\",\"role\":\"{user.Role}\"}}",
            HttpContext);

        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token inválido: identificador de usuário ausente.");
        return id;
    }
}
