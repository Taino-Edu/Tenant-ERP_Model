using CardGameStore.DTOs;
using CardGameStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CardGameStore.Controllers;

[ApiController]
[Route("api/public/assistant")]
[AllowAnonymous]
[EnableRateLimiting("public-ai")]
public sealed class PublicAssistantController : ControllerBase
{
    private readonly IPublicSalesAssistantService _assistant;

    public PublicAssistantController(IPublicSalesAssistantService assistant)
    {
        _assistant = assistant;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PublicAssistantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PublicAssistantResponse>> Ask(
        [FromBody] PublicAssistantRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _assistant.AskAsync(request.Message, cancellationToken));
    }
}
