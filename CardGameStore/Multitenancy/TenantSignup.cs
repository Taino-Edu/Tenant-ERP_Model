// =============================================================================
// TenantSignup.cs — Pedido de criação de loja feito pelo próprio lojista no
// site, esperando a confirmação do e-mail.
//
// Existe como linha própria, e não como Tenant com status "pendente", porque
// um pedido não confirmado não pode custar nada: sem schema, sem migrations,
// sem admin. Quem digita um e-mail que não é seu não cria loja nenhuma — só
// uma linha que expira em 24 horas. O schema só nasce quando alguém prova que
// lê aquela caixa de entrada.
//
// O token do link fica só em hash (SHA-256, como o convite de parceiro).
//
// Senha não passa por aqui. O pedido é anônimo: se a senha fosse escolhida nele,
// quem digitasse o e-mail de outra pessoa definiria a senha da loja que essa
// pessoa criaria ao clicar no link, e entraria nela depois. A senha só é pedida
// na confirmação, por quem abriu o e-mail, e vai direto para o admin.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Multitenancy;

[Table("tenant_signups")]
public class TenantSignup
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>SHA-256 (hex) do token do link. O token em si só existe no e-mail.</summary>
    [Required, MaxLength(64)]
    [Column("token_hash")]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Normalizado (trim + minúsculas). Vira o login do admin e o e-mail de cobrança.</summary>
    [Required, MaxLength(255)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    [Column("owner_name")]
    public string OwnerName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    [Column("store_name")]
    public string StoreName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Nome canônico de um plano de TenantProvisioningService.TabelaPrecos.</summary>
    [Required, MaxLength(40)]
    [Column("plan_name")]
    public string PlanName { get; set; } = string.Empty;

    [MaxLength(30)]
    [Column("phone")]
    public string? Phone { get; set; }

    /// <summary>CPF ou CNPJ de cobrança, só dígitos. Vira Tenant.BillingCnpj
    /// quando a loja nasce.</summary>
    [MaxLength(14)]
    [Column("billing_document")]
    public string? BillingDocument { get; set; }

    /// <summary>Qual texto de /privacidade a pessoa viu ao marcar a ciência.</summary>
    [Required, MaxLength(20)]
    [Column("privacy_notice_version")]
    public string PrivacyNoticeVersion { get; set; } = string.Empty;

    [Column("privacy_notice_acknowledged_at")]
    public DateTime PrivacyNoticeAcknowledgedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Reserva da confirmação. Dois cliques no link (ou o e-mail aberto em
    /// dois aparelhos) chegam juntos; só quem grava esta coluna com um UPDATE
    /// condicional provisiona. Uma reserva mais velha que alguns minutos é
    /// tratada como processo que morreu no meio e pode ser retomada.</summary>
    [Column("confirmation_started_at")]
    public DateTime? ConfirmationStartedAt { get; set; }

    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    [Column("tenant_id")]
    public Guid? TenantId { get; set; }
}
