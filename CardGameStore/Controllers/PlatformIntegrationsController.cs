// =============================================================================
// PlatformIntegrationsController.cs — Credenciais dos serviços externos que a
// PLATAFORMA usa (hoje: Banco Inter, pra cobrar as mensalidades das lojas).
//
// PlatformOwnerOnly, como todo /api/platform/*. Aqui a régua é mais alta que no
// resto: um vazamento daqui entrega a conta bancária da plataforma, não um dado
// de loja. Por isso nenhum segredo volta em resposta — a tela mostra se está
// configurado, nunca o valor.
// =============================================================================

using CardGameStore.DTOs;
using CardGameStore.Multitenancy;
using CardGameStore.Services.Implementations;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/platform/integracoes")]
[Authorize(Policy = "PlatformOwnerOnly")]
[Produces("application/json")]
public class PlatformIntegrationsController : ControllerBase
{
    private readonly CatalogDbContext  _catalog;
    private readonly EncryptionService _enc;
    private readonly IAuditService     _audit;

    public PlatformIntegrationsController(CatalogDbContext catalog, EncryptionService enc, IAuditService audit)
    {
        _catalog = catalog;
        _enc     = enc;
        _audit   = audit;
    }

    /// <summary>Estado de cada integração — o que está configurado, o que falta
    /// e qual foi o último erro. Nunca devolve credencial.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var salvas = await _catalog.PlatformIntegrations.AsNoTracking().ToListAsync(ct);

        // Devolve a lista dos provedores CONHECIDOS, não só os já salvos: a tela
        // precisa mostrar "Inter — não configurado" pra existir o que clicar.
        var resultado = PlatformIntegrationProvider.Todos.Select(provider =>
        {
            var cfg = salvas.FirstOrDefault(s => s.Provider == provider);
            return PlatformIntegrationDto.De(provider, cfg);
        });

        return Ok(resultado);
    }

    /// <summary>Salva/atualiza as credenciais de um provedor.
    ///
    /// Campos de segredo em branco significam "mantém o que já está lá", não
    /// "apaga": a tela nunca recebe o valor atual, então reenviar o formulário
    /// sem retocar o secret não pode zerar a configuração.</summary>
    [HttpPut("{provider}")]
    public async Task<IActionResult> Salvar(string provider, [FromBody] SalvarPlatformIntegrationRequest req, CancellationToken ct)
    {
        provider = provider.Trim().ToLowerInvariant();
        if (!PlatformIntegrationProvider.EhConhecido(provider))
            return BadRequest(new { Message = $"Integração \"{provider}\" não existe. Conhecidas: {string.Join(", ", PlatformIntegrationProvider.Todos)}." });

        var cfg = await _catalog.PlatformIntegrations.FirstOrDefaultAsync(p => p.Provider == provider, ct);
        if (cfg is null)
        {
            cfg = new PlatformIntegration { Provider = provider };
            _catalog.PlatformIntegrations.Add(cfg);
        }

        if (req.ClientId      is not null) cfg.ClientId      = req.ClientId.Trim();
        if (req.ContaCorrente is not null) cfg.ContaCorrente = SomenteDigitos(req.ContaCorrente);
        if (req.PixKey        is not null) cfg.PixKey        = req.PixKey.Trim();
        if (req.IsActive      is bool ativo) cfg.IsActive    = ativo;

        if (!string.IsNullOrWhiteSpace(req.ClientSecret))
            cfg.ClientSecretEncrypted = _enc.Encrypt(req.ClientSecret.Trim());
        if (!string.IsNullOrWhiteSpace(req.CertificateCrt))
            cfg.CertificateCrtEncrypted = _enc.Encrypt(req.CertificateCrt.Trim());
        if (!string.IsNullOrWhiteSpace(req.CertificateKey))
            cfg.CertificateKeyEncrypted = _enc.Encrypt(req.CertificateKey.Trim());

        cfg.UpdatedAt = DateTime.UtcNow;
        await _catalog.SaveChangesAsync(ct);

        // O que foi alterado entra no log; o valor, nunca.
        await _audit.LogAsync(
            action: "PlatformIntegration.Salvar",
            entityType: nameof(PlatformIntegration),
            entityId: cfg.Id.ToString(),
            details: $"provider={provider}; campos={string.Join(",", req.CamposPreenchidos())}");

        return Ok(PlatformIntegrationDto.De(provider, cfg));
    }

    /// <summary>Apaga as credenciais de um provedor — usado quando a conta muda
    /// de titular ou o segredo vaza e precisa ser revogado por inteiro.</summary>
    [HttpDelete("{provider}")]
    public async Task<IActionResult> Remover(string provider, CancellationToken ct)
    {
        provider = provider.Trim().ToLowerInvariant();
        var cfg = await _catalog.PlatformIntegrations.FirstOrDefaultAsync(p => p.Provider == provider, ct);
        if (cfg is null) return NotFound(new { Message = "Integração não configurada." });

        _catalog.PlatformIntegrations.Remove(cfg);
        await _catalog.SaveChangesAsync(ct);

        await _audit.LogAsync(
            action: "PlatformIntegration.Remover",
            entityType: nameof(PlatformIntegration),
            entityId: cfg.Id.ToString(),
            details: $"provider={provider}");

        return NoContent();
    }

    private static string SomenteDigitos(string valor) => new(valor.Where(char.IsDigit).ToArray());
}
