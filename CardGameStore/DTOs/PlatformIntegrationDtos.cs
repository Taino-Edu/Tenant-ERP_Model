// =============================================================================
// PlatformIntegrationDtos.cs — Contrato da área de Integrações da plataforma.
//
// Regra que vale pra tudo aqui: NENHUM campo de segredo sai numa resposta. A
// tela precisa saber se está configurado e o que falta — não precisa (e não
// pode) receber o valor de volta. Por isso o DTO expõe flags "temX", e o
// ClientId, que é identificador e não segredo, sai em claro pra o dono da
// plataforma conferir que apontou pra aplicação certa.
// =============================================================================

using CardGameStore.Multitenancy;

namespace CardGameStore.DTOs;

public record PlatformIntegrationDto(
    string    Provider,
    string    Nome,
    bool      Configurado,
    bool      IsActive,
    bool      Operacional,
    string?   ClientId,
    bool      TemClientSecret,
    bool      TemCertificado,
    string?   ContaCorrente,
    string?   PixKey,
    /// <summary>O que ainda falta pra integração funcionar, em texto pronto pra
    /// tela — evita a UI reimplementar (e divergir de) a regra do servidor.</summary>
    string[]  Pendencias,
    DateTime? LastSyncAt,
    string?   LastError,
    DateTime? UpdatedAt)
{
    private static string NomeDe(string provider) => provider switch
    {
        PlatformIntegrationProvider.Inter => "Banco Inter",
        _                                 => provider,
    };

    public static PlatformIntegrationDto De(string provider, PlatformIntegration? cfg)
    {
        if (cfg is null)
            return new PlatformIntegrationDto(
                provider, NomeDe(provider), Configurado: false, IsActive: false, Operacional: false,
                ClientId: null, TemClientSecret: false, TemCertificado: false,
                ContaCorrente: null, PixKey: null,
                Pendencias: ["Integração ainda não configurada."],
                LastSyncAt: null, LastError: null, UpdatedAt: null);

        var pendencias = new List<string>();
        if (string.IsNullOrWhiteSpace(cfg.ClientId))                 pendencias.Add("Client ID não informado.");
        if (string.IsNullOrWhiteSpace(cfg.ClientSecretEncrypted))    pendencias.Add("Client Secret não informado.");
        if (string.IsNullOrWhiteSpace(cfg.CertificateCrtEncrypted) ||
            string.IsNullOrWhiteSpace(cfg.CertificateKeyEncrypted))  pendencias.Add("Certificado mTLS (.crt + .key) não enviado.");
        // Sem chave Pix o Inter registra boleto puro — funciona, mas o pagador
        // perde o QR Code, então é aviso e não bloqueio.
        if (string.IsNullOrWhiteSpace(cfg.PixKey))                   pendencias.Add("Sem chave Pix: o boleto sai sem QR Code.");
        if (!cfg.IsActive)                                           pendencias.Add("Integração desligada.");

        return new PlatformIntegrationDto(
            cfg.Provider,
            NomeDe(cfg.Provider),
            Configurado:     true,
            IsActive:        cfg.IsActive,
            Operacional:     cfg.Operacional,
            ClientId:        cfg.ClientId,
            TemClientSecret: !string.IsNullOrWhiteSpace(cfg.ClientSecretEncrypted),
            TemCertificado:  !string.IsNullOrWhiteSpace(cfg.CertificateCrtEncrypted) &&
                             !string.IsNullOrWhiteSpace(cfg.CertificateKeyEncrypted),
            ContaCorrente:   cfg.ContaCorrente,
            PixKey:          cfg.PixKey,
            Pendencias:      [.. pendencias],
            LastSyncAt:      cfg.LastSyncAt,
            LastError:       cfg.LastError,
            UpdatedAt:       cfg.UpdatedAt);
    }
}

/// <summary>
/// Campos de segredo nulos/vazios significam "não mexe no que já está salvo".
/// A tela nunca recebe o valor atual, então tratar vazio como "apagar" faria
/// um salvamento de rotina (mudar só a conta corrente, por exemplo) derrubar a
/// integração inteira.
/// </summary>
public class SalvarPlatformIntegrationRequest
{
    public string? ClientId       { get; init; }
    public string? ClientSecret   { get; init; }
    /// <summary>Conteúdo do .crt/.pem, colado como texto.</summary>
    public string? CertificateCrt { get; init; }
    /// <summary>Conteúdo do .key, colado como texto.</summary>
    public string? CertificateKey { get; init; }
    public string? ContaCorrente  { get; init; }
    public string? PixKey         { get; init; }
    public bool?   IsActive       { get; init; }

    /// <summary>Nomes dos campos que vieram preenchidos — vai pro audit log sem
    /// os valores, pra dar pra auditar "quem mexeu em quê" sem registrar segredo.</summary>
    public IEnumerable<string> CamposPreenchidos()
    {
        if (!string.IsNullOrWhiteSpace(ClientId))       yield return nameof(ClientId);
        if (!string.IsNullOrWhiteSpace(ClientSecret))   yield return nameof(ClientSecret);
        if (!string.IsNullOrWhiteSpace(CertificateCrt)) yield return nameof(CertificateCrt);
        if (!string.IsNullOrWhiteSpace(CertificateKey)) yield return nameof(CertificateKey);
        if (!string.IsNullOrWhiteSpace(ContaCorrente))  yield return nameof(ContaCorrente);
        if (!string.IsNullOrWhiteSpace(PixKey))         yield return nameof(PixKey);
        if (IsActive.HasValue)                          yield return nameof(IsActive);
    }
}
