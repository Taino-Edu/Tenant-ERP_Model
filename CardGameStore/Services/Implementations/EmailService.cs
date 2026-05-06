// =============================================================================
// EmailService.cs — Envio de emails via SMTP
//
// Configuração (appsettings.json ou variáveis de ambiente):
//   EmailSettings__Host     → smtp.gmail.com  (ou smtp.sendgrid.net etc.)
//   EmailSettings__Port     → 587
//   EmailSettings__User     → seu@email.com
//   EmailSettings__Password → senha-de-app ou api-key
//   EmailSettings__From     → noreply@softnerd.com.br
//   EmailSettings__AppUrl   → https://softnerd.com.br (para montar o link de reset)
//
// Para Gmail: ative "Senhas de app" nas configurações da conta Google.
// Para SendGrid: use smtp.sendgrid.net:587, usuário "apikey", senha = API Key.
// =============================================================================

using System.Net;
using System.Net.Mail;
using CardGameStore.Services.Interfaces;

namespace CardGameStore.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly IConfiguration         _config;
    private readonly ILogger<EmailService>  _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetToken)
    {
        var appUrl = _config["EmailSettings:AppUrl"] ?? "http://localhost:3000";
        var link   = $"{appUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";

        var body = $"""
            <p>Olá, <strong>{toName}</strong>!</p>
            <p>Recebemos uma solicitação de redefinição de senha para sua conta no <strong>softNerd</strong>.</p>
            <p>
              <a href="{link}" style="background:#f59e0b;color:#000;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold;">
                Redefinir minha senha
              </a>
            </p>
            <p style="color:#888;font-size:12px;">
              Este link expira em 2 horas. Se você não solicitou a redefinição, ignore este email.
            </p>
            """;

        await SendAsync(toEmail, toName, "Redefinição de senha — softNerd", body);
    }

    public async Task SendWelcomeAsync(string toEmail, string toName)
    {
        var body = $"""
            <p>Olá, <strong>{toName}</strong>! Seja bem-vindo(a) ao softNerd!</p>
            <p>Seu cadastro foi criado automaticamente ao escanear o QR Code da mesa.</p>
            <p>Acumule pontos a cada visita e troque por produtos na loja.</p>
            <p style="color:#888;font-size:12px;">
              Dúvidas? Fale com o Maikon no balcão.
            </p>
            """;

        await SendAsync(toEmail, toName, "Bem-vindo(a) ao softNerd!", body);
    }

    // ── Interno ───────────────────────────────────────────────────────────────

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var host     = _config["EmailSettings:Host"];
        var portStr  = _config["EmailSettings:Port"];
        var user     = _config["EmailSettings:User"];
        var password = _config["EmailSettings:Password"];
        var from     = _config["EmailSettings:From"] ?? user;

        // Se email não estiver configurado, loga e retorna sem erro —
        // o sistema funciona sem email em dev/testes.
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
        {
            _logger.LogWarning(
                "EmailService: configuração ausente. Email para {To} ('{Subject}') não foi enviado.",
                toEmail, subject);
            return;
        }

        try
        {
            var port   = int.TryParse(portStr, out var p) ? p : 587;
            var client = new SmtpClient(host, port)
            {
                Credentials       = new NetworkCredential(user, password),
                EnableSsl         = true,
                DeliveryMethod    = SmtpDeliveryMethod.Network,
            };

            using var msg = new MailMessage
            {
                From       = new MailAddress(from!, "softNerd"),
                Subject    = subject,
                Body       = htmlBody,
                IsBodyHtml = true,
            };
            msg.To.Add(new MailAddress(toEmail, toName));

            await client.SendMailAsync(msg);
            _logger.LogInformation("Email '{Subject}' enviado para {To}", subject, toEmail);
        }
        catch (Exception ex)
        {
            // Falha de email não derruba o fluxo principal
            _logger.LogError(ex, "Falha ao enviar email '{Subject}' para {To}", subject, toEmail);
        }
    }
}
