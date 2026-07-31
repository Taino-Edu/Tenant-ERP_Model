namespace CardGameStore.Middleware;

/// <summary>
/// Declara quais permissões de perfil liberam um endpoint para Operator.
/// Quando houver mais de uma, basta possuir uma delas.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireOperatorPermissionAttribute : Attribute
{
    public RequireOperatorPermissionAttribute(params string[] permissions) => Permissions = permissions;
    public IReadOnlyList<string> Permissions { get; }
}

/// <summary>Endpoint autenticado que opera somente sobre o próprio usuário/dispositivo.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class OperatorSelfServiceAttribute : Attribute;

/// <summary>
/// Marca uma negativa intencional. Evita que uma nova rota AdminOnly fique sem
/// classificação por esquecimento e documenta que Operator não deve acessá-la.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class OperatorForbiddenAttribute : Attribute;
