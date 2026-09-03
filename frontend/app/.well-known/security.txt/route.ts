// RFC 9116: canal público para pesquisadores comunicarem vulnerabilidades.
// A validade precisa ser renovada antes desta data (recomendação: menos de 1 ano).
const SECURITY_TEXT = `Contact: mailto:3esysten@gmail.com
Expires: 2027-03-01T00:00:00Z
Preferred-Languages: pt-BR, en
Canonical: https://3esysten.com.br/.well-known/security.txt
`

export const dynamic = 'force-static'

export function GET() {
  return new Response(SECURITY_TEXT, {
    headers: {
      'Content-Type': 'text/plain; charset=utf-8',
      'Cache-Control': 'public, s-maxage=86400, stale-while-revalidate=604800',
    },
  })
}
