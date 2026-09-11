// =============================================================================
// TenantSignupDtos.cs — Contrato público da criação de loja pelo site.
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public class SolicitarLojaRequest
{
    [Required, MaxLength(150)]
    public string NomeResponsavel { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string NomeLoja { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Teto de 72 porque o BCrypt ignora tudo depois do 72º byte: aceitar
    /// mais seria prometer uma senha mais forte do que a que fica gravada.</summary>
    [Required, MinLength(8), MaxLength(72)]
    public string Senha { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? WhatsApp { get; set; }

    /// <summary>Plano de tabela (Lagoa, Rio, Mar). Vazio cai no padrão do serviço.</summary>
    [MaxLength(40)]
    public string? Plano { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "É necessário confirmar a ciência da Política de Privacidade.")]
    public bool PrivacyNoticeAcknowledged { get; set; }

    [Required, MaxLength(20)]
    public string PrivacyNoticeVersion { get; set; } = "2.2";
}

public class ConfirmarLojaRequest
{
    [Required, MaxLength(100)]
    public string Token { get; set; } = string.Empty;
}

public class DisponibilidadeSlugDto
{
    /// <summary>O endereço como o servidor o entende (minúsculas, sem espaços).</summary>
    public string Slug { get; init; } = string.Empty;
    public bool Disponivel { get; init; }
    public string? Motivo { get; init; }
    public string? Sugestao { get; init; }
}
