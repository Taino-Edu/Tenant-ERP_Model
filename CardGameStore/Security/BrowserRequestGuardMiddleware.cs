namespace CardGameStore.Security;

/// <summary>
/// Bloqueia mutações autenticadas iniciadas por outro site ou por um subdomínio
/// irmão. SameSite não separa tenants sob o mesmo domínio registrável.
/// Headers ausentes são tolerados para clientes não-browser e testes; CORS e a
/// autenticação continuam sendo aplicados normalmente.
/// </summary>
public sealed class BrowserRequestGuardMiddleware
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { HttpMethods.Get, HttpMethods.Head, HttpMethods.Options };

    private readonly RequestDelegate _next;

    public BrowserRequestGuardMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.Request.Path.StartsWithSegments("/api") &&
            !SafeMethods.Contains(context.Request.Method))
        {
            var fetchSite = context.Request.Headers["Sec-Fetch-Site"].FirstOrDefault();
            if (fetchSite is "cross-site" or "same-site")
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    Message = "Requisição bloqueada por proteção de origem.",
                });
                return;
            }
        }

        await _next(context);
    }
}

public static class BrowserRequestGuardMiddlewareExtensions
{
    public static IApplicationBuilder UseBrowserRequestGuard(this IApplicationBuilder app) =>
        app.UseMiddleware<BrowserRequestGuardMiddleware>();
}
