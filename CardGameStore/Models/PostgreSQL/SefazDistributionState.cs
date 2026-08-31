using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

/// <summary>
/// Estado operacional do NFeDistribuicaoDFe. A tabela vive dentro do schema do
/// tenant; CNPJ + ambiente completam a identidade e impedem reaproveitar NSU ou
/// cooldown quando a empresa alterna homologação/produção.
/// </summary>
[Table("sefaz_distribution_state")]
public class SefazDistributionState
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(18), Column("cnpj")]
    public string Cnpj { get; set; } = string.Empty;

    [Column("ambiente")]
    public AmbienteFiscal Ambiente { get; set; }

    [Column("ultimo_nsu")]
    public long UltimoNsu { get; set; }

    [Column("proxima_consulta_em")]
    public DateTime? ProximaConsultaEm { get; set; }

    [Column("bloqueado_ate")]
    public DateTime? BloqueadoAte { get; set; }

    [Column("sync_lock_id")]
    public Guid? SyncLockId { get; set; }

    [Column("sync_lock_ate")]
    public DateTime? SyncLockAte { get; set; }

    [Column("consulta_pontual_janela_inicio")]
    public DateTime? ConsultaPontualJanelaInicio { get; set; }

    [Column("consulta_pontual_quantidade")]
    public int ConsultaPontualQuantidade { get; set; }

    /// <summary>Quantos 656 (consumo indevido) consecutivos este CNPJ levou.
    /// Zera em qualquer consulta aceita pela SEFAZ.
    ///
    /// Existe porque o cooldown fixo de 65 minutos não distinguia "a SEFAZ me
    /// barrou uma vez" de "a SEFAZ me barra sempre". No segundo caso — que é o
    /// que se observou em produção, com ultNSU parado em 0 — o job reentrava no
    /// mesmo bloqueio a cada 65 minutos indefinidamente, gerando log e consumo
    /// sem nunca progredir. Com o contador, o intervalo cresce e o problema para
    /// de se repetir sozinho enquanto a causa não é resolvida.</summary>
    [Column("bloqueios_consecutivos")]
    public int BloqueiosConsecutivos { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
