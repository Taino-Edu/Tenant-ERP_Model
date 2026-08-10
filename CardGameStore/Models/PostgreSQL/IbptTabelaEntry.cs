// =============================================================================
// IbptTabelaEntry.cs — IBPT-002: a tabela de tributos aproximados, local.
//
// O que isto substitui: uma chamada HTTP por NCM, DENTRO da requisição, com 15s
// de timeout cada. Um catálogo com muitos NCMs distintos e a API lenta estoura
// qualquer proxy antes de terminar — e foi assim que o "Sincronizar produtos
// agora" virou 500 em produção (trace 0HNNI4IMEL3EK).
//
// A inversão é simples: um job diário conversa com o IBPT e guarda o resultado
// aqui; cadastrar produto passa a ser LEITURA LOCAL. A rede sai do caminho do
// usuário e vai para um lugar onde demorar não machuca ninguém.
//
// Consequência que importa tanto quanto a latência: a API do IBPT fora do ar por
// um dia deixa de impedir cadastrar produto e emitir nota. A última tabela
// conhecida continua valendo, com a vigência dela registrada — que é exatamente
// como o dado fiscal deve se comportar.
//
// Escopo: uma linha por (NCM, UF, origem). Vive no schema do tenant, alimentada
// com o token do próprio tenant — a tabela do IBPT é licenciada ao CNPJ que a
// obteve, e servir uma loja com credencial de outra não é decisão de engenharia.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("ibpt_tabela")]
public class IbptTabelaEntry
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(8)]
    [Column("ncm")]
    public string Ncm { get; set; } = string.Empty;

    [Required, MaxLength(2)]
    [Column("uf")]
    public string Uf { get; set; } = string.Empty;

    /// <summary>
    /// A alíquota federal difere entre mercadoria nacional e importada, e a
    /// origem vem da natureza de operação do produto — por isso faz parte da
    /// chave, não é detalhe de apresentação.
    /// </summary>
    [Column("importado")]
    public bool Importado { get; set; }

    [Column("percentual_federal")]
    public decimal PercentualFederal { get; set; }

    [Column("percentual_estadual")]
    public decimal PercentualEstadual { get; set; }

    [Column("percentual_municipal")]
    public decimal PercentualMunicipal { get; set; }

    [MaxLength(100)]
    [Column("fonte")]
    public string? Fonte { get; set; }

    [MaxLength(30)]
    [Column("versao")]
    public string? Versao { get; set; }

    [MaxLength(50)]
    [Column("chave")]
    public string? Chave { get; set; }

    [Column("vigencia_inicio")]
    public DateTime? VigenciaInicio { get; set; }

    [Column("vigencia_fim")]
    public DateTime? VigenciaFim { get; set; }

    /// <summary>Quando esta linha foi obtida do IBPT. Distinto da vigência: diz
    /// há quanto tempo o sistema não confirma o dado, não até quando ele vale.</summary>
    [Column("atualizado_em")]
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>Vigência vencida não apaga a linha — o produto continua podendo
    /// ser cadastrado, e é a emissão que decide o que fazer com dado vencido.</summary>
    [NotMapped]
    public bool Vencida => VigenciaFim.HasValue && VigenciaFim.Value.Date < DateTime.UtcNow.Date;
}
