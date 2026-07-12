// =============================================================================
// ContadorTenantLink.cs — Vínculo N:N entre ContadorAccount e Tenant.
// Um contador pode atender vários tenants; um tenant pode ter mais de um
// contador vinculado (ex: troca de escritório). Nasce Approved quando o
// lojista convida por e-mail; nasce Pending quando o contador solicita acesso
// por slug e precisa ser aprovado pelo lojista em /admin/fiscal.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

public enum ContadorLinkStatus
{
    Pending,
    Approved,
}

[Table("contador_tenant_links")]
public class ContadorTenantLink
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("contador_account_id")]
    public Guid ContadorAccountId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("status")]
    public ContadorLinkStatus Status { get; set; } = ContadorLinkStatus.Pending;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
