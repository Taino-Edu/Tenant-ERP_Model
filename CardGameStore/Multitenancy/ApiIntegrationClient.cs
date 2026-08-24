using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

/// <summary>
/// Credencial servidor-a-servidor vinculada a um tenant. Vive no catalogo
/// global para poder ser validada sem trocar o schema da requisicao.
/// </summary>
[Table("api_integration_clients")]
public sealed class ApiIntegrationClient
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required, MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    [Column("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    [Column("secret_hash")]
    public string SecretHash { get; set; } = string.Empty;

    [Column("scopes")]
    public string[] Scopes { get; set; } = [];

    [Column("credential_version")]
    public int CredentialVersion { get; set; } = 1;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }
}
