// =============================================================================
// IEmailService.cs — Contrato de envio de emails do sistema
// =============================================================================

namespace CardGameStore.Services.Interfaces;

public interface IEmailService
{
    // ── Base dos links ────────────────────────────────────────────────────────

    /// <summary>
    /// URL base pública da instalação (sem barra no fim), usada pra montar
    /// qualquer link que saia dentro de um e-mail. Fonte única de verdade:
    /// cada consumidor que reimplementava essa leitura acabava lendo a chave
    /// de configuração errada e mandando link apontando pro placeholder
    /// https://tenant-erp.local, que não existe.
    /// </summary>
    string AppUrl { get; }

    // ── Autenticação ──────────────────────────────────────────────────────────

    /// <summary>Envia email de recuperação de senha com link contendo o token.</summary>
    Task SendPasswordResetAsync(string toEmail, string toName, string resetToken);

    /// <summary>Convida um integrante para criar a senha da conta da plataforma.</summary>
    Task SendPlatformOwnerInviteAsync(string toEmail, string toName, string profileName, string inviteToken);

    /// <summary>Envia email de boas-vindas após primeiro login via QR Code.</summary>
    Task SendWelcomeAsync(string toEmail, string toName);

    // ── Crediário ─────────────────────────────────────────────────────────────

    /// <summary>Notifica o cliente que uma comanda foi lançada no crediário.</summary>
    Task SendCrediarioAbertoAsync(string toEmail, string toName, decimal valor, DateTime vencimento);

    /// <summary>Notifica o cliente que seu crediário foi quitado.</summary>
    Task SendCrediarioPagoAsync(string toEmail, string toName, decimal valor);

    // ── Pré-venda / Lista de espera ───────────────────────────────────────────

    /// <summary>Avisa o cliente que chegou sua vez na lista de espera.</summary>
    Task SendWaitListNotifiedAsync(string toEmail, string toName, string productName, string productUrl);

    // ── Anúncios (broadcast) ──────────────────────────────────────────────────

    /// <summary>
    /// Envia anúncio/promoção para uma lista de destinatários.
    /// Imagem e link são opcionais; retorna a quantidade de e-mails enviados com sucesso.
    /// </summary>
    Task<int> SendAnuncioAsync(IEnumerable<(string email, string name)> destinatarios, string titulo, string corpo,
                               string? imageUrl = null, string? link = null);

    // ── LGPD ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Confirma ao solicitante o recebimento da solicitação LGPD com número de protocolo e prazo.
    /// </summary>
    Task SendLgpdConfirmationAsync(string toEmail, string toName, string protocol,
                                   string requestType, DateTime deadline);

    /// <summary>
    /// Envia ao solicitante a resposta formal do responsável pelo tratamento de dados.
    /// </summary>
    Task SendLgpdResponseAsync(string toEmail, string toName, string protocol,
                                string requestType, string response);

    /// <summary>Envia um email de diagnóstico para testar as configurações de SMTP.</summary>
    Task<bool> SendDiagnosticEmailAsync(string toEmail);

    // ── Fiscal ────────────────────────────────────────────────────────────────

    /// <summary>Alerta o admin que o certificado digital A1 está próximo do vencimento.</summary>
    Task SendCertificadoVencendoAsync(string toEmail, string toName, int diasRestantes, DateTime validade);

    /// <summary>Envia ao contador o ZIP mensal com os XMLs de NFC-e autorizadas/canceladas.</summary>
    Task SendXmlsMensalContadorAsync(string toEmail, string mesReferencia, byte[] zipBytes, string zipFileName);

    /// <summary>
    /// Pendência fiscal crítica (CON-002). Vai para os admins do tenant porque
    /// notificação in-app só é vista por quem abre o painel — e um resultado
    /// incerto às 19h de sábado não pode esperar segunda-feira.
    /// </summary>
    Task SendAlertaFiscalCriticoAsync(
        string toEmail, string toName, string titulo, string detalhe, int totalCriticos);
}
