// =============================================================================
// LoginLanding.cs — Para onde o redeem-login pode mandar a pessoa depois de
// entrar.
//
// O redeem aceita um destino na query (?next=) porque a loja criada pelo site
// deve abrir no guia inicial, não na tela de comandas vazia. Destino vindo da
// URL é o formato clássico de open redirect, então a regra é a mais estreita
// que resolve: só caminho do próprio painel, só letras minúsculas, números e
// hífen — sem esquema, sem host, sem "//", sem "..", sem query.
// =============================================================================

using System.Text.RegularExpressions;

namespace CardGameStore.Security;

public static class LoginLanding
{
    private static readonly Regex CaminhoDoPainel = new(@"^/admin(?:/[a-z0-9-]+)+\z", RegexOptions.Compiled);

    public static bool EhCaminhoSeguroDoPainel(string? caminho) =>
        caminho is { Length: <= 100 } && CaminhoDoPainel.IsMatch(caminho);
}
