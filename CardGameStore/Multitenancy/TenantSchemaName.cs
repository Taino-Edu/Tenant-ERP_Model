namespace CardGameStore.Multitenancy;

/// <summary>
/// Validação centralizada dos identificadores de schema usados em SQL que não
/// aceita parâmetros bind (CREATE/DROP SCHEMA e SET search_path).
/// </summary>
public static class TenantSchemaName
{
    public static string Validate(string? name)
    {
        if (!IsValid(name))
            throw new InvalidOperationException($"Nome de schema de tenant inválido: '{name}'.");

        return name!;
    }

    public static bool IsValid(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= 63
        && !char.IsDigit(name[0])
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}
