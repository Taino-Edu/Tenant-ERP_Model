using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>Registro local imutável de uma faixa de NFC-e inutilizada na SEFAZ.</summary>
[Table("inutilizacoes_fiscais")]
public class InutilizacaoFiscal
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("ano")]
    public int Ano { get; set; }

    [Column("serie")]
    public int Serie { get; set; }

    [Column("numero_inicial")]
    public int NumeroInicial { get; set; }

    [Column("numero_final")]
    public int NumeroFinal { get; set; }

    [Required, MaxLength(255), Column("justificativa")]
    public string Justificativa { get; set; } = string.Empty;

    [Required, MaxLength(30), Column("protocolo")]
    public string Protocolo { get; set; } = string.Empty;

    [Column("xml_retorno")]
    public string? XmlRetorno { get; set; }

    [Column("inutilizado_em")]
    public DateTime InutilizadoEm { get; set; } = DateTime.UtcNow;
}
