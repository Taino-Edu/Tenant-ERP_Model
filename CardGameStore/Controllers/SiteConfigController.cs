// =============================================================================
// SiteConfigController.cs — Personalização da landing page (nome, textos, cores)
//
// GET /api/site-config  → público, a landing precisa carregar sem estar logado
// PUT /api/site-config  → Admin only
// =============================================================================

using CardGameStore.Data;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Multitenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/site-config")]
[Produces("application/json")]
public class SiteConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly CatalogDbContext _catalog;
    private readonly ILogger<SiteConfigController> _logger;

    public SiteConfigController(AppDbContext db, ITenantContext tenant, CatalogDbContext catalog, ILogger<SiteConfigController> logger)
    {
        _db      = db;
        _tenant  = tenant;
        _catalog = catalog;
        _logger  = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get()
    {
        var cfg = await _db.SiteConfigs.FindAsync(SiteConfig.SingletonId) ?? new SiteConfig();
        // Inofensivo expor via endpoint público: só diz quais módulos pagos a loja
        // habilitou, não vaza dado sensível nenhum (mesmo espírito de expor a cor/nome
        // da loja aqui, que já é público).
        cfg.EnabledModules = _tenant.EnabledModules;
        return Ok(cfg);
    }

    [HttpPut]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Save([FromBody] SaveSiteConfigRequest req)
    {
        var cfg = await GetOrCreateConfigAsync();

        if (req.SiteName             is not null) cfg.SiteName             = req.SiteName;
        if (req.HeroSubtitle         is not null) cfg.HeroSubtitle         = req.HeroSubtitle;
        if (req.AddressLine          is not null) cfg.AddressLine          = req.AddressLine;
        if (req.ContactPersonName    is not null) cfg.ContactPersonName    = req.ContactPersonName;
        if (req.WhatsappNumber       is not null) cfg.WhatsappNumber       = req.WhatsappNumber;
        if (req.ContactEmail         is not null) cfg.ContactEmail         = req.ContactEmail;
        if (req.LogoUrl              is not null) cfg.LogoUrl              = req.LogoUrl;
        if (req.FaviconUrl           is not null) cfg.FaviconUrl           = req.FaviconUrl;
        if (req.PwaIconUrl           is not null) cfg.PwaIconUrl           = req.PwaIconUrl;
        if (req.AdminIconUrl         is not null) cfg.AdminIconUrl         = req.AdminIconUrl;
        if (req.NavProdutosLabel     is not null) cfg.NavProdutosLabel     = req.NavProdutosLabel;
        if (req.NavPontosLabel       is not null) cfg.NavPontosLabel       = req.NavPontosLabel;
        if (req.CtaVerProdutosLabel  is not null) cfg.CtaVerProdutosLabel  = req.CtaVerProdutosLabel;
        if (req.ProdutosEyebrow      is not null) cfg.ProdutosEyebrow      = req.ProdutosEyebrow;
        if (req.ProdutosTitle        is not null) cfg.ProdutosTitle        = req.ProdutosTitle;
        if (req.PontosEyebrow        is not null) cfg.PontosEyebrow        = req.PontosEyebrow;
        if (req.PontosTitle          is not null) cfg.PontosTitle          = req.PontosTitle;
        if (req.PontosParagraph      is not null) cfg.PontosParagraph      = req.PontosParagraph;
        if (req.ColorPrimary         is not null) cfg.ColorPrimary         = req.ColorPrimary;
        if (req.ColorAccent          is not null) cfg.ColorAccent          = req.ColorAccent;
        if (req.ColorNavy            is not null) cfg.ColorNavy            = req.ColorNavy;
        if (req.ColorBackground      is not null) cfg.ColorBackground      = req.ColorBackground;
        if (req.ColorCard            is not null) cfg.ColorCard            = req.ColorCard;

        cfg.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await SyncCatalogDirectoryAsync(cfg);

        return Ok(cfg);
    }

    /// <summary>
    /// Espelha nome/logo no catálogo (Tenant.DisplayName/LogoUrl) — cópia
    /// denormalizada só pra o diretório público de lojas (institucional) não
    /// precisar trocar de schema por tenant a cada carregamento. Best-effort:
    /// uma falha aqui é cosmética (o card da loja no diretório fica com dado
    /// velho até a próxima edição), nunca deve derrubar o save principal do
    /// SiteConfig do próprio tenant, que já aconteceu com sucesso acima.
    /// </summary>
    private async Task SyncCatalogDirectoryAsync(SiteConfig cfg)
    {
        try
        {
            var tenant = await _catalog.Tenants.FirstOrDefaultAsync(t => t.Id == _tenant.TenantId);
            if (tenant is null) return;

            tenant.DisplayName = cfg.SiteName;
            tenant.LogoUrl     = cfg.LogoUrl;
            await _catalog.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao sincronizar DisplayName/LogoUrl pro catálogo do tenant {TenantId}", _tenant.TenantId);
        }
    }

    /// <summary>
    /// Busca a linha única de configuração pelo ID fixo, criando-a se necessário.
    /// Mesmo padrão de FiscalController.GetOrCreateConfigAsync.
    /// </summary>
    private async Task<SiteConfig> GetOrCreateConfigAsync()
    {
        var cfg = await _db.SiteConfigs.FindAsync(SiteConfig.SingletonId);
        if (cfg is not null) return cfg;

        cfg = new SiteConfig();
        _db.SiteConfigs.Add(cfg);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(cfg).State = EntityState.Detached;
            cfg = await _db.SiteConfigs.FindAsync(SiteConfig.SingletonId)
                ?? throw new InvalidOperationException("Falha ao obter configuração do site após conflito de concorrência.");
        }

        return cfg;
    }
}

public class SaveSiteConfigRequest
{
    public string? SiteName            { get; init; }
    public string? HeroSubtitle        { get; init; }
    public string? AddressLine         { get; init; }
    public string? ContactPersonName   { get; init; }
    public string? WhatsappNumber      { get; init; }
    public string? ContactEmail        { get; init; }
    public string? LogoUrl             { get; init; }
    public string? FaviconUrl          { get; init; }
    public string? PwaIconUrl          { get; init; }
    public string? AdminIconUrl        { get; init; }
    public string? NavProdutosLabel    { get; init; }
    public string? NavPontosLabel      { get; init; }
    public string? CtaVerProdutosLabel { get; init; }
    public string? ProdutosEyebrow     { get; init; }
    public string? ProdutosTitle       { get; init; }
    public string? PontosEyebrow       { get; init; }
    public string? PontosTitle         { get; init; }
    public string? PontosParagraph     { get; init; }
    public string? ColorPrimary        { get; init; }
    public string? ColorAccent         { get; init; }
    public string? ColorNavy           { get; init; }
    public string? ColorBackground     { get; init; }
    public string? ColorCard           { get; init; }
}
