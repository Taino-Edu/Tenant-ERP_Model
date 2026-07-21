// =============================================================================
// FiscalCertificadoServiceTests.cs — Testes unitários da validação do
// certificado A1, usando um certificado self-signed gerado em memória.
// =============================================================================

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CardGameStore.Services.Implementations;

namespace CardGameStore.Tests.Services;

public class FiscalCertificadoServiceTests
{
    private const string Senha = "senha-teste-123";

    private static byte[] CreateSelfSignedPfx(string senha, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Fiscal Teste", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(notBefore, notAfter);
        return cert.Export(X509ContentType.Pfx, senha);
    }

    [Fact]
    public void Validar_ComSenhaCorreta_RetornaValidadeDoCertificado()
    {
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter  = DateTimeOffset.UtcNow.AddDays(30);
        var pfxBytes  = CreateSelfSignedPfx(Senha, notBefore, notAfter);

        var service = new FiscalCertificadoService();
        var info = service.Validar(pfxBytes, Senha);

        info.NotAfter.Date.Should().Be(notAfter.UtcDateTime.Date);
        info.Subject.Should().Contain("Fiscal Teste");
    }

    [Fact]
    public void Validar_ComSenhaErrada_LancaCertificadoInvalidoException()
    {
        var pfxBytes = CreateSelfSignedPfx(Senha, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        var service = new FiscalCertificadoService();
        Action act = () => service.Validar(pfxBytes, "senha-errada");

        act.Should().Throw<CertificadoInvalidoException>();
    }

    [Fact]
    public void Validar_ComArquivoInvalido_LancaCertificadoInvalidoException()
    {
        var bytesInvalidos = new byte[] { 1, 2, 3, 4, 5 };

        var service = new FiscalCertificadoService();
        Action act = () => service.Validar(bytesInvalidos, Senha);

        act.Should().Throw<CertificadoInvalidoException>();
    }

    [Fact]
    public void Validar_CertificadoVencido_LancaErroClaro()
    {
        var pfxBytes = CreateSelfSignedPfx(
            Senha, DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1));

        var act = () => new FiscalCertificadoService().Validar(pfxBytes, Senha);

        act.Should().Throw<CertificadoInvalidoException>().WithMessage("*venceu*");
    }

    [Fact]
    public void Validar_CertificadoAindaNaoValido_LancaErroClaro()
    {
        var pfxBytes = CreateSelfSignedPfx(
            Senha, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(30));

        var act = () => new FiscalCertificadoService().Validar(pfxBytes, Senha);

        act.Should().Throw<CertificadoInvalidoException>().WithMessage("*ainda não é válido*");
    }
}
