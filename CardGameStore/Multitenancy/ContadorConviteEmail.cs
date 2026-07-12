// =============================================================================
// ContadorConviteEmail.cs — "Convite cego": o lojista convida um e-mail que
// ainda não tem conta de contador. Fica guardado aqui até o contador se
// cadastrar com esse e-mail em /contador/cadastro — nesse momento o vínculo
// Approved é criado automaticamente e esta linha é consumida (apagada).
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

[Table("contador_convites_email")]
public class ContadorConviteEmail
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
