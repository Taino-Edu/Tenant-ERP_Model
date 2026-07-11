// =============================================================================
// RequireModuleAttribute.cs — Gate de módulo pago por tenant (billing ciclo 1).
//
// Convive com [Authorize(Policy = "...")]: a policy decide QUEM pode chamar o
// endpoint (role), este atributo decide se a LOJA (tenant atual) contratou o
// módulo. Lê ITenantContext.EnabledModules (populado pelo
// TenantResolutionMiddleware) — não faz query própria nem cache extra.
// =============================================================================

using Microsoft.AspNetCore.Mvc.Filters;

namespace CardGameStore.Multitenancy;

public class RequireModuleAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _module;

    public RequireModuleAttribute(string module) => _module = module;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();

        if (!tenantContext.EnabledModules.Contains(_module, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new Microsoft.AspNetCore.Mvc.ObjectResult(
                new { Message = "Este módulo não está habilitado para esta loja." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        await next();
    }
}
