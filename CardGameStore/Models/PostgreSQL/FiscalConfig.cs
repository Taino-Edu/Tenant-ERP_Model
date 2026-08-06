// =============================================================================
// FiscalConfig.cs — Configuração fiscal da loja para emissão de NFC-e
// Singleton lógico: uma única linha representa a empresa emitente (a loja).
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

public enum RegimeTributario
{
    SimplesNacional,
    LucroPresumido,
    LucroReal
}

public enum AmbienteFiscal
{
    Homologacao,
    Producao
}

/// <summary>
/// Anexo da LC 123/2006 usado na apuração do Simples Nacional. Define qual
/// tabela de faixas/parcela a deduzir entra no cálculo da alíquota efetiva —
/// não muda nada na emissão da NFC-e, é parâmetro só de apuração.
/// </summary>
public enum AnexoSimplesNacional
{
    /// <summary>Comércio.</summary>
    I,
    /// <summary>Indústria.</summary>
    II,
    /// <summary>Serviços em geral (e Anexo V com fator R ≥ 28%).</summary>
    III,
    /// <summary>Construção civil e serviços do §5º-C (CPP recolhida fora do DAS).</summary>
    IV,
    /// <summary>Serviços do §5º-I (fator R &lt; 28%).</summary>
    V,
}

/// <summary>
/// Configuração fiscal da empresa emitente e do certificado digital A1
/// usado para assinar e transmitir NFC-e à SEFAZ via DFe.NET.
/// </summary>
[Table("fiscal_config")]
public class FiscalConfig
{
    /// <summary>
    /// ID fixo — esta tabela é um singleton lógico (uma só linha, a config da empresa emitente).
    /// Usar sempre este ID (via FindAsync) em vez de FirstOrDefaultAsync, para que a PK
    /// já rejeite qualquer segunda inserção concorrente.
    /// </summary>
    public static readonly Guid SingletonId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Key]
    [Column("id")]
    public Guid Id { get; set; } = SingletonId;

    [Required, MaxLength(18)]
    [Column("cnpj")]
    public string Cnpj { get; set; } = string.Empty;

    /// <summary>Razão social do emitente — obrigatório no XML (emit.xNome).</summary>
    [MaxLength(150)]
    [Column("razao_social")]
    public string? RazaoSocial { get; set; }

    [MaxLength(20)]
    [Column("inscricao_estadual")]
    public string? InscricaoEstadual { get; set; }

    // -------------------------------------------------------------------------
    // Endereço do estabelecimento — obrigatório no XML da NFC-e (emit.enderEmit)
    // -------------------------------------------------------------------------

    [MaxLength(150)]
    [Column("logradouro")]
    public string? Logradouro { get; set; }

    [MaxLength(20)]
    [Column("numero")]
    public string? Numero { get; set; }

    [MaxLength(100)]
    [Column("complemento")]
    public string? Complemento { get; set; }

    [MaxLength(100)]
    [Column("bairro")]
    public string? Bairro { get; set; }

    /// <summary>Código do município no padrão IBGE (7 dígitos) — exigido no XML, não o nome.</summary>
    [MaxLength(7)]
    [Column("codigo_municipio_ibge")]
    public string? CodigoMunicipioIbge { get; set; }

    [MaxLength(100)]
    [Column("municipio")]
    public string? Municipio { get; set; }

    /// <summary>Sigla da UF (ex: "SP") — convertida para o enum Estado do DFe.NET na emissão.</summary>
    [MaxLength(2)]
    [Column("uf")]
    public string? Uf { get; set; }

    [MaxLength(9)]
    [Column("cep")]
    public string? Cep { get; set; }

    /// <summary>Id do Código de Segurança do Contribuinte, cadastrado na SEFAZ — usado no QR Code da NFC-e.</summary>
    [MaxLength(10)]
    [Column("csc_id")]
    public string? CscId { get; set; }

    /// <summary>Token do CSC, criptografado com EncryptionService (M14) — antes ficava em claro;
    /// permite gerar QR Codes válidos em nome da loja se o banco vazar. Nunca exposto em resposta
    /// de API; decriptado só na hora de montar o QR Code (NfceEmissionService).</summary>
    [Column("csc_token_encrypted")]
    public string? CscTokenEncrypted { get; set; }

    [Column("regime_tributario")]
    public RegimeTributario RegimeTributario { get; set; } = RegimeTributario.SimplesNacional;

    // ── Perfil IBS/CBS (RTC-001) ──────────────────────────────────────────────
    // Duas condições que o regime declarado NÃO revela e que o sistema não tem
    // como inferir — vêm do contador. Selecionam qual faixa do catálogo de regras
    // (CatalogoRegrasIbsCbs) se aplica ao contribuinte.

    /// <summary>Optante do Simples que excedeu o sublimite estadual no período.</summary>
    [Column("excedeu_sublimite_simples")]
    public bool ExcedeuSublimiteSimples { get; set; }

    /// <summary>Optante do Simples que fez a opção pelo regime regular de IBS/CBS.</summary>
    [Column("optou_regime_regular_ibs_cbs")]
    public bool OptouRegimeRegularIbsCbs { get; set; }

    // -------------------------------------------------------------------------
    // Parâmetros de apuração (portal do contador) — nada aqui entra no XML da
    // NFC-e; servem só pra calcular DAS do Simples e o comparativo com Lucro
    // Presumido, que dependem de dados que o sistema não tem como inferir
    // (anexo da atividade, folha de pagamento, alíquotas de ICMS/ISS locais).
    // -------------------------------------------------------------------------

    [Column("anexo_simples")]
    public AnexoSimplesNacional AnexoSimples { get; set; } = AnexoSimplesNacional.I;

    /// <summary>Folha de pagamento dos últimos 12 meses (com encargos), em centavos — numerador do fator R.</summary>
    [Column("folha_pagamento12m_em_centavos")]
    public long FolhaPagamento12mEmCentavos { get; set; }

    /// <summary>Folha mensal com encargos, em centavos — base do INSS patronal no Lucro Presumido.</summary>
    [Column("folha_pagamento_mensal_em_centavos")]
    public long FolhaPagamentoMensalEmCentavos { get; set; }

    /// <summary>% de presunção do IRPJ no Lucro Presumido (8 comércio/indústria, 32 serviços).</summary>
    [Column("percentual_presuncao_irpj")]
    public decimal PercentualPresuncaoIrpj { get; set; } = 8m;

    /// <summary>% de presunção da CSLL no Lucro Presumido (12 comércio/indústria, 32 serviços).</summary>
    [Column("percentual_presuncao_csll")]
    public decimal PercentualPresuncaoCsll { get; set; } = 12m;

    /// <summary>Alíquota média de ICMS aplicável fora do Simples — varia por UF e produto, então o contador informa.</summary>
    [Column("aliquota_icms_percentual")]
    public decimal AliquotaIcmsPercentual { get; set; }

    /// <summary>Alíquota de ISS do município (2% a 5%) para a parcela de serviços.</summary>
    [Column("aliquota_iss_percentual")]
    public decimal AliquotaIssPercentual { get; set; }

    [Column("ambiente")]
    public AmbienteFiscal Ambiente { get; set; } = AmbienteFiscal.Homologacao;

    [Column("serie_nfce")]
    public int SerieNfce { get; set; } = 1;

    [Column("proximo_numero_nfce")]
    public int ProximoNumeroNfce { get; set; } = 1;

    /// <summary>Email do contador — destino do ZIP mensal de XMLs autorizados/cancelados.</summary>
    [MaxLength(200)]
    [Column("email_contador")]
    public string? EmailContador { get; set; }

    /// <summary>Certificado .pfx (Base64) criptografado com EncryptionService.</summary>
    [Column("certificado_pfx_encrypted")]
    public string? CertificadoPfxEncrypted { get; set; }

    /// <summary>Senha do certificado, criptografada com EncryptionService.</summary>
    [Column("certificado_senha_encrypted")]
    public string? CertificadoSenhaEncrypted { get; set; }

    /// <summary>Data de validade (NotAfter) extraída do certificado X509 no momento do upload.</summary>
    [Column("certificado_validade")]
    public DateTime? CertificadoValidade { get; set; }

    [Column("certificado_uploaded_at")]
    public DateTime? CertificadoUploadedAt { get; set; }

    /// <summary>
    /// Menor limiar (30/15/7/1 dias) já alertado para a validade atual do certificado.
    /// Evita reenviar o mesmo alerta todo dia até o vencimento.
    /// </summary>
    [Column("certificado_ultimo_alerta_limiar")]
    public int? CertificadoUltimoAlertaLimiar { get; set; }

    /// <summary>Última vez que o ZIP mensal de XMLs foi enviado automaticamente ao contador.</summary>
    [Column("ultimo_envio_mensal_xmls")]
    public DateTime? UltimoEnvioMensalXmls { get; set; }

    /// <summary>
    /// Último NSU consumido do DFe Distribuição (notas destinadas ao CNPJ da loja).
    /// A próxima consulta continua deste ponto — nunca zerar em produção, senão a SEFAZ
    /// reenvia todo o histórico e pode bloquear por consumo indevido (cStat 656).
    /// </summary>
    [Column("dist_ultimo_nsu")]
    public long DistUltimoNsu { get; set; }

    /// <summary>
    /// Formas de pagamento (CSV: "Pix,Dinheiro,...") que emitem NFC-e automaticamente ao
    /// fechar a venda, sem perguntar. Vazio por padrão — a loja não quer que o sistema
    /// emita nota sem antes perguntar; o admin decide explicitamente a cada fechamento
    /// via checkbox, que só vem pré-marcado para as formas listadas aqui.
    /// </summary>
    [Column("formas_pagamento_auto_emissao")]
    public string FormasPagamentoAutoEmissao { get; set; } = string.Empty;

    /// <summary>Token da API De Olho no Imposto/IBPT, sempre criptografado em repouso.</summary>
    [Column("ibpt_token_encrypted")]
    public string? IbptTokenEncrypted { get; set; }

    [Column("ibpt_auto_sync_enabled")]
    public bool IbptAutoSyncEnabled { get; set; }

    [Column("ibpt_ultima_sincronizacao")]
    public DateTime? IbptUltimaSincronizacao { get; set; }

    [MaxLength(30)]
    [Column("ibpt_ultima_versao")]
    public string? IbptUltimaVersao { get; set; }

    [Column("ibpt_vigencia_inicio")]
    public DateTime? IbptVigenciaInicio { get; set; }

    [Column("ibpt_vigencia_fim")]
    public DateTime? IbptVigenciaFim { get; set; }

    [MaxLength(500)]
    [Column("ibpt_ultimo_erro")]
    public string? IbptUltimoErro { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public bool CertificadoConfigurado => !string.IsNullOrWhiteSpace(CertificadoPfxEncrypted);

    [NotMapped]
    public bool IbptConfigurado => !string.IsNullOrWhiteSpace(IbptTokenEncrypted);
}
