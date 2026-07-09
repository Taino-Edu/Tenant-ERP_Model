// =============================================================================
// PerfisController.cs — CRUD de perfis de operador
// POST   /api/perfis        → Criar perfil (Admin)
// GET    /api/perfis        → Listar perfis (Admin)
// GET    /api/perfis/{id}   → Detalhar perfil (Admin)
// PUT    /api/perfis/{id}   → Atualizar perfil (Admin)
// DELETE /api/perfis/{id}   → Excluir perfil (Admin)
// GET    /api/perfis/permissoes → Listar permissões disponíveis
// =============================================================================

using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/perfis")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class PerfisController : ControllerBase
{
    private readonly AppDbContext           _db;
    private readonly ILogger<PerfisController> _logger;

    public PerfisController(AppDbContext db, ILogger<PerfisController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // =========================================================================
    // GET /api/perfis/permissoes — Lista todas as permissões disponíveis
    // =========================================================================

    [HttpGet("permissoes")]
    public IActionResult ListPermissoes()
    {
        var permissoes = Permissao.Todos.Select(p => new
        {
            Key   = p,
            Label = p switch
            {
                Permissao.Dashboard   => "Painel Geral",
                Permissao.Pdv         => "Frente de Caixa",
                Permissao.Comandas    => "Comandas",
                Permissao.Estoque     => "Estoque",
                Permissao.Categorias  => "Categorias",
                Permissao.Usuarios    => "Clientes & Usuários",
                Permissao.Crediario   => "Crediário",
                Permissao.Financeiro  => "Relatório Financeiro",
                Permissao.Relatorios  => "Relatórios Gerais",
                Permissao.Anuncios    => "Anúncios",
                Permissao.QrCodes     => "QR Codes",
                Permissao.Lgpd        => "LGPD & Auditoria",
                _                     => p,
            }
        });
        return Ok(permissoes);
    }

    // =========================================================================
    // GET /api/perfis
    // =========================================================================

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var perfis = await _db.Perfis
            .Include(p => p.Users)
            .OrderBy(p => p.Nome)
            .ToListAsync();

        var result = perfis.Select(p => ToDto(p));
        return Ok(result);
    }

    // =========================================================================
    // GET /api/perfis/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var perfil = await _db.Perfis.Include(p => p.Users).FirstOrDefaultAsync(p => p.Id == id);
        if (perfil == null) return NotFound(new { Message = "Perfil não encontrado." });
        return Ok(ToDto(perfil));
    }

    // =========================================================================
    // POST /api/perfis
    // =========================================================================

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CriarPerfilRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var adminId = GetAdminId();
        if (adminId == null) return Unauthorized();

        // Filtra permissões inválidas
        var permissoesValidas = request.Permissoes
            .Where(p => Permissao.Todos.Contains(p))
            .Distinct()
            .ToArray();

        var perfil = new Perfil
        {
            Nome              = request.Nome.Trim(),
            PermissoesJson    = JsonSerializer.Serialize(permissoesValidas),
            CriadoPorAdminId  = adminId.Value,
        };

        _db.Perfis.Add(perfil);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminId} criou perfil '{Nome}' ({Id})", adminId, perfil.Nome, perfil.Id);
        return CreatedAtAction(nameof(GetById), new { id = perfil.Id }, ToDto(perfil));
    }

    // =========================================================================
    // PUT /api/perfis/{id}
    // =========================================================================

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AtualizarPerfilRequest request)
    {
        var perfil = await _db.Perfis.FindAsync(id);
        if (perfil == null) return NotFound(new { Message = "Perfil não encontrado." });

        if (!string.IsNullOrWhiteSpace(request.Nome))
            perfil.Nome = request.Nome.Trim();

        if (request.Permissoes != null)
        {
            var permissoesValidas = request.Permissoes
                .Where(p => Permissao.Todos.Contains(p))
                .Distinct()
                .ToArray();
            perfil.PermissoesJson = JsonSerializer.Serialize(permissoesValidas);
        }

        perfil.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Perfil {Id} atualizado", id);
        return Ok(ToDto(perfil));
    }

    // =========================================================================
    // DELETE /api/perfis/{id}
    // =========================================================================

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var perfil = await _db.Perfis.Include(p => p.Users).FirstOrDefaultAsync(p => p.Id == id);
        if (perfil == null) return NotFound(new { Message = "Perfil não encontrado." });

        if (perfil.Users.Any())
            return Conflict(new { Message = $"Este perfil está em uso por {perfil.Users.Count} usuário(s). Reatribua-os antes de excluir." });

        _db.Perfis.Remove(perfil);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Perfil {Id} ({Nome}) excluído", id, perfil.Nome);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid? GetAdminId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    private static PerfilDto ToDto(Perfil p)
    {
        string[] perms;
        try { perms = JsonSerializer.Deserialize<string[]>(p.PermissoesJson) ?? []; }
        catch { perms = []; }

        return new PerfilDto
        {
            Id            = p.Id,
            Nome          = p.Nome,
            Permissoes    = perms,
            CriadoEm     = p.CriadoEm,
            AtualizadoEm = p.AtualizadoEm,
            TotalUsuarios = p.Users.Count,
        };
    }
}
