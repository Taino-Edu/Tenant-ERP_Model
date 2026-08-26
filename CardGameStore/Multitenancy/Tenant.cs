// =============================================================================
// Tenant.cs — Catálogo de tenants (schema-per-tenant).
// Vive no CatalogDbContext, sempre no schema "public" — resolver o schema de
// um tenant a partir do slug não pode depender do schema ainda não resolvido.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CardGameStore.Multitenancy;

public enum TenantStatus
{
    Active,
    Suspended,
}

/// <summary>Define onde vivem os dados operacionais do tenant.</summary>
public enum TenantKind
{
    /// <summary>Loja hospedada integralmente na plataforma, com schema PostgreSQL próprio.</summary>
    Native,

    /// <summary>Sistema independente que mantém banco e usuários próprios e consome APIs da plataforma.</summary>
    ExternalIntegrated,
}

/// <summary>Status de pagamento do tenant — rastreio manual pelo dono da plataforma
/// (ciclo 1 de billing, sem gateway de pagamento integrado ainda).</summary>
public enum TenantPaymentStatus
{
    Pago,
    Atrasado,
    Isento,
}

[Table("tenants")]
public class Tenant
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Subdomínio do tenant (ex: "loja-maikon" em loja-maikon.2esysten.com.br).</summary>
    [Required, MaxLength(63)]
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Nome do schema Postgres dedicado a este tenant (limite de 63 bytes do Postgres).</summary>
    [Required, MaxLength(63)]
    [Column("schema_name")]
    public string SchemaName { get; set; } = string.Empty;

    [Column("status")]
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    [Column("kind")]
    public TenantKind Kind { get; set; } = TenantKind.Native;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Nome do plano contratado — texto livre (sem enum fixo, pricing ainda não fechado).</summary>
    [Required, MaxLength(63)]
    [Column("plan_name")]
    public string PlanName { get; set; } = "Rio";

    [Column("payment_status")]
    public TenantPaymentStatus PaymentStatus { get; set; } = TenantPaymentStatus.Pago;

    /// <summary>Módulos pagos habilitados pra este tenant — hoje "fiscal", "estoque",
    /// "restaurante" (mesa, QR Code e áreas de produção), "pontos" (fidelidade),
    /// "contador" (portal cross-tenant), "ia" (assistente Gemini) e "eventos"
    /// (gestão de eventos com cobrança de entrada). Ver RequireModuleAttribute
    /// e, pro portal do contador, o gate manual em ContadorPortalController.AutorizarEObterTenantAsync.
    ///
    /// Comanda NÃO está nessa lista: é plano base e não depende de módulo nenhum.
    /// O módulo "restaurante" acrescenta a operação de salão em cima dela.</summary>
    [Column("enabled_modules")]
    public string[] EnabledModules { get; set; } = new[] { "fiscal" };

    /// <summary>Limite de usuários com acesso ao painel (Admin + Operator) pro plano
    /// contratado — null significa sem limite. Enforçado em UserService.AdminCreateUserAsync
    /// na criação de Operator (Customer não conta nem é limitado por isso).</summary>
    [Column("max_users")]
    public int? MaxUsers { get; set; }

    /// <summary>Cópia denormalizada de SiteConfig.SiteName do schema deste tenant — mantida em
    /// sincronia por SiteConfigController.SaveConfig. Existe só pra o diretório público de lojas
    /// (institucional) não precisar trocar de schema por tenant a cada carregamento; a fonte de
    /// verdade continua sendo o SiteConfig do próprio tenant.</summary>
    [MaxLength(100)]
    [Column("display_name")]
    public string? DisplayName { get; set; }

    /// <summary>Cópia denormalizada de SiteConfig.LogoUrl — mesmo motivo/mesma sincronia de DisplayName.</summary>
    [MaxLength(300)]
    [Column("logo_url")]
    public string? LogoUrl { get; set; }

    /// <summary>Domínio próprio do lojista (ex: "minhaloja.com.br"), sempre em minúsculas,
    /// sem esquema/porta/path. Null = só o subdomínio de <c>Slug</c> funciona. TLS não é
    /// automatizado pra domínio próprio — o lojista precisa colocar o domínio dele atrás da
    /// própria conta Cloudflare (modo Flexible), do mesmo jeito que fazemos com o domínio
    /// raiz da plataforma. Ver TenantResolutionMiddleware pra como isso é resolvido.</summary>
    [MaxLength(253)]
    [Column("custom_domain")]
    public string? CustomDomain { get; set; }

    // ── Billing da plataforma ────────────────────────────────────────────────
    // Tabela vigente: Lagoa R$129, Rio R$269 e Mar R$487. Todos têm implantação
    // de 2 mensalidades e 15 dias sem mensalidade. A implantação é moeda de
    // negociação: o painel da plataforma edita o valor por loja, inclusive
    // zerando — a tabela é ponto de partida.
    //
    // O valor fica NO TENANT, não numa tabela de planos, de propósito: PlanName
    // já é texto livre e desconto negociado caso a caso é regra, não exceção
    // nesse estágio. Derivar o preço do nome do plano quebraria no primeiro
    // cliente que fechar por um valor diferente da tabela.

    /// <summary>Mensalidade efetivamente cobrada deste tenant, em reais. Zero =
    /// não cobra (cortesia, piloto, tenant-zero da própria plataforma). É a base
    /// do MRR: somar isto nos tenants Active dá a receita contratada.</summary>
    // [Precision] em vez de TypeName = "decimal(10,2)": no Postgres o tipo é
    // `numeric`, e passar "decimal(10,2)" fazia o Npgsql cair no caminho de
    // mapeamento de coleção e estourar IndexOutOfRangeException ao gerar
    // migration. [Precision] deixa o provider escolher o tipo certo.
    [Precision(10, 2)]
    [Column("monthly_price")]
    public decimal MonthlyPrice { get; set; }

    /// <summary>Taxa de implantação cobrada na contratação, em reais.
    /// Persistida em vez de calculada (2 × <see cref="MonthlyPrice"/>) porque é
    /// fato histórico: se a política ou o preço do plano mudar amanhã, o que foi
    /// cobrado deste cliente não pode mudar retroativamente.</summary>
    [Precision(10, 2)]
    [Column("setup_fee")]
    public decimal SetupFee { get; set; }

    /// <summary>Data da PRIMEIRA mensalidade devida. É assim que o período de
    /// 15 dias grátis fica registrado — na provisão vira CreatedAt + 15 dias, em
    /// vez de uma flag booleana "primeiroMesGratis" que desalinha da realidade no
    /// instante em que alguém edita a data à mão. Null = billing ainda não
    /// definido (tenant provisionado antes deste campo existir).
    ///
    /// O dia do vencimento é o dia desta data — não há campo separado de propósito,
    /// pra não criar duas fontes de verdade pro mesmo dado. Vencimento configurável
    /// independente da data de início é refinamento futuro, se algum cliente pedir.</summary>
    [Column("billing_starts_on")]
    public DateTime? BillingStartsOn { get; set; }
}
