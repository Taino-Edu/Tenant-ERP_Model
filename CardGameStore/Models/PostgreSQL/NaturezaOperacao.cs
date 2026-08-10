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

    /// <summary>
    /// Código de Situação da Operação no Simples Nacional. Usado quando a loja
    /// está no Simples (CRT=1); ignorado no regime normal, que usa <see cref="Cst"/>.
    /// </summary>
    [MaxLength(3)]
    [Column("csosn")]
    public string? Csosn { get; set; }

    /// <summary>
    /// CST do ICMS (00, 10, 20, 30, 40, 41, 50, 51, 60, 70, 90) — o par do CSOSN
    /// para Lucro Presumido/Real. São dois campos e não um só porque a mesma
    /// natureza pode ser reaproveitada se a empresa mudar de regime, e porque o
    /// XML rejeita CSOSN com CRT=3 e CST com CRT=1: guardar os dois separados
    /// deixa a incompatibilidade impossível de acontecer por descuido.
    /// </summary>
    [MaxLength(2)]
    [Column("cst")]
    public string? Cst { get; set; }

    /// <summary>% de redução da base de cálculo da operação própria — CST 20 e 70.</summary>
    [Column("percentual_reducao_bc")]
    public decimal? PercentualReducaoBc { get; set; }

    /// <summary>Alíquota do FCP sobre a operação própria (não o ST) — CST 00, 20, 70 e 90.</summary>
    [Column("aliquota_fcp")]
    public decimal? AliquotaFcp { get; set; }

    /// <summary>
    /// Base de cálculo e valor do ICMS-ST retido anteriormente, informados no
    /// CST 60. Opcionais: a SEFAZ aceita o 60 sem eles, e boa parte do varejo
    /// não recebe esse dado do fornecedor.
    /// </summary>
    [Column("base_st_retida_centavos")]
    public int? BaseStRetidaEmCentavos { get; set; }

    [Column("valor_st_retido_centavos")]
    public int? ValorStRetidoEmCentavos { get; set; }

    // ── PIS/COFINS ───────────────────────────────────────────────────────────
    // No Simples ambos saem como CST 99 zerado (o tributo está dentro do DAS).
    // Fora dele, cada item precisa de CST próprio e alíquota: 0,65%/3% no regime
    // cumulativo (Presumido) e 1,65%/7,6% no não-cumulativo (Real).

    /// <summary>CST do PIS (01, 02, 04, 06, 07, 08, 09, 49, 99…). Null = padrão do regime.</summary>
    [MaxLength(2)]
    [Column("cst_pis")]
    public string? CstPis { get; set; }

    /// <summary>CST da COFINS. Null = padrão do regime.</summary>
    [MaxLength(2)]
    [Column("cst_cofins")]
    public string? CstCofins { get; set; }

    /// <summary>Alíquota de PIS em %. Null = padrão do regime da loja.</summary>
    [Column("aliquota_pis")]
    public decimal? AliquotaPis { get; set; }

    /// <summary>Alíquota de COFINS em %. Null = padrão do regime da loja.</summary>
    [Column("aliquota_cofins")]
    public decimal? AliquotaCofins { get; set; }

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
