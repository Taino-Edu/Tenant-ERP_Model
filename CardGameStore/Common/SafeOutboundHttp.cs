// =============================================================================
// SafeOutboundHttp.cs — Proteção contra SSRF pra requisições feitas a partir de
// dados vindos de terceiros (ex: website de um negócio achado no OpenStreetMap
// — dado editável por qualquer pessoa, que um atacante pode controlar via um
// cadastro malicioso).
//
// Bloqueia loopback/private/link-local (isso já cobre o endpoint de metadados
// de nuvem 169.254.169.254) tanto no host quanto em qualquer redirect. A
// validação do IP acontece no ConnectCallback do SocketsHttpHandler — ou seja,
// no momento real da conexão TCP, não só na resolução DNS prévia (que sozinha
// seria vulnerável a DNS rebinding: IP público na hora da checagem, IP privado
// na hora da conexão de verdade).
// =============================================================================

using System.Net;
using System.Net.Sockets;

namespace CardGameStore.Common;

public static class SafeOutboundHttp
{
    /// <summary>Handler que só permite conectar em IPs públicos — usar em
    /// qualquer HttpClient que vá buscar uma URL vinda de dado externo não
    /// confiável.</summary>
    public static SocketsHttpHandler CreatePublicOnlyHandler(int maxRedirects = 3) => new()
    {
        AllowAutoRedirect = false, // redirects seguidos manualmente, revalidando a cada hop
        ConnectCallback = async (context, cancellationToken) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            var safe = addresses.FirstOrDefault(a => !IsPrivateOrReserved(a))
                ?? throw new InvalidOperationException($"Destino não permitido (sem IP público): {context.DnsEndPoint.Host}");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(safe, context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    /// <summary>true se a URL é http/https com host bem-formado — checagem
    /// rápida antes mesmo de tentar resolver DNS.</summary>
    public static bool IsPublicHttpUrl(string? url, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return false;

        uri = parsed;
        return true;
    }

    private static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            // 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16 (link-local,
            // inclui o endpoint de metadados de nuvem 169.254.169.254)
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254)
                || bytes[0] == 127;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
            // fc00::/7 — unique local address (equivalente IPv6 do range privado)
            return (bytes[0] & 0xFE) == 0xFC;
        }

        return true; // família desconhecida — bloqueia por padrão
    }
}
