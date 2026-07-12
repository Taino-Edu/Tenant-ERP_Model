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

/// <summary>Status de pagamento do tenant — rastreio manual pelo dono da plataforma
/// (ciclo 1 de billing, sem gateway de pagamento integrado ainda).</summary>
public enum TenantPaymentStatus
{
    Pago,
    Atrasado,
    Isento,
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

    /// <summary>Nome do plano contratado — texto livre (sem enum fixo, pricing ainda não fechado).</summary>
    [Required, MaxLength(63)]
    [Column("plan_name")]
    public string PlanName { get; set; } = "Completo";

    [Column("payment_status")]
    public TenantPaymentStatus PaymentStatus { get; set; } = TenantPaymentStatus.Pago;

    /// <summary>Módulos pagos habilitados pra este tenant (ex: "fiscal"). Módulos que são
    /// fundação do sistema (ex: estoque) não entram aqui — ver RequireModuleAttribute.</summary>
    [Column("enabled_modules")]
    public string[] EnabledModules { get; set; } = new[] { "fiscal" };

    /// <summary>Cópia denormalizada de SiteConfig.SiteName do schema deste tenant — mantida em
    /// sincronia por SiteConfigController.SaveConfig. Existe só pra o diretório público de lojas
    /// (institucional) não precisar trocar de schema por tenant a cada carregamento; a fonte de
    /// verdade continua sendo o SiteConfig do próprio tenant.</summary>
    [MaxLength(100)]
    [Column("display_name")]
    public string? DisplayName { get; set; }

    /// <summary>Cópia denormalizada de SiteConfig.LogoUrl — mesmo motivo/mesma sincronia de DisplayName.</summary>
    [MaxLength(300)]
    [Column("logo_url")]
    public string? LogoUrl { get; set; }
}
