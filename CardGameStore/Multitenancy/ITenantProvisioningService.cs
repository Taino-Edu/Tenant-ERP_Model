namespace CardGameStore.Multitenancy;

public interface ITenantProvisioningService
{
    /// <summary>
    /// Cria um tenant novo: valida o slug, cria o schema Postgres, roda as
    /// migrations do AppDbContext nele e cadastra o admin inicial da loja.
    /// Lança InvalidOperationException para erros de validação (slug inválido
    /// ou já em uso).
    /// </summary>
    /// <param name="slug">Subdomínio da loja (ex: "loja-exemplo" em loja-exemplo.2esysten.com.br).</param>
    /// <param name="adminEmail">E-mail do admin inicial da loja.</param>
    /// <param name="adminPassword">Senha inicial do admin.</param>
    /// <param name="enabledModules">Módulos pagos habilitados já na criação (ex: ["fiscal","estoque"]).
    /// Null ou vazio cai no default do model (["fiscal"]) — mesmo comportamento de antes desse parâmetro existir.</param>
    /// <param name="planName">Nome do plano contratado (ex: "Mar", "Lagoa"). Null cai no default do model ("Rio").</param>
    /// <param name="maxUsers">Limite de usuários com acesso ao painel (Admin+Operator). Null = sem limite.</param>
    /// <param name="kind">Native cria banco e admin; ExternalIntegrated mantém os dados no sistema de origem.</param>
    /// <param name="isPubliclyListed">Autoriza a vitrine institucional; a exibição só ocorre depois que a loja tiver logo.</param>
    /// <param name="owner">Dono informado por quem criou a loja pelo site. Quando vem, o admin
    /// nasce com esse nome, com a senha já em hash (adminPassword é ignorado) e com o Id que o
    /// chamador escolheu, e a loja nasce com o nome digitado. Null mantém o comportamento do
    /// painel da plataforma.</param>
    Task<Tenant> ProvisionAsync(
        string slug, string? adminEmail, string? adminPassword, string[]? enabledModules = null,
        string? planName = null, int? maxUsers = null, TenantKind kind = TenantKind.Native,
        bool isPubliclyListed = false, TenantOwnerProfile? owner = null);
}

/// <summary>Quem criou a loja pelo site (TenantSignupService).</summary>
/// <param name="UserId">Id do User admin. O chamador escolhe para poder emitir o ticket de
/// entrada sem abrir uma conexão no schema recém-criado só para descobri-lo.</param>
/// <param name="Name">Nome de quem cadastrou; vira o nome do User admin.</param>
/// <param name="PasswordHash">BCrypt. A senha em texto puro nunca chega ao provisionamento.</param>
/// <param name="StoreName">Nome da loja; vai para Tenant.DisplayName e SiteConfig.SiteName.</param>
/// <remarks>O WhatsApp informado no site fica só no TenantSignup, e não no User do admin, de
/// propósito: o quick-login da mesa procura cadastro por WhatsApp, e o dono testando a
/// própria mesa esbarraria na conta de admin em vez de virar cliente.</remarks>
public sealed record TenantOwnerProfile(Guid UserId, string Name, string PasswordHash, string StoreName);
