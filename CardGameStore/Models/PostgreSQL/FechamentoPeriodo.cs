// =============================================================================
// FechamentoPeriodo.cs — Snapshot congelado de um período financeiro fechado
// (dia/semana/mês). Vive no schema de cada tenant, junto de Comanda/VendaAvulsa
// — não é dado cross-tenant, então não faz sentido no catálogo.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

public enum TipoFechamento
{
    Dia,
    Semana,
    Mes,
}

/// <summary>
/// Uma vez gravado, um fechamento não muda sozinho — mesmo que uma Comanda
/// daquele período seja editada depois. Corrigir requer rodar o fechamento
/// de novo pra aquela janela (upsert), não é recalculado implicitamente.
/// </summary>
[Table("fechamentos_periodo")]
public class FechamentoPeriodo
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tipo")]
    public TipoFechamento Tipo { get; set; }

    [Column("data_inicio")]
    public DateTime DataInicio { get; set; }

    [Column("data_fim")]
    public DateTime DataFim { get; set; }

    [Column("receita_comandas")]
    public long ReceitaComandas { get; set; }

    [Column("receita_avulsa")]
    public long ReceitaAvulsa { get; set; }

    [Column("custo_comandas")]
    public long CustoComandas { get; set; }

    [Column("custo_avulsa")]
    public long CustoAvulsa { get; set; }

    [Column("margem")]
    public long Margem { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
