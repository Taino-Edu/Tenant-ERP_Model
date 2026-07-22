// =============================================================================
// NotaFiscalEmitida.cs — Registro de NFC-e emitida (ou pendente de emissão)
// vinculada a uma Comanda ou Venda Avulsa.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

public enum NotaFiscalOrigem
{
    Comanda,
    VendaAvulsa
}

public enum NotaFiscalStatus
{
    PendenteEmissao,
    Autorizada,
    Rejeitada,
    Cancelada,

    /// <summary>Emitida em contingência offline (tpEmis=9) porque a SEFAZ estava
    /// inalcançável — já vale pro cliente (cupom liberado), mas ainda precisa ser
    /// retransmitida à SEFAZ (o retry automático faz isso) pra virar Autorizada de fato.</summary>
    AutorizadaContingencia,
}

/// <summary>
/// Uma NFC-e emitida (ou em tentativa de emissão) referente ao fechamento
/// de uma Comanda ou registro de Venda Avulsa. Guarda o XML autorizado
/// para exportação posterior ao contador.
/// </summary>
[Table("notas_fiscais_emitidas")]
public class NotaFiscalEmitida
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("origem")]
    public NotaFiscalOrigem Origem { get; set; }

    /// <summary>Id da Comanda de origem (quando Origem == Comanda).</summary>
    [Column("comanda_id")]
    public Guid? ComandaId { get; set; }

    /// <summary>Id da VendaAvulsa (quando Origem == VendaAvulsa).</summary>
    [Column("venda_avulsa_id")]
    public Guid? VendaAvulsaId { get; set; }

    [Column("status")]
    public NotaFiscalStatus Status { get; set; } = NotaFiscalStatus.PendenteEmissao;

    [Column("valor_total_em_centavos")]
    public int ValorTotalEmCentavos { get; set; }

    /// <summary>Snapshot dos valores aproximados exibidos ao consumidor.</summary>
    [Column("tributos_federais_em_centavos")]
    public int TributosFederaisEmCentavos { get; set; }

    [Column("tributos_estaduais_em_centavos")]
    public int TributosEstaduaisEmCentavos { get; set; }

    [Column("tributos_municipais_em_centavos")]
    public int TributosMunicipaisEmCentavos { get; set; }

    [MaxLength(500)]
    [Column("fontes_tributos")]
    public string? FontesTributos { get; set; }

    /// <summary>Snapshot JSON dos tributos aproximados de cada item, na ordem do XML.</summary>
    [Column("tributos_itens_json")]
    public string? TributosItensJson { get; set; }

    [Column("serie")]
    public int? Serie { get; set; }

    [Column("numero")]
    public int? Numero { get; set; }

    /// <summary>Chave de acesso de 44 dígitos, preenchida após autorização.</summary>
    [MaxLength(44)]
    [Column("chave_acesso")]
    public string? ChaveAcesso { get; set; }

    /// <summary>Protocolo de autorização retornado pela SEFAZ.</summary>
    [MaxLength(30)]
    [Column("protocolo")]
    public string? Protocolo { get; set; }

    [Column("autorizado_em")]
    public DateTime? AutorizadoEm { get; set; }

    [Column("motivo_rejeicao")]
    public string? MotivoRejeicao { get; set; }

    /// <summary>XML autorizado (com protNFe anexado) — usado na exportação ao contador.</summary>
    [Column("xml_autorizado")]
    public string? XmlAutorizado { get; set; }

    /// <summary>URL do QR Code (com hash do CSC), calculada pela lib fiscal no momento da
    /// autorização e persistida aqui — evita recalcular (e evita fórmula desatualizada) toda
    /// vez que o cupom é exibido.</summary>
    [Column("url_qrcode")]
    public string? UrlQrCode { get; set; }

    [Column("emitido_em")]
    public DateTime? EmitidoEm { get; set; }

    [Column("cancelado_em")]
    public DateTime? CanceladoEm { get; set; }

    /// <summary>Justificativa usada no evento de cancelamento (mín. 15 caracteres exigidos pela SEFAZ).</summary>
    [Column("justificativa_cancelamento")]
    public string? JustificativaCancelamento { get; set; }

    [MaxLength(30)]
    [Column("protocolo_cancelamento")]
    public string? ProtocoloCancelamento { get; set; }

    [Column("xml_evento_cancelamento")]
    public string? XmlEventoCancelamento { get; set; }

    [Column("erp_estornado_em")]
    public DateTime? ErpEstornadoEm { get; set; }

    [Column("erp_estorno_erro")]
    public string? ErpEstornoErro { get; set; }

    /// <summary>Preenchido somente quando o número foi formalmente inutilizado na SEFAZ.</summary>
    [Column("inutilizado_em")]
    public DateTime? InutilizadoEm { get; set; }

    [MaxLength(30)]
    [Column("protocolo_inutilizacao")]
    public string? ProtocoloInutilizacao { get; set; }

    /// <summary>Quantas vezes o reprocessamento (manual ou automático) já foi tentado — limita retries.</summary>
    [Column("tentativas_reprocessamento")]
    public int TentativasReprocessamento { get; set; } = 0;

    // ── Contingência offline (tpEmis=9) ────────────────────────────────────────
    // Persistidos na primeira tentativa em contingência pra reconstruir a MESMA chave
    // de acesso (já mostrada ao cliente no cupom) quando a retransmissão acontecer depois
    // — cNf/dhCont/tpEmis entram na fórmula da chave, então não podem mudar entre tentativas.

    /// <summary>Código numérico aleatório (cNf) usado no cálculo da chave — fixado na
    /// primeira tentativa em contingência pra a retransmissão gerar a chave idêntica.</summary>
    [Column("cnf_contingencia")]
    public int? CnfContingencia { get; set; }

    /// <summary>Momento (UTC) em que a contingência foi acionada — vira dhCont no XML.</summary>
    [Column("dh_contingencia")]
    public DateTime? DhContingencia { get; set; }

    /// <summary>Justificativa (xJust) da entrada em contingência, exigida pela SEFAZ.</summary>
    [Column("justificativa_contingencia")]
    public string? JustificativaContingencia { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
