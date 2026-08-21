using CardGameStore.Security;
using CardGameStore.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/platform/ibpt")]
[Authorize(Policy = "PlatformOwnerOnly")]
[RequirePlatformPermission(PlatformPermission.TenantsRead)]
public sealed class PlatformIbptController(PlatformIbptService ibpt) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await ibpt.ListarAsync(ct));

    [HttpPost("importar")]
    [RequirePlatformPermission(PlatformPermission.TenantsManage)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Importar(IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(new { Message = "Selecione o arquivo TabelaIBPTax<UF><versão>.csv." });

        if (!string.Equals(Path.GetExtension(arquivo.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { Message = "Envie o CSV oficial do IBPT; outros formatos não são aceitos." });

        try
        {
            await using var conteudo = arquivo.OpenReadStream();
            return Ok(await ibpt.ImportarAsync(conteudo, arquivo.FileName, ct));
        }
        catch (IbptIntegrationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}
