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

const INTERNAL_API_URL = process.env.INTERNAL_API_URL || 'http://cardgamestore_api:5000'
const ROOT_DOMAIN = process.env.NEXT_PUBLIC_ROOT_DOMAIN || ''

export interface TenantSiteIcons {
  faviconUrl?: string | null
  pwaIconUrl?: string | null
  siteName?: string
  updatedAt?: string
}

/** Mesma regra de CardGameStore/Multitenancy/TenantResolutionMiddleware.ExtractSlug —
 * host precisa terminar em ".{ROOT_DOMAIN}" (subdomínio de UM nível só, tenant de
 * verdade); domínio raiz, www, IP puro ou host que não bate com o sufixo → null
 * (não tem tenant pra resolver, ex: página institucional). */
function extractSlug(host: string | null): string | null {
  if (!host || !ROOT_DOMAIN) return null

  const suffix = '.' + ROOT_DOMAIN
  if (!host.toLowerCase().endsWith(suffix.toLowerCase())) return null

  const slug = host.slice(0, host.length - suffix.length)
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

    if (!res.ok) return null

    const data = await res.json()
    if (!data || typeof data !== 'object') return null

    return {
      faviconUrl: typeof data.faviconUrl === 'string' ? data.faviconUrl : null,
      pwaIconUrl: typeof data.pwaIconUrl === 'string' ? data.pwaIconUrl : null,
      siteName:   typeof data.siteName   === 'string' ? data.siteName   : undefined,
      updatedAt:  typeof data.updatedAt  === 'string' ? data.updatedAt  : undefined,
    }
  } catch {
    return null
  }
}

/** Adiciona um query param de cache-busting (?v=timestamp) numa URL de ícone,
 * pra navegador não continuar servindo versão antiga depois de um re-upload. */
export function withCacheBust(url: string, updatedAt?: string): string {
  if (!updatedAt) return url
  const v = encodeURIComponent(updatedAt)
  return url.includes('?') ? `${url}&v=${v}` : `${url}?v=${v}`
}
