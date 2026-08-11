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
            [Partner] = new(Partner, "Sócio administrador", "Opera toda a plataforma, sem gerenciar o proprietário principal.",
                [PlatformPermission.Dashboard, PlatformPermission.TenantsRead, PlatformPermission.TenantsManage,
                 PlatformPermission.TenantsDelete, PlatformPermission.FinanceRead, PlatformPermission.FinanceManage,
                 PlatformPermission.Leads, PlatformPermission.Support, PlatformPermission.Logs, PlatformPermission.Impersonate,
                 PlatformPermission.ReferralsRead, PlatformPermission.ReferralsManage]),
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
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePlatformPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
