// =============================================================================
// AiConfigController.cs — Chave própria do Gemini (BYOK) pro tenant, opcional.
//
// GET/PUT /api/ai-config → AdminOnly + módulo "ia" (só faz sentido configurar
// BYOK pra quem já tem o assistente de IA habilitado). Mesmo padrão de
// EmailConfigController: nunca devolve a chave em texto puro, só um flag.
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/ai-config")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
[RequireModule("ia")]
public class AiConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EncryptionService _enc;

    public AiConfigController(AppDbContext db, EncryptionService enc)
    {
        _db  = db;
        _enc = enc;
    }

    /// <summary>Configuração de chave própria do Gemini do tenant. Nunca devolve
    /// a chave — só um flag indicando se já existe uma salva.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var cfg = await _db.AiConfigs.FindAsync(AiConfig.SingletonId);

        return Ok(new AiConfigDto
        {
            IsActive = cfg?.IsActive ?? false,
            HasKey   = !string.IsNullOrEmpty(cfg?.GeminiApiKeyEncrypted),
        });
    }

    /// <summary>Atualiza a configuração de BYOK do tenant — patch parcial. A chave
    /// só é re-criptografada e sobrescrita se um valor novo, não-vazio, for
    /// enviado; do contrário a chave já salva é preservada.</summary>
    [HttpPut]
    public async Task<IActionResult> Save([FromBody] SaveAiConfigRequest req)
    {
        var cfg = await GetOrCreateConfigAsync();

        if (req.IsActive.HasValue) cfg.IsActive = req.IsActive.Value;

        if (!string.IsNullOrEmpty(req.GeminiApiKey))
            cfg.GeminiApiKeyEncrypted = _enc.Encrypt(req.GeminiApiKey);

        cfg.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new AiConfigDto
        {
            IsActive = cfg.IsActive,
            HasKey   = !string.IsNullOrEmpty(cfg.GeminiApiKeyEncrypted),
        });
    }

    /// <summary>Busca a linha única de configuração pelo ID fixo, criando-a se necessário.
    /// Mesmo padrão de EmailConfigController.GetOrCreateConfigAsync.</summary>
    private async Task<AiConfig> GetOrCreateConfigAsync()
    {
        var cfg = await _db.AiConfigs.FindAsync(AiConfig.SingletonId);
        if (cfg is not null) return cfg;

        cfg = new AiConfig();
        _db.AiConfigs.Add(cfg);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(cfg).State = EntityState.Detached;
            cfg = await _db.AiConfigs.FindAsync(AiConfig.SingletonId)
                ?? throw new InvalidOperationException("Falha ao obter configuração de IA após conflito de concorrência.");
        }

        return cfg;
    }
}

public class AiConfigDto
{
    public bool IsActive { get; init; }
    public bool HasKey   { get; init; }
}

public class SaveAiConfigRequest
{
    public string? GeminiApiKey { get; init; }
    public bool?   IsActive     { get; init; }
}
