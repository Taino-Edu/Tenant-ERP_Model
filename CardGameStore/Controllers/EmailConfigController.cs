// =============================================================================
// EmailConfigController.cs — SMTP próprio do tenant (ex: Gmail dele), opcional.
//
// GET/PUT /api/email-config → ambos Admin only. Diferente de SiteConfig, não
// existe motivo pra expor isso publicamente — é credencial, não branding.
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/email-config")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class EmailConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EncryptionService _enc;

    public EmailConfigController(AppDbContext db, EncryptionService enc)
    {
        _db  = db;
        _enc = enc;
    }

    /// <summary>Configuração de SMTP próprio do tenant. Nunca devolve a senha —
    /// só um flag indicando se já existe uma salva.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var cfg = await _db.EmailConfigs.FindAsync(EmailConfig.SingletonId);

        return Ok(new EmailConfigDto
        {
            SmtpHost     = cfg?.SmtpHost,
            SmtpPort     = cfg?.SmtpPort,
            SmtpUsername = cfg?.SmtpUsername,
            FromName     = cfg?.FromName,
            IsActive     = cfg?.IsActive ?? false,
            HasPassword  = !string.IsNullOrEmpty(cfg?.SmtpPasswordEncrypted),
        });
    }

    /// <summary>Atualiza a configuração de SMTP do tenant — patch parcial. A senha só é
    /// re-criptografada e sobrescrita se um valor novo, não-vazio, for enviado; do
    /// contrário a senha já salva é preservada.</summary>
    /// <param name="req">Campos a atualizar. Campos omitidos/nulos não são alterados.</param>
    [HttpPut]
    public async Task<IActionResult> Save([FromBody] SaveEmailConfigRequest req)
    {
        var cfg = await GetOrCreateConfigAsync();

        if (req.SmtpHost     is not null) cfg.SmtpHost     = req.SmtpHost;
        if (req.SmtpPort.HasValue)        cfg.SmtpPort     = req.SmtpPort;
        if (req.SmtpUsername is not null) cfg.SmtpUsername = req.SmtpUsername;
        if (req.FromName     is not null) cfg.FromName     = req.FromName;
        if (req.IsActive.HasValue)        cfg.IsActive     = req.IsActive.Value;

        if (!string.IsNullOrEmpty(req.SmtpPassword))
            cfg.SmtpPasswordEncrypted = _enc.Encrypt(req.SmtpPassword);

        cfg.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new EmailConfigDto
        {
            SmtpHost     = cfg.SmtpHost,
            SmtpPort     = cfg.SmtpPort,
            SmtpUsername = cfg.SmtpUsername,
            FromName     = cfg.FromName,
            IsActive     = cfg.IsActive,
            HasPassword  = !string.IsNullOrEmpty(cfg.SmtpPasswordEncrypted),
        });
    }

    /// <summary>Busca a linha única de configuração pelo ID fixo, criando-a se necessário.
    /// Mesmo padrão de SiteConfigController.GetOrCreateConfigAsync.</summary>
    private async Task<EmailConfig> GetOrCreateConfigAsync()
    {
        var cfg = await _db.EmailConfigs.FindAsync(EmailConfig.SingletonId);
        if (cfg is not null) return cfg;

        cfg = new EmailConfig();
        _db.EmailConfigs.Add(cfg);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(cfg).State = EntityState.Detached;
            cfg = await _db.EmailConfigs.FindAsync(EmailConfig.SingletonId)
                ?? throw new InvalidOperationException("Falha ao obter configuração de e-mail após conflito de concorrência.");
        }

        return cfg;
    }
}

public class EmailConfigDto
{
    public string? SmtpHost     { get; init; }
    public int?    SmtpPort     { get; init; }
    public string? SmtpUsername { get; init; }
    public string? FromName     { get; init; }
    public bool    IsActive     { get; init; }
    public bool    HasPassword  { get; init; }
}

public class SaveEmailConfigRequest
{
    public string? SmtpHost     { get; init; }
    public int?    SmtpPort     { get; init; }
    public string? SmtpUsername { get; init; }
    public string? SmtpPassword { get; init; }
    public string? FromName     { get; init; }
    public bool?   IsActive     { get; init; }
}
