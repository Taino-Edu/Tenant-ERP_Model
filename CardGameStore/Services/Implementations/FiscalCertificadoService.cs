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
            using var cert = new X509Certificate2(pfxBytes, senha, X509KeyStorageFlags.Exportable);

            if (!cert.HasPrivateKey)
                throw new CertificadoInvalidoException("O certificado não possui chave privada — verifique se é um .pfx/.p12 válido.");

            return new CertificadoInfo(cert.Subject, cert.NotBefore, cert.NotAfter);
        }
        catch (CryptographicException ex)
        {
            throw new CertificadoInvalidoException("Senha incorreta ou arquivo de certificado inválido.", ex);
        }
    }
}

public record CertificadoInfo(string Subject, DateTime NotBefore, DateTime NotAfter);

public class CertificadoInvalidoException : Exception
{
    public CertificadoInvalidoException(string message, Exception? inner = null) : base(message, inner) { }
}
