namespace CardGameStore.Services.Interfaces;

public interface IEmailService
{
    /// <summary>Envia email de recuperação de senha com link contendo o token.</summary>
    Task SendPasswordResetAsync(string toEmail, string toName, string resetToken);

    /// <summary>Envia email de boas-vindas após primeiro login via QR Code.</summary>
    Task SendWelcomeAsync(string toEmail, string toName);
}
