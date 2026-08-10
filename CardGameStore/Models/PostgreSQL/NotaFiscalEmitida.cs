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

    /// <summary>
    /// O documento foi transmitido e a resposta se perdeu (RES-001). Não se sabe
    /// se a SEFAZ autorizou: presumir falha e emitir outro documento pela mesma
    /// venda é o que produz duas NFC-e para um único fato gerador.
    ///
    /// Estado transitório e resolvível: a consulta da chave original decide o
    /// destino (autorizada, rejeitada ou inexistente na base da SEFAZ). Enquanto
    /// não resolve, o número está reservado e NÃO pode ser inutilizado nem
    /// reaproveitado, e a venda de origem não pode ser editada.
    /// </summary>
    ResultadoIncerto,
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

    [Column("motivo_rejeicao")]
    public string? MotivoRejeicao { get; set; }

    /// <summary>XML autorizado (com protNFe anexado) — usado na exportação ao contador.</summary>
    [Column("xml_autorizado")]
    public string? XmlAutorizado { get; set; }

    /// <summary>
    /// XML assinado entregue ao consumidor em contingência offline (tpEmis=9),
    /// SEM protNFe — é o documento que o cliente já levou, antes de a SEFAZ
    /// voltar. Guardado à parte do XmlAutorizado por dois motivos (RES-002):
    ///
    ///   • é o único documento fiscal existente enquanto a retransmissão não
    ///     acontece, então o DANFE de uma nota em contingência precisa sair
    ///     daqui — sem isso a via impressa não teria fonte imutável;
    ///   • a retransmissão tem que enviar exatamente ESTE documento assinado à
    ///     SEFAZ, não remontar um novo — remontar mudaria a assinatura e a via
    ///     do consumidor deixaria de conferir com a autorizada.
    /// </summary>
    [Column("xml_contingencia")]
    public string? XmlContingencia { get; set; }

    /// <summary>URL do QR Code (com hash do CSC), calculada pela lib fiscal no momento da
    /// autorização e persistida aqui — evita recalcular (e evita fórmula desatualizada) toda
    /// vez que o cupom é exibido.</summary>
    [Column("url_qrcode")]
    public string? UrlQrCode { get; set; }

    [Column("emitido_em")]
    public DateTime? EmitidoEm { get; set; }

    /// <summary>Momento (UTC) da confirmação de autorização pela SEFAZ (cStat 100) — distinto
    /// de <see cref="EmitidoEm"/>, que preserva o momento real da venda mesmo em contingência.
    /// A janela legal de cancelamento (F14) conta a partir daqui: uma nota autorizada
    /// tardiamente (retransmissão de contingência horas depois) precisa nascer com a janela
    /// cheia, não já expirada por causa do EmitidoEm antigo.</summary>
    [Column("autorizado_em")]
    public DateTime? AutorizadoEm { get; set; }

    [Column("cancelado_em")]
    public DateTime? CanceladoEm { get; set; }

    /// <summary>Justificativa usada no evento de cancelamento (mín. 15 caracteres exigidos pela SEFAZ).</summary>
    [Column("justificativa_cancelamento")]
    public string? JustificativaCancelamento { get; set; }

    /// <summary>Protocolo (nProt) devolvido pela SEFAZ no evento de cancelamento — distinto do
    /// protocolo de autorização em <see cref="Protocolo"/>. Exigido como prova documental do
    /// cancelamento (guarda de 5 anos, ZIP do contador).</summary>
    [MaxLength(30)]
    [Column("protocolo_cancelamento")]
    public string? ProtocoloCancelamento { get; set; }

    /// <summary>XML do procEventoNFe assinado/autorizado pela SEFAZ.</summary>
    [Column("xml_evento_cancelamento")]
    public string? XmlEventoCancelamento { get; set; }

    [Column("erp_estornado_em")]
    public DateTime? ErpEstornadoEm { get; set; }

    [Column("erp_estorno_erro")]
    public string? ErpEstornoErro { get; set; }

    /// <summary>Preenchido quando o número desta nota foi formalmente inutilizado (nota rejeitada).</summary>
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

    // ── Tentativa em aberto e resultado incerto (RES-001) ──────────────────────
    // Gravados ANTES de o documento sair pela rede. Se a resposta se perder, é
    // por estes campos que se descobre o que foi enviado e qual chave consultar
    // na SEFAZ — sem eles, "não recebi resposta" e "não foi enviado" seriam
    // indistinguíveis e a única saída seria emitir um segundo documento.

    /// <summary>Identificador da tentativa de transmissão em aberto. Muda a cada
    /// envio e é limpo quando o destino do documento fica conhecido.</summary>
    [Column("tentativa_id")]
    public Guid? TentativaId { get; set; }

    /// <summary>XML assinado exatamente como foi transmitido na tentativa em aberto.
    /// É o que permite montar o <c>nfeProc</c> quando a autorização é recuperada
    /// depois, por consulta da chave, em vez de remontar o documento.</summary>
    [Column("xml_tentativa")]
    public string? XmlTentativa { get; set; }

    /// <summary>Momento (UTC) em que a resposta da SEFAZ se perdeu e a nota entrou
    /// em <see cref="NotaFiscalStatus.ResultadoIncerto"/>.</summary>
    [Column("resultado_incerto_em")]
    public DateTime? ResultadoIncertoEm { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
