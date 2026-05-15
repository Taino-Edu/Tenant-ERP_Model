// =============================================================================
// UploadController.cs — Upload de imagens para a loja
// POST /api/upload/image  → salva em wwwroot/uploads/ e retorna URL pública
// Apenas administradores podem fazer upload.
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UploadController> _logger;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public UploadController(IWebHostEnvironment env, ILogger<UploadController> logger)
    {
        _env    = env;
        _logger = logger;
    }

    /// <summary>
    /// Faz upload de uma imagem (JPEG, PNG ou WebP, máx. 5 MB).
    /// Retorna a URL pública do arquivo salvo em /uploads/.
    /// </summary>
    [HttpPost("image")]
    [Authorize(Policy = "AdminOnly")]
    [RequestSizeLimit(6 * 1024 * 1024)] // margem acima do limite de negócio
    [ProducesResponseType(typeof(UploadImageResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ErrorResponse("Nenhum arquivo enviado."));

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new ErrorResponse($"O arquivo excede o tamanho máximo de 5 MB (recebido: {file.Length / 1024} KB)."));

        if (!AllowedMimeTypes.Contains(file.ContentType))
            return BadRequest(new ErrorResponse($"Tipo de arquivo não permitido: {file.ContentType}. Use JPEG, PNG ou WebP."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new ErrorResponse($"Extensão não permitida: {ext}. Use .jpg, .jpeg, .png ou .webp."));

        // Garante que a pasta de uploads existe
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);

        // Nome único para evitar colisões e path traversal
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream);
        }

        var url = $"/uploads/{fileName}";
        _logger.LogInformation("Upload de imagem: {FileName} ({Size} bytes) por {User}",
            fileName, file.Length, User.Identity?.Name ?? "unknown");

        return Ok(new UploadImageResponse(url));
    }
}

/// <summary>Resposta de upload bem-sucedido.</summary>
public record UploadImageResponse(string Url);

/// <summary>Resposta de erro de validação.</summary>
public record ErrorResponse(string Message);
