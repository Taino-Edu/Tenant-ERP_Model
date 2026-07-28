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

    // ── Titular do certificado ────────────────────────────────────────────────
    // É o CNPJ do Subject que impede a loja de assinar NFC-e com certificado de
    // outra empresa (uso indevido + rejeição na SEFAZ), então o parser precisa
    // ser previsível nos formatos que as ACs da ICP-Brasil emitem.

    [Theory]
    // Formato padrão do e-CNPJ A1: "RAZAO SOCIAL:CNPJ" no CN.
    [InlineData("CN=EMPRESA TESTE LTDA:11222333000181, OU=Certificado Digital, O=ICP-Brasil, C=BR", "11222333000181")]
    // Ordem invertida do Subject (varia por AC).
    [InlineData("C=BR, O=ICP-Brasil, CN=OUTRA EMPRESA:44555666000172", "44555666000172")]
    // CNPJ sozinho, sem razão social junto.
    [InlineData("CN=11222333000181", "11222333000181")]
    public void ExtrairCnpj_SubjectComCnpj_DeveDevolverOsQuatorzeDigitos(string subject, string esperado)
    {
        FiscalCertificadoService.ExtrairCnpj(subject).Should().Be(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    // e-CPF: 11 dígitos, não é CNPJ.
    [InlineData("CN=FULANO DE TAL:12345678901, O=ICP-Brasil, C=BR")]
    // Sequência de 15+ dígitos não pode virar um "quase CNPJ" truncado.
    [InlineData("CN=EMPRESA:112223330001812, O=ICP-Brasil")]
    public void ExtrairCnpj_SemCnpjValido_DeveDevolverNull(string? subject)
    {
        FiscalCertificadoService.ExtrairCnpj(subject).Should().BeNull();
    }

    [Fact]
    public void Validar_CertificadoSemCnpjNoSubject_NaoQuebra()
    {
        // O self-signed dos testes tem CN sem CNPJ — Validar deve seguir normal
        // e só deixar Cnpj nulo, que é o caso "não consegui identificar".
        var pfxBytes = CreateSelfSignedPfx(Senha, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        var info = new FiscalCertificadoService().Validar(pfxBytes, Senha);

        info.Cnpj.Should().BeNull();
    }
}
