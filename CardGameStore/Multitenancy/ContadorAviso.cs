// =============================================================================
// ContadorAviso.cs — Mural de recados entre lojista e contador, por vínculo.
// Vive no catálogo (schema "public"), preso a um ContadorTenantLink específico
// — não faz sentido um aviso existir sem o vínculo que o originou.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

[Table("contador_avisos")]
public class ContadorAviso
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("contador_tenant_link_id")]
    public Guid ContadorTenantLinkId { get; set; }

    [Column("autor")]
    [MaxLength(20)]
    public string Autor { get; set; } = null!; // "Contador" ou "Lojista"

    [Column("mensagem")]
    public string Mensagem { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
