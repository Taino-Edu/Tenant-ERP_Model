using Microsoft.AspNetCore.Authorization;
namespace CardGameStore.Security;

public static class McpAccess
{
    // Operadores só poderão entrar quando cada ferramenta declarar sua permissão.
    public static AuthorizationPolicy Policy { get; } = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().RequireRole("Admin").Build();
}
