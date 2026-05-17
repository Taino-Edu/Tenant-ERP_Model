// =============================================================================
// IEmailService.cs — Contrato de envio de emails do sistema
// =============================================================================

namespace CardGameStore.Services.Interfaces;

public interface IEmailService
{
    // ── Autenticação ──────────────────────────────────────────────────────────

    /// <summary>Envia email de recuperação de senha com link contendo o token.</summary>
    Task SendPasswordResetAsync(string toEmail, string toName, string resetToken);

    /// <summary>Envia email de boas-vindas após primeiro login via QR Code.</summary>
    Task SendWelcomeAsync(string toEmail, string toName);

    // ── Crediário ─────────────────────────────────────────────────────────────

    /// <summary>Notifica o cliente que uma comanda foi lançada no crediário.</summary>
    Task SendCrediarioAbertoAsync(string toEmail, string toName, decimal valor, DateTime vencimento);

    /// <summary>Notifica o cliente que seu crediário foi quitado.</summary>
    Task SendCrediarioPagoAsync(string toEmail, string toName, decimal valor);

    // ── Campeonatos ───────────────────────────────────────────────────────────

    /// <summary>Confirmação de inscrição em campeonato.</summary>
    Task SendCampeonatoInscricaoAsync(string toEmail, string toName, string campeonato, DateTime data, decimal entryFee);

    // ── Anúncios (broadcast) ──────────────────────────────────────────────────

    /// <summary>Envia anúncio/promoção para uma lista de destinatários.</summary>
    Task SendAnuncioAsync(IEnumerable<(string email, string name)> destinatarios, string titulo, string corpo);

    // ── LGPD ──────────────────────────────────────────────────────────────────

    /// <summary>Confirma ao solicitante o recebimento da solicitação LGPD com protocolo e prazo.</summary>
    Task SendLgpdConfirmationAsync(string toEmail, string toName, string protocol,
                                   string requestType, DateTime deadline);

    /// <summary>Envia ao solicitante a resposta formal do responsável pelo tratamento de dados.</summary>
    Task SendLgpdResponseAsync(string toEmail, string toName, string protocol,
                                string requestType, string response);
}
