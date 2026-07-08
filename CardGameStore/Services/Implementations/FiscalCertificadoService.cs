// =============================================================================
// FiscalCertificadoService.cs — Validação e leitura do certificado digital A1
// usado para assinar NFC-e. Não depende de banco — puro X509.
// =============================================================================

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CardGameStore.Services.Implementations;

public class FiscalCertificadoService
{
    /// <summary>
    /// Abre o certificado .pfx com a senha informada e retorna seus metadados.
    /// Lança <see cref="CertificadoInvalidoException"/> se a senha estiver errada
    /// ou o arquivo não for um certificado válido.
    /// </summary>
    public CertificadoInfo Validar(byte[] pfxBytes, string senha)
    {
        try
        {
            using var cert = Pkcs12Loader.Abrir(pfxBytes, senha);

            if (!cert.HasPrivateKey)
                throw new CertificadoInvalidoException("O certificado não possui chave privada — verifique se é um .pfx/.p12 válido.");

            // X509Certificate2.NotBefore/NotAfter vêm com Kind=Local (conversão do .NET a
            // partir do UTC original do certificado) — Npgsql rejeita gravar DateTime não-UTC
            // em timestamptz. ToUniversalTime() converte preservando o instante real.
            return new CertificadoInfo(cert.Subject, cert.NotBefore.ToUniversalTime(), cert.NotAfter.ToUniversalTime());
        }
        catch (CryptographicException ex)
        {
            throw new CertificadoInvalidoException(
                $"Senha incorreta ou arquivo de certificado inválido. Detalhe técnico: {ex.Message}", ex);
        }
    }
}

public record CertificadoInfo(string Subject, DateTime NotBefore, DateTime NotAfter);

public class CertificadoInvalidoException : Exception
{
    public CertificadoInvalidoException(string message, Exception? inner = null) : base(message, inner) { }
}
