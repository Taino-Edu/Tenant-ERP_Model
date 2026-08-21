// =============================================================================
// seo.ts — De quem é a URL que o Google está olhando.
//
// O app responde no domínio da plataforma E no subdomínio de cada loja E no
// domínio próprio de quem contratou. Isso é invisível para quase todo o código,
// mas não para buscador: `robots.txt`, `sitemap.xml` e `<link rel=canonical>`
// são justamente as três respostas que MUDAM conforme o host que pediu.
//
// Antes eram fixas em `https://3esysten.com.br`. O efeito prático: a loja
// `fulano.3esysten.com.br` servia um sitemap listando as páginas comerciais da
// plataforma — nenhuma delas existindo naquele host — e um `robots.txt`
// declarando outro domínio como o canônico dela.
// =============================================================================

/** Domínio raiz da plataforma. O fallback existe porque `robots.ts`/`sitemap.ts`
 *  também rodam em build local e em CI, onde a env não está definida — e um
 *  sitemap apontando para `https://` seria pior que um apontando para o domínio
 *  certo. */
export const ROOT_DOMAIN = process.env.NEXT_PUBLIC_ROOT_DOMAIN || '3esysten.com.br'

/** URL da plataforma, sem barra no fim. É o canônico das páginas que são as
 *  MESMAS em todo host (política de privacidade, termos, cookies): elas falam
 *  da 3E Systen, não da loja, e sem isso o Google vê o mesmo texto repetido em
 *  um host por cliente e escolhe sozinho qual indexar. */
export const PLATFORM_URL = `https://${ROOT_DOMAIN}`

/** Base absoluta para o host que fez a requisição. `localhost` cai em http
 *  porque em desenvolvimento não há TLS. */
export function siteUrlFromHost(host: string | null): string {
  const hostname = (host || '').split(':')[0].toLowerCase()
  if (!hostname) return PLATFORM_URL
  const protocol = hostname === 'localhost' || hostname.endsWith('.localhost') ? 'http' : 'https'
  return `${protocol}://${host}`
}

/** O host é a própria plataforma (com ou sem `www`), e não a vitrine de uma
 *  loja? É o que separa "servir o sitemap comercial" de "servir o da loja". */
export function isPlatformHost(host: string | null): boolean {
  const hostname = (host || '').split(':')[0].toLowerCase()
  return hostname === ROOT_DOMAIN || hostname === `www.${ROOT_DOMAIN}`
}

/** Base canônica para robots/sitemap. Difere de `siteUrlFromHost` num ponto:
 *  `www` colapsa no apex. Sitemap e canônico têm que concordar — a landing
 *  declara canônico no apex, então um sitemap servido em `www` listando URLs
 *  com `www` entregaria ao Google uma lista de páginas que ele já sabe que não
 *  são as canônicas. Loja segue o próprio host. */
export function canonicalBaseForHost(host: string | null): string {
  return isPlatformHost(host) ? PLATFORM_URL : siteUrlFromHost(host)
}
