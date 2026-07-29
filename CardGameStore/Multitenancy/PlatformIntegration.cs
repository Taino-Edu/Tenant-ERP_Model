// =============================================================================
// PlatformIntegration.cs — Credenciais de serviços externos DA PLATAFORMA.
//
// Vive no CatalogDbContext (schema "public"), nunca no schema do tenant. A
// distinção não é organizacional, é de segurança: estas são as credenciais da
// conta bancária da PLATAFORMA, usadas pra cobrar os lojistas. Se morassem no
// schema do tenant, qualquer falha de isolamento entregaria a chave da nossa
// conta pro cliente — e o `IntegrationConfig` que já existe (esse sim no schema
// do tenant) guarda as credenciais DELE, pra cobrar os clientes DELE. São dois
// conjuntos de segredo com donos diferentes; misturar seria o pior tipo de bug.
//
// Uma linha por provedor: o índice único em Provider impede duas configurações
// concorrentes do mesmo serviço, que na prática significaria "não sei com qual
// conta eu cobro".
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

public static class PlatformIntegrationProvider
{
    /// <summary>Banco Inter — cobrança (boleto híbrido com QR Pix) das mensalidades.</summary>
    public const string Inter = "inter";

    public static readonly string[] Todos = [Inter];
    public static bool EhConhecido(string? provider) => Todos.Contains(provider);
}

[Table("platform_integrations")]
[Index(nameof(Provider), IsUnique = true)]
public class PlatformIntegration
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(30)]
    [Column("provider")]
    public string Provider { get; set; } = "";

    // -------------------------------------------------------------------------
    // Credenciais — tudo que é segredo entra criptografado pelo EncryptionService
    // e NUNCA volta numa resposta de API. O ClientId fica em claro de propósito:
    // é identificador, não segredo, e mostrá-lo na tela é o que deixa o dono da
    // plataforma conferir que configurou a aplicação certa.
    // -------------------------------------------------------------------------

    [MaxLength(200)]
    [Column("client_id")]
    public string? ClientId { get; set; }

    [Column("client_secret_encrypted")]
    public string? ClientSecretEncrypted { get; set; }

    /// <summary>Certificado mTLS (.crt/.pem) exigido pela API do Inter.</summary>
    [Column("certificate_crt_encrypted")]
    public string? CertificateCrtEncrypted { get; set; }

    /// <summary>Chave privada (.key) do certificado mTLS.</summary>
    [Column("certificate_key_encrypted")]
    public string? CertificateKeyEncrypted { get; set; }

    // -------------------------------------------------------------------------
    // Dados da conta — não são segredo, mas são obrigatórios pra emitir cobrança
    // -------------------------------------------------------------------------

    /// <summary>Conta corrente do Inter, exigida no header `x-conta-corrente`
    /// quando a aplicação tem acesso a mais de uma conta.</summary>
    [MaxLength(20)]
    [Column("conta_corrente")]
    public string? ContaCorrente { get; set; }

    /// <summary>Chave Pix da conta — é ela que faz o boleto sair híbrido, com o
    /// QR Code embutido. Sem chave, o Inter registra boleto puro.</summary>
    [MaxLength(100)]
    [Column("pix_key")]
    public string? PixKey { get; set; }

    // -------------------------------------------------------------------------
    // Estado
    // -------------------------------------------------------------------------

    /// <summary>Desligar sem apagar credencial — pra suspender a cobrança
    /// automática sem ter que reconfigurar tudo depois.</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("last_sync_at")]
    public DateTime? LastSyncAt { get; set; }

    /// <summary>Último erro da integração, pra tela dizer o que houve em vez de
    /// só "não funcionou". Mensagem já sanitizada — nunca guarda credencial.</summary>
    [MaxLength(500)]
    [Column("last_error")]
    public string? LastError { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // -------------------------------------------------------------------------
    // Calculados (não mapeados)
    // -------------------------------------------------------------------------

    /// <summary>Tem tudo que a API do Inter exige pra autenticar.</summary>
    [NotMapped]
    public bool CredenciaisCompletas =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecretEncrypted) &&
        !string.IsNullOrWhiteSpace(CertificateCrtEncrypted) &&
        !string.IsNullOrWhiteSpace(CertificateKeyEncrypted);

    /// <summary>Pronta pra emitir: credenciais completas E ligada.</summary>
    [NotMapped]
    public bool Operacional => CredenciaisCompletas && IsActive;
}
