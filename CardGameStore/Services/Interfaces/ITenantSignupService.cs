// =============================================================================
// ITenantSignupService.cs — Criação de loja pelo próprio lojista, em duas
// etapas: pedir (manda o link) e confirmar (cria a loja).
// =============================================================================

using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface ITenantSignupService
{
    Task<DisponibilidadeSlugDto> VerificarSlugAsync(string? slug, CancellationToken ct = default);

    Task<SolicitarLojaResultado> SolicitarAsync(SolicitarLojaRequest request, CancellationToken ct = default);

    /// <summary>Só lê: diz se o link ainda serve, para a página não pedir a senha
    /// de quem já não vai conseguir criar a loja. Não reserva nem cria nada.</summary>
    Task<LinkDeLojaResultado> VerificarLinkAsync(string? token, CancellationToken ct = default);

    Task<ConfirmarLojaResultado> ConfirmarAsync(string? token, string? senha, CancellationToken ct = default);
}

public enum LinkDeLojaStatus
{
    Valido,
    TokenInvalido,
    Expirado,
    JaConfirmada,
}

public sealed record LinkDeLojaResultado(
    LinkDeLojaStatus Status, string? Slug = null, string? NomeLoja = null);

public enum SolicitacaoStatus
{
    Enviada,
    SlugInvalido,
    SlugIndisponivel,
    PlanoInvalido,
    LimiteDiario,
    EmailFalhou,
}

public sealed record SolicitarLojaResultado(
    SolicitacaoStatus Status, string? Mensagem = null, string? SugestaoSlug = null);

public enum ConfirmacaoStatus
{
    Criada,
    TokenInvalido,
    Expirado,
    SenhaInvalida,
    JaConfirmada,
    EmAndamento,
    SlugIndisponivel,
    LimiteDiario,
    Falhou,
}

/// <remarks>Ticket só vem em <see cref="ConfirmacaoStatus.Criada"/>. Nunca em
/// JaConfirmada: se o link servisse para entrar de novo, ele viraria uma senha
/// permanente guardada na caixa de e-mail.</remarks>
public sealed record ConfirmarLojaResultado(
    ConfirmacaoStatus Status, string? Slug = null, string? Ticket = null, string? Mensagem = null);
