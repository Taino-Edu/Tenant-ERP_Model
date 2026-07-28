// =============================================================================
// FiscalCertificadoService.cs — Validação e leitura do certificado digital A1
// usado para assinar NFC-e. Não depende de banco — puro X509.
// =============================================================================

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

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

            var agora = DateTime.UtcNow;
            var validoDe = cert.NotBefore.ToUniversalTime();
            var validoAte = cert.NotAfter.ToUniversalTime();
            if (validoDe > agora)
                throw new CertificadoInvalidoException(
                    $"O certificado ainda não é válido. Início da validade: {validoDe:dd/MM/yyyy HH:mm} UTC.");
            if (validoAte <= agora)
                throw new CertificadoInvalidoException(
                    $"O certificado venceu em {validoAte:dd/MM/yyyy HH:mm} UTC. Envie um certificado A1 válido antes de emitir.");

            // X509Certificate2.NotBefore/NotAfter vêm com Kind=Local (conversão do .NET a
            // partir do UTC original do certificado) — Npgsql rejeita gravar DateTime não-UTC
            // em timestamptz. ToUniversalTime() converte preservando o instante real.
            return new CertificadoInfo(cert.Subject, validoDe, validoAte, ExtrairCnpj(cert.Subject));
        }
        catch (CryptographicException ex)
        {
            throw new CertificadoInvalidoException(
                $"Senha incorreta ou arquivo de certificado inválido. Detalhe técnico: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Tira o CNPJ do titular do Subject do certificado. Num A1 e-CNPJ da
    /// ICP-Brasil o CN vem como "RAZAO SOCIAL:00000000000000" — o CNPJ é a
    /// sequência de 14 dígitos. Devolve null quando não acha (e-CPF, formato
    /// fora do padrão), e nesse caso quem chama decide o que fazer.
    /// </summary>
    public static string? ExtrairCnpj(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return null;

        // Percorre todas as sequências de 14 dígitos e fica na primeira que passa
        // no dígito verificador: o Subject também carrega número de série e OIDs,
        // e aceitar qualquer 14 dígitos faria a comparação de titularidade bater
        // contra um número que não é CNPJ nenhum.
        foreach (Match m in Regex.Matches(subject, @"(?<!\d)(\d{14})(?!\d)"))
            if (CnpjTemDigitoValido(m.Groups[1].Value))
                return m.Groups[1].Value;

        return null;
    }

    /// <summary>Confere os dois dígitos verificadores do CNPJ (módulo 11).</summary>
    public static bool CnpjTemDigitoValido(string cnpj)
    {
        if (cnpj.Length != 14 || !cnpj.All(char.IsDigit)) return false;
        // 00000000000000, 11111111111111... passam na conta mas não existem.
        if (cnpj.Distinct().Count() == 1) return false;

        static int Digito(string parcial, int[] pesos)
        {
            var soma = parcial.Select((c, i) => (c - '0') * pesos[i]).Sum();
            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        var dv1 = Digito(cnpj[..12], pesos1);
        var dv2 = Digito(cnpj[..12] + dv1, pesos2);

        return cnpj[12] - '0' == dv1 && cnpj[13] - '0' == dv2;
    }
}

public record CertificadoInfo(string Subject, DateTime NotBefore, DateTime NotAfter, string? Cnpj = null);

public class CertificadoInvalidoException : Exception
{
    public CertificadoInvalidoException(string message, Exception? inner = null) : base(message, inner) { }
}
