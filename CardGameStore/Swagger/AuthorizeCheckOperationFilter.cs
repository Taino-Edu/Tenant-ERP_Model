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

        // Dois requisitos SEPARADOS (duas entradas na lista), não um só com dois
        // esquemas: no OpenAPI, itens da lista são alternativas ("ou"), e chaves
        // dentro do mesmo item são cumulativas ("e"). Juntar os dois num item só
        // faria a doc afirmar que o endpoint exige cookie E header ao mesmo tempo,
        // o que é falso — qualquer um dos dois autentica.
        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {
                [Reference("cookieAuth")] = Array.Empty<string>()
            },
            new()
            {
                [Reference("Bearer")] = Array.Empty<string>()
            }
        };
    }

    private static OpenApiSecurityScheme Reference(string id) => new()
    {
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = id }
    };
}
