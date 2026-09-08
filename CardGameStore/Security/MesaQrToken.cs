using System.Security.Cryptography;
using System.Text;

namespace CardGameStore.Security;

/// <summary>
/// Token que acompanha o QR Code da mesa e serve de prova (fraca, mas real) de
/// que quem está pedindo viu o código impresso.
///
/// POR QUE EXISTE
/// A URL do QR era só `/mesa/5` — adivinhável por qualquer um, de qualquer
/// lugar. Isso não tinha custo enquanto o quick-login não decidia nada com base
/// nela, mas passou a ter quando surgiu a necessidade de o cliente sem senha
/// retomar a própria conta: sem algo imprevisível na URL, "estar na loja" não
/// se distingue de "estar em casa com o CPF de alguém", e a retomada viraria
/// sequestro de conta — com pontos, histórico e, pior, crediário aberto em nome
/// da vítima.
///
/// O QUE ELE NÃO É
/// Não é autenticação. Um QR impresso é público pra quem senta na mesa, e o
/// token é o mesmo até o segredo mudar. Ele eleva o custo de "sabe um CPF" pra
/// "viu o código daquela loja", e é por isso que só libera conta SEM senha:
/// conta com senha nunca é retomada por token, só por login.
///
/// SEGREDO
/// `Security:MesaQrSecret` quando configurado. Sem ele, deriva do
/// `JwtSettings:SecretKey` — que já é obrigatório e forte em qualquer deploy
/// (o setup.sh gera aleatório). A derivação usa rótulo próprio pra que este
/// token não sirva pra nada além disto, mesmo compartilhando a origem.
/// </summary>
public static class MesaQrToken
{
    private const string Rotulo = "mesa-qr-v1";

    /// <summary>
    /// 16 caracteres base64url (96 bits). Curto o suficiente pra não inchar o
    /// QR Code — o que aumentaria a densidade e atrapalharia a leitura em
    /// impressão pequena — e longo demais pra tentativa e erro, ainda mais com
    /// o rate limit de `auth` na frente.
    /// </summary>
    internal const int TamanhoToken = 16;

    public static string Compute(Guid tenantId, string mesa, string secret)
    {
        var chave = Encoding.UTF8.GetBytes($"{Rotulo}:{secret}");
        // O separador é `\n` porque não pode aparecer no nome da mesa: sem ele,
        // as mesas "1" e "12" com tenants diferentes poderiam colidir na
        // concatenação e um token valeria para a outra.
        var dados = Encoding.UTF8.GetBytes($"{tenantId:N}\n{mesa.Trim()}");
        var hash  = HMACSHA256.HashData(chave, dados);
        return Convert.ToBase64String(hash)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=')
            [..TamanhoToken];
    }

    /// <summary>
    /// Comparação em tempo constante: comparar string por string vazaria, pelo
    /// tempo de resposta, quantos caracteres iniciais o palpite acertou, e isso
    /// transforma 96 bits de busca em 16 buscas de um caractere.
    /// </summary>
    public static bool Verify(Guid tenantId, string? mesa, string? tokenRecebido, string secret)
    {
        if (string.IsNullOrWhiteSpace(mesa) || string.IsNullOrWhiteSpace(tokenRecebido))
            return false;

        var esperado = Compute(tenantId, mesa, secret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(esperado),
            Encoding.UTF8.GetBytes(tokenRecebido));
    }

    /// <summary>Segredo efetivo — ver a nota de SEGREDO na classe.</summary>
    public static string ResolveSecret(IConfiguration config)
    {
        var proprio = config["Security:MesaQrSecret"];
        if (!string.IsNullOrWhiteSpace(proprio)) return proprio;

        var jwt = config["JwtSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(jwt))
            throw new InvalidOperationException(
                "Nem Security:MesaQrSecret nem JwtSettings:SecretKey estão configurados — " +
                "sem segredo não há como assinar o token do QR Code da mesa.");
        return jwt;
    }
}
