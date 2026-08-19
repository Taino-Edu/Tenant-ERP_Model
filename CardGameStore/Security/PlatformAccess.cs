using System.Text.Json;

namespace CardGameStore.Security;

public static class PlatformPermission
{
    public const string All            = "platform.*";
    public const string Dashboard      = "platform.dashboard";
    public const string TenantsRead    = "platform.tenants.read";
    public const string TenantsManage  = "platform.tenants.manage";
    public const string TenantsDelete  = "platform.tenants.delete";
    public const string FinanceRead    = "platform.finance.read";
    public const string FinanceManage  = "platform.finance.manage";
    public const string Leads          = "platform.leads";
    public const string Support        = "platform.support";
    public const string Logs           = "platform.logs";
    public const string Impersonate    = "platform.impersonate";
    public const string Team           = "platform.team";
    public const string ReferralsRead  = "platform.referrals.read";
    public const string ReferralsManage = "platform.referrals.manage";
}

public sealed record PlatformProfileDefinition(
    string Key,
    string Name,
    string Description,
    string[] Permissions,
    bool Selectable = true);

public static class PlatformAccessProfiles
{
    public const string Primary      = "primary_owner";
    public const string Partner      = "partner_admin";
    public const string Commercial   = "commercial";
    public const string Finance      = "finance";
    public const string SupportDev   = "support_development";
    public const string Auditor      = "auditor";

    public static readonly IReadOnlyDictionary<string, PlatformProfileDefinition> All =
        new Dictionary<string, PlatformProfileDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [Primary] = new(Primary, "Proprietário principal", "Acesso total e gestão da equipe.", [PlatformPermission.All], false),
            // Sócio enxerga o painel inteiro, equipe inclusive. É curinga de
            // propósito: uma área nova da plataforma passa a valer pra ele no
            // mesmo deploy, sem depender de alguém lembrar de somar a permissão
            // aqui. O que continua fora do alcance dele não é permissão e sim a
            // conta raiz — PlatformTeamController barra editar/desativar o
            // proprietário principal olhando IsPlatformPrimaryOwner.
            [Partner] = new(Partner, "Sócio administrador", "Acesso total à plataforma, equipe incluída. Só não altera a conta do proprietário principal.",
                [PlatformPermission.All]),
            [Commercial] = new(Commercial, "Comercial", "Cuida de leads, prospecção e implantação de clientes.",
                [PlatformPermission.Dashboard, PlatformPermission.TenantsRead, PlatformPermission.TenantsManage, PlatformPermission.Leads,
                 PlatformPermission.ReferralsRead, PlatformPermission.ReferralsManage]),
            [Finance] = new(Finance, "Financeiro", "Consulta tenants e administra cobranças da plataforma.",
                [PlatformPermission.Dashboard, PlatformPermission.TenantsRead, PlatformPermission.FinanceRead, PlatformPermission.FinanceManage,
                 PlatformPermission.ReferralsRead, PlatformPermission.ReferralsManage]),
            [SupportDev] = new(SupportDev, "Suporte e desenvolvimento", "Atende chamados, consulta logs e acessa lojas de forma temporária.",
                [PlatformPermission.Dashboard, PlatformPermission.TenantsRead, PlatformPermission.Support,
                 PlatformPermission.Logs, PlatformPermission.Impersonate]),
            [Auditor] = new(Auditor, "Auditoria", "Acesso somente de leitura a indicadores, tenants, financeiro e logs.",
                [PlatformPermission.Dashboard, PlatformPermission.TenantsRead, PlatformPermission.FinanceRead, PlatformPermission.Logs,
                 PlatformPermission.ReferralsRead]),
        };

    public static PlatformProfileDefinition GetRequired(string key)
    {
        if (!All.TryGetValue(key, out var profile) || !profile.Selectable)
            throw new ArgumentException("Perfil de acesso da plataforma inválido.", nameof(key));
        return profile;
    }

    public static string Serialize(IEnumerable<string> permissions) =>
        JsonSerializer.Serialize(permissions.Distinct(StringComparer.OrdinalIgnoreCase));

    public static string[] Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    /// <summary>
    /// Permissões que valem AGORA para um integrante da equipe da plataforma.
    ///
    /// A coluna platform_permissions_json é só o retrato do perfil no momento em
    /// que a conta foi convidada ou editada. Quando a definição de um perfil muda
    /// aqui no código, quem já estava cadastrado continuaria com a lista velha até
    /// alguém reabrir a conta e salvar de novo — foi exatamente assim que o sócio
    /// ficou sem enxergar parte do painel. Ler pelo perfil elimina esse
    /// descompasso; o JSON só é usado como reserva, se a chave sumir do código.
    /// </summary>
    public static string[] EffectivePermissions(bool isPrimaryOwner, string? profileKey, string? permissionsJson)
    {
        if (isPrimaryOwner) return [PlatformPermission.All];

        // `Selectable` entra aqui e não só no GetRequired: uma conta comum com a
        // chave "primary_owner" gravada resolveria pro curinga. Hoje isso é
        // inalcançável — os dois caminhos de escrita passam pelo GetRequired, que
        // recusa perfis não selecionáveis —, mas depender de um invariante em
        // outro arquivo é frágil demais pra uma decisão de permissão.
        if (!string.IsNullOrWhiteSpace(profileKey) &&
            All.TryGetValue(profileKey, out var profile) &&
            profile.Selectable)
            return [.. profile.Permissions];

        return Deserialize(permissionsJson);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePlatformPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
