// =============================================================================
// UploadController.cs — Upload de imagens para a loja
// POST /api/upload/image  → salva em wwwroot/uploads/ e retorna URL pública
// Apenas administradores podem fazer upload.
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CardGameStore.Services.Interfaces;
using System.Security.Claims;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UploadController> _logger;
    private readonly IUserService _userService;

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

    public UploadController(IWebHostEnvironment env, ILogger<UploadController> logger, IUserService userService)
    {
        _env         = env;
        _logger      = logger;
        _userService = userService;
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
        return await ProcessImageUpload(file, "uploads");
    }

    /// <summary>
    /// Upload de imagem para anúncio no marketplace. Qualquer usuário logado pode usar.
    /// Salva em /uploads/marketplace/ e retorna a URL pública.
    /// </summary>
    [HttpPost("marketplace-image")]
    [Authorize]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadImageResponse), 200)]
    public Task<IActionResult> UploadMarketplaceImage(IFormFile? file)
        => ProcessImageUpload(file, Path.Combine("uploads", "marketplace"));

    /// <summary>
    /// Faz upload da foto de perfil do usuário logado.
    /// Salva em /uploads/profiles/ e atualiza o banco de dados.
    /// </summary>
    [HttpPost("profile-image")]
    [Authorize]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadProfileImage(IFormFile? file)
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            return Unauthorized();

        var result = await ProcessImageUpload(file, Path.Combine("uploads", "profiles"));

        if (result is OkObjectResult okResult && okResult.Value is UploadImageResponse response)
        {
            await _userService.UpdateProfileImageAsync(userId, response.Url);
            return Ok(response);
        }

        return result;
    }

    private async Task<IActionResult> ProcessImageUpload(IFormFile? file, string relativeDir)
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

        // Valida magic bytes — impede upload de arquivos renomeados com extensão falsa
        if (!HasValidImageMagicBytes(file))
            return BadRequest(new ErrorResponse("O arquivo não é uma imagem válida."));

        // Garante que a pasta existe
        var uploadsDir = Path.Combine(_env.WebRootPath, relativeDir);
        Directory.CreateDirectory(uploadsDir);

        // Nome único
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream);
        }

        // Se relativeDir for "uploads\profiles" (Windows), substitui \ por / na URL final
        var urlPath = relativeDir.Replace("\\", "/");
        var url = $"/{urlPath}/{fileName}";
        
        _logger.LogInformation("Upload de imagem: {FileName} ({Size} bytes) por {User}",
            fileName, file.Length, User.Identity?.Name ?? "unknown");

        return Ok(new UploadImageResponse(url));
    }

    private static bool HasValidImageMagicBytes(IFormFile file)
    {
        Span<byte> header = stackalloc byte[12];
        using var stream = file.OpenReadStream();
        var n = stream.Read(header);
        if (n < 4) return false;

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true;
        // PNG: 89 50 4E 47
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true;
        // WebP: RIFF .... WEBP
        if (n >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return true;

        return false;
    }
}

/// <summary>Resposta de upload bem-sucedido.</summary>
public record UploadImageResponse(string Url);

/// <summary>Resposta de erro de validação.</summary>
public record ErrorResponse(string Message);
