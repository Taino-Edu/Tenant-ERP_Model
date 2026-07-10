// =============================================================================
// Tenant.cs — Catálogo de tenants (schema-per-tenant).
// Vive no CatalogDbContext, sempre no schema "public" — resolver o schema de
// um tenant a partir do slug não pode depender do schema ainda não resolvido.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

public enum TenantStatus
{
    Active,
    Suspended,
}

[Table("tenants")]
public class Tenant
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Subdomínio do tenant (ex: "loja-maikon" em loja-maikon.2esysten.com.br).</summary>
    [Required, MaxLength(63)]
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Nome do schema Postgres dedicado a este tenant (limite de 63 bytes do Postgres).</summary>
    [Required, MaxLength(63)]
    [Column("schema_name")]
    public string SchemaName { get; set; } = string.Empty;

    [Column("status")]
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
