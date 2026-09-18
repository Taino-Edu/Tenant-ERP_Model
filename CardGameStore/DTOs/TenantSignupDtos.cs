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

    // Sem senha aqui, de propósito: este pedido é anônimo e ninguém provou ainda
    // que lê a caixa de entrada. Senha escolhida nesta etapa ficaria com quem
    // digitou o e-mail de outra pessoa — ver ConfirmarLojaRequest.

    [MaxLength(30)]
    public string? WhatsApp { get; set; }

    /// <summary>CPF ou CNPJ de quem vai pagar a mensalidade. Opcional: o teste
    /// grátis não depende dele, e exigir documento no primeiro contato afasta
    /// quem só quer experimentar. Mas quem informa aqui recebe a primeira fatura
    /// sozinho no fim do teste, sem precisar lembrar da tela de Assinatura.</summary>
    [MaxLength(18)]
    [CnpjOuCpfValid]
    public string? Documento { get; set; }

    /// <summary>Plano de tabela (Lagoa, Rio, Mar). Vazio cai no padrão do serviço.</summary>
    [MaxLength(40)]
    public string? Plano { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "É necessário confirmar a ciência da Política de Privacidade.")]
    public bool PrivacyNoticeAcknowledged { get; set; }

    [Required, MaxLength(20)]
    public string PrivacyNoticeVersion { get; set; } = "2.2";
}

public class VerificarLinkDeLojaRequest
{
    [Required, MaxLength(100)]
    public string Token { get; set; } = string.Empty;
}

public class ConfirmarLojaRequest
{
    [Required, MaxLength(100)]
    public string Token { get; set; } = string.Empty;

    /// <summary>Senha do admin, escolhida por quem abriu o link do e-mail — a única
    /// pessoa que provou ser dona do endereço. De 8 a 72 caracteres: o BCrypt
    /// ignora tudo depois do 72º byte, e aceitar mais seria prometer uma senha
    /// mais forte do que a que fica gravada.</summary>
    /// <remarks>Sem atributos de validação de propósito: o [ApiController]
    /// responderia um 400 genérico antes da action, e a página precisa do
    /// errorCode "senha_invalida" para mostrar o erro no próprio formulário. Quem
    /// confere é o TenantSignupService.</remarks>
    public string? Senha { get; set; }
}

public static class TenantSignupSenha
{
    public const int Minimo = 8;
    public const int Maximo = 72;
}

/// <summary>O que a página do link mostra antes de pedir a senha.</summary>
public class LinkDeLojaDto
{
    public string Slug { get; init; } = string.Empty;
    public string NomeLoja { get; init; } = string.Empty;
}

public class DisponibilidadeSlugDto
{
    /// <summary>O endereço como o servidor o entende (minúsculas, sem espaços).</summary>
    public string Slug { get; init; } = string.Empty;
    public bool Disponivel { get; init; }
    public string? Motivo { get; init; }
    public string? Sugestao { get; init; }
}
