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

    public async Task SendCrediarioAbertoAsync(string toEmail, string toName, decimal valor, DateTime vencimento)
    {
        var venc = vencimento.ToLocalTime().ToString("dd/MM/yyyy");
        var body = $"""
            <div style="font-family:sans-serif;max-width:500px">
              <h2 style="color:#7839F3">softNerd — Crediário Aberto</h2>
              <p>Olá, <strong>{toName}</strong>!</p>
              <p>
                Uma comanda foi registrada no seu crediário.
                Por favor, efetue o pagamento até a data de vencimento.
              </p>
              <table style="width:100%;border-collapse:collapse;margin:16px 0">
                <tr>
                  <td style="padding:8px;color:#666">Valor</td>
                  <td style="padding:8px;font-weight:bold;color:#111">R$ {valor:N2}</td>
                </tr>
                <tr style="background:#f9f9f9">
                  <td style="padding:8px;color:#666">Vencimento</td>
                  <td style="padding:8px;font-weight:bold;color:#dc2626">{venc}</td>
                </tr>
              </table>
              <p>
                Enquanto o crediário estiver em aberto, novas comandas ficarão bloqueadas.
                Compareça à loja ou fale com o Maikon para quitar.
              </p>
              <p style="color:#888;font-size:12px">softNerd — Sistema de Gestão</p>
            </div>
            """;

        await SendAsync(toEmail, toName, $"Crediário aberto — R$ {valor:N2} vence em {venc}", body);
    }

    public async Task SendCrediarioPagoAsync(string toEmail, string toName, decimal valor)
    {
        var body = $"""
            <div style="font-family:sans-serif;max-width:500px">
              <h2 style="color:#00F0A8">softNerd — Crediário Quitado</h2>
              <p>Olá, <strong>{toName}</strong>!</p>
              <p>
                Seu crediário de <strong>R$ {valor:N2}</strong> foi quitado com sucesso.
                Obrigado pelo pagamento!
              </p>
              <p>Você já pode abrir uma nova comanda normalmente.</p>
              <p style="color:#888;font-size:12px">softNerd — Sistema de Gestão</p>
            </div>
            """;

        await SendAsync(toEmail, toName, "Crediário quitado — softNerd", body);
    }

    public async Task SendCampeonatoInscricaoAsync(string toEmail, string toName, string campeonato, DateTime data, decimal entryFee)
    {
        var dataFmt = data.ToLocalTime().ToString("dd/MM/yyyy 'às' HH:mm");
        var body = $"""
            <div style="font-family:sans-serif;max-width:500px">
              <h2 style="color:#7839F3">softNerd — Inscrição Confirmada</h2>
              <p>Olá, <strong>{toName}</strong>!</p>
              <p>Sua inscrição no campeonato abaixo foi confirmada:</p>
              <table style="width:100%;border-collapse:collapse;margin:16px 0">
                <tr>
                  <td style="padding:8px;color:#666">Campeonato</td>
                  <td style="padding:8px;font-weight:bold">{campeonato}</td>
                </tr>
                <tr style="background:#f9f9f9">
                  <td style="padding:8px;color:#666">Data</td>
                  <td style="padding:8px;font-weight:bold">{dataFmt}</td>
                </tr>
                <tr>
                  <td style="padding:8px;color:#666">Taxa de Inscrição</td>
                  <td style="padding:8px;font-weight:bold">R$ {entryFee:N2}</td>
                </tr>
              </table>
              <p>Apareça na loja no dia do evento. Boa sorte!</p>
              <p style="color:#888;font-size:12px">softNerd — Sistema de Gestão</p>
            </div>
            """;

        await SendAsync(toEmail, toName, $"Inscrição confirmada: {campeonato}", body);
    }

    public async Task SendAnuncioAsync(IEnumerable<(string email, string name)> destinatarios, string titulo, string corpo)
    {
        var body = $"""
            <div style="font-family:sans-serif;max-width:500px">
              <h2 style="color:#7839F3">softNerd — {titulo}</h2>
              <div style="margin:16px 0;color:#333">
                {corpo}
              </div>
              <p style="color:#888;font-size:12px">
                Você recebe este email por ser cliente softNerd.<br/>
                Dúvidas? Fale com o Maikon no balcão.
              </p>
            </div>
            """;

        foreach (var (email, name) in destinatarios)
            await SendAsync(email, name, $"softNerd — {titulo}", body);
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
