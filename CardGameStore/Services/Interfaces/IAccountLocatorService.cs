using CardGameStore.DTOs;

namespace CardGameStore.Services.Interfaces;

public interface IAccountLocatorService
{
    /// <summary>Procura o e-mail/senha em PlatformOwner (schema public), Contador
    /// (catálogo) e todo tenant ativo — só chamado explicitamente (botão "procurar
    /// em outro lugar"), nunca em toda tentativa de login. Para cada acerto de
    /// senha, gera um LoginRedirectTicket de uso único.</summary>
    Task<List<LocateAccountMatchDto>> LocateAsync(string email, string password);
}
