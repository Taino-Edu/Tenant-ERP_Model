using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;

namespace CardGameStore.Swagger;

/// <summary>
/// Só adiciona o cadeado/"requer token" na doc do Swagger em endpoints que
/// exigem [Authorize] de verdade — sem isso, um requisito de segurança global
/// (nível de documento) fazia TODO endpoint mostrar o cadeado, inclusive os
/// públicos ([AllowAnonymous]). Tentar "remover" o cadeado por operação não
/// funciona (uma lista de segurança vazia é omitida do JSON e o Swagger UI
/// cai de volta pro default do documento) — por isso a abordagem é inversa:
/// nenhum requisito global, só adiciona onde precisa mesmo.
/// </summary>
public class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var methodAttrs = context.MethodInfo.GetCustomAttributes(true);
        var classAttrs  = context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? Array.Empty<object>();

        var methodAllowAnonymous = methodAttrs.OfType<AllowAnonymousAttribute>().Any();
        var requiresAuth = !methodAllowAnonymous
            && (methodAttrs.OfType<AuthorizeAttribute>().Any() || classAttrs.OfType<AuthorizeAttribute>().Any());

        if (!requiresAuth) return;

        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }]
                    = Array.Empty<string>()
            }
        };
    }
}
