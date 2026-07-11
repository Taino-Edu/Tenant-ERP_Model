namespace CardGameStore.Multitenancy;

public interface ITenantProvisioningService
{
    /// <summary>
    /// Cria um tenant novo: valida o slug, cria o schema Postgres, roda as
    /// migrations do AppDbContext nele e cadastra o admin inicial da loja.
    /// Lança InvalidOperationException para erros de validação (slug inválido
    /// ou já em uso).
    /// </summary>
    Task<Tenant> ProvisionAsync(string slug, string adminEmail, string adminPassword);
}
