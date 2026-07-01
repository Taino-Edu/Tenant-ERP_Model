// =============================================================================
// NaturezaOperacao.cs — Regra de tributação reutilizável (estilo Bling)
// Produto referencia uma natureza em vez de repetir CFOP/CSOSN item a item.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Natureza de operação fiscal — agrupa CFOP e CSOSN sob uma descrição
/// reutilizável (ex: "Venda de mercadoria dentro do estado").
/// </summary>
[Table("naturezas_operacao")]
public class NaturezaOperacao
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150)]
    [Column("descricao")]
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Código Fiscal de Operações e Prestações (ex: "5102").</summary>
    [Required, MaxLength(4)]
    [Column("cfop")]
    public string Cfop { get; set; } = string.Empty;

    /// <summary>Código de Situação da Operação no Simples Nacional (ex: "102", "500").</summary>
    [MaxLength(3)]
    [Column("csosn")]
    public string? Csosn { get; set; }

    /// <summary>Se true, é sugerida como padrão ao cadastrar um novo produto.</summary>
    [Column("is_padrao")]
    public bool IsPadrao { get; set; } = false;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
