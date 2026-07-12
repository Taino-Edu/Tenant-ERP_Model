// =============================================================================
// SiteConfig.cs — Personalização da landing page (nome, textos, cores)
// Singleton lógico: uma única linha representa a configuração da loja.
// Todo campo tem um default igual ao valor hardcoded original da landing —
// enquanto o admin não mexe em nada, o site continua idêntico a hoje.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardGameStore.Models.PostgreSQL;

[Table("site_config")]
public class SiteConfig
{
    public static readonly Guid SingletonId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Key]
    [Column("id")]
    public Guid Id { get; set; } = SingletonId;

    // ── Identidade ───────────────────────────────────────────────────────────
    [Required, MaxLength(100)]
    [Column("site_name")]
    public string SiteName { get; set; } = "Minha Loja";

    [MaxLength(400)]
    [Column("hero_subtitle")]
    public string HeroSubtitle { get; set; } =
        "Produtos e a melhor experiência de atendimento da região. Acumule pontos e compre direto na mesa.";

    [MaxLength(150)]
    [Column("address_line")]
    public string AddressLine { get; set; } = "Sua Cidade — UF";

    /// <summary>Nome da pessoa de contato (ex: dono da loja) — usado em textos tipo "Falar com o
    /// {nome}" ou "{nome} vai confirmar...". Separado do nome do site de propósito: um é a marca,
    /// o outro é quem atende no WhatsApp/balcão.</summary>
    [MaxLength(60)]
    [Column("contact_person_name")]
    public string ContactPersonName { get; set; } = "Atendimento";

    // ── Contato ──────────────────────────────────────────────────────────────
    [MaxLength(20)]
    [Column("whatsapp_number")]
    public string WhatsappNumber { get; set; } = "";

    [MaxLength(150)]
    [Column("contact_email")]
    public string ContactEmail { get; set; } = "contato@tenant-erp.local";

    /// <summary>URL do logo (upload via /api/upload/image). Vazio = usa o ícone genérico padrão.</summary>
    [MaxLength(300)]
    [Column("logo_url")]
    public string? LogoUrl { get; set; }

    /// <summary>URL do favicon do site (upload via /api/upload/image). Vazio = usa o ícone genérico padrão.</summary>
    [MaxLength(300)]
    [Column("favicon_url")]
    public string? FaviconUrl { get; set; }

    /// <summary>URL do ícone do PWA (upload via /api/upload/image). Vazio = usa o ícone genérico padrão.</summary>
    [MaxLength(300)]
    [Column("pwa_icon_url")]
    public string? PwaIconUrl { get; set; }

    /// <summary>URL do ícone exibido no painel admin (upload via /api/upload/image). Vazio = usa LogoUrl.</summary>
    [MaxLength(300)]
    [Column("admin_icon_url")]
    public string? AdminIconUrl { get; set; }

    // ── Textos da navbar ─────────────────────────────────────────────────────
    [MaxLength(40)]
    [Column("nav_produtos_label")]
    public string NavProdutosLabel { get; set; } = "Produtos";

    [MaxLength(40)]
    [Column("nav_pontos_label")]
    public string NavPontosLabel { get; set; } = "Pontos";

    [MaxLength(40)]
    [Column("cta_ver_produtos_label")]
    public string CtaVerProdutosLabel { get; set; } = "Ver Produtos";

    // ── Textos das seções ────────────────────────────────────────────────────
    [MaxLength(60)]
    [Column("produtos_eyebrow")]
    public string ProdutosEyebrow { get; set; } = "Vitrine";

    [MaxLength(80)]
    [Column("produtos_title")]
    public string ProdutosTitle { get; set; } = "Em Destaque";

    [MaxLength(60)]
    [Column("pontos_eyebrow")]
    public string PontosEyebrow { get; set; } = "Programa de Fidelidade";

    [MaxLength(80)]
    [Column("pontos_title")]
    public string PontosTitle { get; set; } = "Ganhe pontos a cada visita";

    [MaxLength(400)]
    [Column("pontos_paragraph")]
    public string PontosParagraph { get; set; } =
        "Acumule pontos nas suas compras e troque por descontos. Só com CPF e WhatsApp — nada de senha ou aplicativo.";

    // ── Cores ────────────────────────────────────────────────────────────────
    [MaxLength(9)]
    [Column("color_primary")]
    public string ColorPrimary { get; set; } = "#3EC2F2";

    [MaxLength(9)]
    [Column("color_accent")]
    public string ColorAccent { get; set; } = "#FFE45E";

    [MaxLength(9)]
    [Column("color_navy")]
    public string ColorNavy { get; set; } = "#0C3D5A";

    /// <summary>Fundo da landing page — só afeta o tema claro (o tema escuro mantém a paleta
    /// própria dele, é uma escolha de tema, não de marca).</summary>
    [MaxLength(9)]
    [Column("color_background")]
    public string ColorBackground { get; set; } = "#EBF7FD";

    /// <summary>Fundo dos cards/caixas (produto, etc.) na landing page — só tema claro.</summary>
    [MaxLength(9)]
    [Column("color_card")]
    public string ColorCard { get; set; } = "#FFFFFF";

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Módulos pagos habilitados pro tenant atual (ex: "fiscal") — não persiste
    /// aqui, é preenchido pelo SiteConfigController a partir do ITenantContext na hora da
    /// resposta. Só "pegando carona" no fetch de SiteConfig (já chamado em toda página) pra
    /// evitar um round-trip novo — não é config de site de verdade.</summary>
    [NotMapped]
    public string[] EnabledModules { get; set; } = Array.Empty<string>();
}
