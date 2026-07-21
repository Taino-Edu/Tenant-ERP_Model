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

    /// <summary>Código de Situação da Operação no Simples Nacional.</summary>
    [MaxLength(3)]
    [Column("csosn")]
    public string? Csosn { get; set; }

    /// <summary>% de crédito de ICMS (pCredSN) — só usado quando Csosn = "101". Nos demais
    /// códigos este campo é ignorado.</summary>
    [Column("percentual_credito_sn")]
    public decimal? PercentualCreditoIcmsSn { get; set; }

    /// <summary>Origem da mercadoria conforme leiaute da NF-e/NFC-e (0 a 8).</summary>
    [Column("origem_mercadoria")]
    public int OrigemMercadoria { get; set; } = 0;

    /// <summary>Modalidade da base do ICMS-ST (0 a 6). Normalmente 4=MVA.</summary>
    [Column("modalidade_bc_st")]
    public int? ModalidadeBcSt { get; set; }

    [Column("percentual_mva_st")]
    public decimal? PercentualMvaSt { get; set; }

    [Column("percentual_reducao_bc_st")]
    public decimal? PercentualReducaoBcSt { get; set; }

    [Column("aliquota_icms_st")]
    public decimal? AliquotaIcmsSt { get; set; }

    /// <summary>Alíquota interna/interestadual da operação própria, deduzida do ICMS-ST.</summary>
    [Column("aliquota_icms_proprio")]
    public decimal? AliquotaIcmsProprio { get; set; }

    [Column("aliquota_fcp_st")]
    public decimal? AliquotaFcpSt { get; set; }

    /// <summary>Base/pauta fixa por unidade, em centavos, para modalidades diferentes de MVA.</summary>
    [Column("base_st_fixa_centavos")]
    public int? BaseStFixaEmCentavos { get; set; }

    /// <summary>Classificação IBS/CBS aplicável aos produtos desta natureza.</summary>
    [MaxLength(3)]
    [Column("ibs_cbs_cst")]
    public string IbsCbsCst { get; set; } = "000";

    [MaxLength(6)]
    [Column("ibs_cbs_class_trib")]
    public string IbsCbsClassTrib { get; set; } = "000001";

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
