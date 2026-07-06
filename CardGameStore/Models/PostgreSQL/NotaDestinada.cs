// =============================================================================
// NotaDestinada.cs — NF-e emitida por fornecedor contra o CNPJ da loja,
// descoberta via SEFAZ DFe Distribuição (Manifestação do Destinatário).
// Pipeline: resumo → ciencia → xml_baixado → contas_geradas
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>Status do pipeline de uma nota destinada.</summary>
public static class NotaDestinadaStatus
{
    /// <summary>Só o resumo (resNFe) chegou via distDFe — aguardando manifestar ciência.</summary>
    public const string Resumo = "resumo";
    /// <summary>Ciência da Operação registrada na SEFAZ — aguardando o XML completo ficar disponível.</summary>
    public const string Ciencia = "ciencia";
    /// <summary>XML completo (procNFe) baixado — aguardando geração das contas a pagar.</summary>
    public const string XmlBaixado = "xml_baixado";
    /// <summary>Contas a pagar geradas em external_transactions — pipeline concluído.</summary>
    public const string ContasGeradas = "contas_geradas";
    /// <summary>NF-e cancelada pelo emitente (cSitNFe=3 ou evento 110111).</summary>
    public const string Cancelada = "cancelada";
}

[Table("notas_destinadas")]
public class NotaDestinada
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Chave de acesso da NF-e (44 dígitos) — única.</summary>
    [Required, MaxLength(44)]
    [Column("chave_acesso")]
    public string ChaveAcesso { get; set; } = string.Empty;

    /// <summary>NSU em que o documento apareceu na distribuição.</summary>
    [Column("nsu")]
    public long Nsu { get; set; }

    [MaxLength(14)]
    [Column("emitente_cnpj")]
    public string? EmitenteCnpj { get; set; }

    [MaxLength(150)]
    [Column("emitente_nome")]
    public string? EmitenteNome { get; set; }

    [Column("valor", TypeName = "numeric(12,2)")]
    public decimal Valor { get; set; }

    [Column("data_emissao")]
    public DateTime? DataEmissao { get; set; }

    /// <summary>cSitNFe do resumo: 1=autorizada, 2=denegada, 3=cancelada.</summary>
    [Column("situacao")]
    public int Situacao { get; set; } = 1;

    [Required, MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = NotaDestinadaStatus.Resumo;

    /// <summary>Protocolo do evento de Ciência da Operação registrado na SEFAZ.</summary>
    [MaxLength(30)]
    [Column("ciencia_protocolo")]
    public string? CienciaProtocolo { get; set; }

    [Column("ciencia_em")]
    public DateTime? CienciaEm { get; set; }

    /// <summary>XML completo (procNFe) — fonte das duplicatas de cobrança.</summary>
    [Column("xml_proc")]
    public string? XmlProc { get; set; }

    /// <summary>Quantas contas a pagar foram geradas em external_transactions.</summary>
    [Column("contas_geradas")]
    public int ContasGeradas { get; set; }

    /// <summary>Último erro do pipeline (manifestação/download/parse) — null quando ok.</summary>
    [MaxLength(500)]
    [Column("erro")]
    public string? Erro { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
