using CardGameStore.Configuration;

namespace CardGameStore.Security;

public static class ProductionConfigurationGuard
{
    private const string DefaultPassword = "SenhaForte@123";

    public static void ValidateJwt(JwtSettings jwt, bool isProduction)
    {
        if (!isProduction) return;
        if (string.IsNullOrWhiteSpace(jwt.SecretKey) || jwt.SecretKey.Length < 32 ||
            jwt.SecretKey.Contains("SUBSTITUA", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "JwtSettings:SecretKey deve ser um segredo exclusivo de produção com pelo menos 32 caracteres.");
        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
            throw new InvalidOperationException("JwtSettings:Issuer e Audience são obrigatórios em produção.");
    }

    public static string SeedPassword(string? configured, bool isProduction, string variableName)
    {
        if (isProduction && (string.IsNullOrWhiteSpace(configured) || configured == DefaultPassword))
            throw new InvalidOperationException(
                $"{variableName} é obrigatória e não pode usar a senha padrão ao criar uma conta privilegiada.");
        return string.IsNullOrWhiteSpace(configured) ? DefaultPassword : configured;
    }
}
