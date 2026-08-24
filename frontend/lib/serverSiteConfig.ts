// =============================================================================
// serverSiteConfig.ts — Busca favicon/ícone de PWA/nome do backend, do lado do
// servidor, pro tenant resolvido pelo Host da requisição atual. Usado só por
// generateMetadata (layout.tsx) e app/manifest.ts — a API de Metadata do
// Next.js roda sempre no servidor, mesmo num app tão client-heavy quanto
// este, então isso não é "portar pra SSR", é usar uma parte do framework
// que nunca foi client-rendered.
//
// Em produção o nginx roteia /api/* direto pro container da API sem passar
// pelo Next.js (ver next.config.js) — então essa é a PRIMEIRA vez que o
// processo do Next.js precisa chamar o backend diretamente. Por isso o
// fallback pra null em qualquer falha não é só polimento, é o mecanismo de
// segurança principal: uma falha de rede/timeout aqui NUNCA pode quebrar o
// carregamento da página, só faz cair no ícone/manifest estático de sempre.
//
// Tentativa original era mandar o Host certo via `fetch(url, { headers: {
// Host: host } })` — não funciona: Host é um "forbidden header name" do
// próprio Fetch spec, o undici (usado pelo Next.js) ignora silenciosamente
// qualquer tentativa de sobrescrevê-lo, sempre manda o Host derivado da URL
// de destino. Corrigido extraindo o SLUG do host (mesma regra de
// TenantResolutionMiddleware.ExtractSlug) e chamando um endpoint público que
// recebe o slug como query param comum — dado já público (aparece em toda
// URL de loja), sem precisar mexer em header nenhum.
// =============================================================================

export const INTERNAL_API_URL = process.env.INTERNAL_API_URL || 'http://cardgamestore_api:5000'
// NEXT_PUBLIC_* e gravado durante o build, mas o bundle standalone ainda pode
// ler process.env no container final, onde essa variavel nao era propagada.
// O fallback precisa ser o mesmo de lib/seo.ts; vazio impediria o SSR de
// reconhecer qualquer tenant e esconderia do rastreador quem opera o login.
const ROOT_DOMAIN = process.env.NEXT_PUBLIC_ROOT_DOMAIN || '3esysten.com.br'

export interface TenantSiteIcons {
  faviconUrl?: string | null
  pwaIconUrl?: string | null
  siteName?: string
  heroSubtitle?: string
  addressLine?: string
  updatedAt?: string
}

/** Mesma regra de CardGameStore/Multitenancy/TenantResolutionMiddleware.ExtractSlug —
 * host precisa terminar em ".{ROOT_DOMAIN}" (subdomínio de UM nível só, tenant de
 * verdade); domínio raiz, www, IP puro ou host que não bate com o sufixo → null
 * (não tem tenant pra resolver, ex: página institucional). Exportado pra
 * lib/serverProduct.ts reaproveitar a mesma regra. */
export function extractSlug(host: string | null): string | null {
  if (!host || !ROOT_DOMAIN) return null

  // headers().get('host') devolve o header cru, com porta se o cliente mandou
  // uma (ex: dev local "loja.localhost:3000") — diferente do HostString.Host
  // do ASP.NET Core (TenantResolutionMiddleware), que já vem sem porta. Sem
  // isso o slug nunca batia em dev (bug real: achado testando no navegador,
  // via curl com Host sem porta o problema não aparecia).
  const hostname = host.split(':')[0]

  const suffix = '.' + ROOT_DOMAIN
  if (!hostname.toLowerCase().endsWith(suffix.toLowerCase())) return null

  const slug = hostname.slice(0, hostname.length - suffix.length)
  if (!slug || slug.includes('.') || slug.toLowerCase() === 'www') return null

  return slug
}

/**
 * Busca favicon/ícone do PWA/nome do site pro tenant resolvido pelo Host
 * informado. Retorna null em QUALQUER falha (rede, timeout, status != 200,
 * JSON inesperado, ou host sem tenant — ex: domínio raiz) — nunca lança, pra
 * generateMetadata/manifest.ts sempre poderem cair no fallback estático sem
 * precisar de try/catch próprio.
 */
export async function getTenantIconsForHost(host: string | null): Promise<TenantSiteIcons | null> {
  const slug = extractSlug(host)
  if (!slug) return null

  try {
    const res = await fetch(`${INTERNAL_API_URL}/api/public/site-icons?slug=${encodeURIComponent(slug)}`, {
      signal: AbortSignal.timeout(2000),
      next: { revalidate: 300 },
    })

    if (!res.ok) {
      avisarFalha(slug, `HTTP ${res.status}`)
      return null
    }

    const data = await res.json()
    if (!data || typeof data !== 'object') return null

    return {
      faviconUrl:   typeof data.faviconUrl   === 'string' ? data.faviconUrl   : null,
      pwaIconUrl:   typeof data.pwaIconUrl    === 'string' ? data.pwaIconUrl   : null,
      siteName:     typeof data.siteName      === 'string' ? data.siteName     : undefined,
      heroSubtitle: typeof data.heroSubtitle  === 'string' ? data.heroSubtitle : undefined,
      addressLine:  typeof data.addressLine   === 'string' ? data.addressLine  : undefined,
      updatedAt:    typeof data.updatedAt      === 'string' ? data.updatedAt    : undefined,
    }
  } catch (erro) {
    avisarFalha(slug, erro instanceof Error ? erro.message : String(erro))
    return null
  }
}

/**
 * Registra que a resolução do tenant falhou — no log do servidor, não na tela.
 *
 * O `catch { return null }` acima é a rede de segurança certa: uma falha aqui
 * NUNCA pode derrubar o carregamento da página. Mas ele também tornava a falha
 * invisível, e o custo disso apareceu em 21/08/2026: toda vitrine estava
 * publicando `<title>Octus</title>` e um manifest genérico em vez do nome da
 * loja, porque o fetch para a API interna não completava. Nada no log, nada na
 * tela quebrada — só o Google indexando dezenas de lojas com o mesmo título.
 *
 * "Cai no fallback em silêncio" e "cai no fallback e conta" são coisas
 * diferentes. Host sem tenant (domínio raiz) sai antes daqui, então tudo que
 * chega nesta função é falha de verdade.
 */
function avisarFalha(slug: string, motivo: string) {
  console.warn(
    `[tenant-seo] não resolvi o tenant "${slug}" em ${INTERNAL_API_URL} (${motivo}). ` +
    'A página cai no título/ícone genéricos "Octus" — confira se o container do frontend alcança a API.',
  )
}

/** Adiciona um query param de cache-busting (?v=timestamp) numa URL de ícone,
 * pra navegador não continuar servindo versão antiga depois de um re-upload. */
export function withCacheBust(url: string, updatedAt?: string): string {
  if (!updatedAt) return url
  const v = encodeURIComponent(updatedAt)
  return url.includes('?') ? `${url}&v=${v}` : `${url}?v=${v}`
}

/** Imagem de compartilhamento (og:image) da loja resolvida pelo Host.
 *
 *  Centralizada porque metadados no App Router NÃO mesclam campo a campo: um
 *  `openGraph` declarado numa página SUBSTITUI o do layout inteiro. Quando o
 *  og:image morava só no layout raiz, bastava a página de catálogo declarar um
 *  og:title próprio para o cartão de compartilhamento perder a imagem — e
 *  ninguém percebe, porque a página continua funcionando; só o link colado no
 *  WhatsApp fica sem foto.
 *
 *  Toda página que declarar `openGraph` precisa chamar isto. */
export function resolveShareImage(icons: TenantSiteIcons | null): string {
  return icons?.pwaIconUrl
    ? withCacheBust(icons.pwaIconUrl, icons.updatedAt)
    : '/institutional/octus-hero-waves.png'
}
