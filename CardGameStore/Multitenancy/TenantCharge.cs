// =============================================================================
// TenantCharge.cs — Cobranças da plataforma contra cada loja.
//
// Vive no CatalogDbContext (schema "public"), não no schema do tenant: é dado
// DA PLATAFORMA sobre o cliente, não dado do cliente. O lojista nunca deve ler
// nem escrever aqui — se isso morasse no schema dele, qualquer bug de
// isolamento viraria vazamento do nosso financeiro.
//
// Por que uma tabela e não campos calculados no Tenant: "quanto cada loja já
// pagou" e "quanto está em aberto" são perguntas sobre HISTÓRICO. O Tenant tem
// PaymentStatus, que é um único flag do agora — não responde "quanto entrou em
// março" nem "esse cliente atrasa sempre".
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

public enum TenantChargeKind
{
    /// <summary>Taxa de implantação — cobrada uma vez, na contratação.</summary>
    Implantacao,

    /// <summary>Mensalidade de um mês de competência.</summary>
    Mensalidade,
}

[Table("tenant_charges")]
public class TenantCharge
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("kind")]
    public TenantChargeKind Kind { get; set; }

    /// <summary>Valor cobrado, em reais.
    ///
    /// É uma CÓPIA do preço vigente no momento em que a cobrança foi gerada, não
    /// uma referência a Tenant.MonthlyPrice. Reajustar o plano de um cliente não
    /// pode reescrever o que já foi cobrado dele em meses passados — isso não é
    /// denormalização preguiçosa, é o requisito: registro financeiro emitido não
    /// muda retroativamente.</summary>
    [Precision(10, 2)]
    [Column("amount")]
    public decimal Amount { get; set; }

    /// <summary>Mês de competência, sempre normalizado pro dia 1 às 00:00 UTC
    /// (ver PlatformBillingService.NormalizarCompetencia). É o que distingue a
    /// mensalidade de março da de abril, e o que a unique index usa pra impedir
    /// cobrar o mesmo mês duas vezes.</summary>
    [Column("reference_month")]
    public DateTime ReferenceMonth { get; set; }

    [Column("due_date")]
    public DateTime DueDate { get; set; }

    /// <summary>Quando foi efetivamente paga. Null = em aberto. Esta data (e não
    /// DueDate) é o que separa receita REALIZADA de receita esperada.</summary>
    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [MaxLength(300)]
    [Column("notes")]
    public string? Notes { get; set; }

    // -------------------------------------------------------------------------
    // Cobrança no gateway (RB-01)
    //
    // Null nos três campos = cobrança que nunca foi registrada em gateway
    // nenhum: implantação negociada no boleto, mês de cortesia, ou o histórico
    // inteiro anterior à automação. Baixa manual continua funcionando pra essas,
    // e é por isso que nada aqui é obrigatório.
    // -------------------------------------------------------------------------

    /// <summary>Qual gateway emitiu ("asaas", ...). Fica junto do id externo
    /// porque id de gateway só é único DENTRO do gateway — se um dia rodarmos
    /// dois em paralelo (a taxa do Asaas em assinatura ainda está em aberto),
    /// buscar só pelo id casaria a cobrança errada.</summary>
    [MaxLength(20)]
    [Column("gateway")]
    public string? Gateway { get; set; }

    /// <summary>Id da cobrança no gateway. É por ele que o webhook encontra esta
    /// linha — o payload do gateway não sabe nada de TenantCharge.Id.</summary>
    [MaxLength(100)]
    [Column("external_charge_id")]
    public string? ExternalChargeId { get; set; }

    /// <summary>Link de pagamento (fatura/checkout) devolvido pelo gateway, pra
    /// mandar ao lojista sem precisar entrar no painel do gateway.</summary>
    [MaxLength(500)]
    [Column("payment_url")]
    public string? PaymentUrl { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
