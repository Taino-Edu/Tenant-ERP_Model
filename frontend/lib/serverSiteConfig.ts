// =============================================================================
// serverSiteConfig.ts — Busca SiteConfig do backend, do lado do servidor,
// pro tenant resolvido pelo Host da requisição atual. Usado só por
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
// =============================================================================

// URL interna do container da API, na rede do Docker Compose (ver
// deploy/docker-compose.prod.yml — container_name: cardgamestore_api,
// ASPNETCORE_URLS: http://+:5000). O valor padrão já é o correto pra esse
// deploy específico, então isso funciona sem precisar adicionar nada no
// docker-compose — a env var é só uma válvula de escape se o nome do
// container/porta mudar algum dia.
const INTERNAL_API_URL = process.env.INTERNAL_API_URL || 'http://cardgamestore_api:5000'

export interface TenantSiteIcons {
  faviconUrl?: string | null
  pwaIconUrl?: string | null
  siteName?: string
  updatedAt?: string
}

/**
 * Busca favicon/ícone do PWA/nome do site pro tenant resolvido pelo Host
 * informado. Retorna null em QUALQUER falha (rede, timeout, status != 200,
 * JSON inesperado) — nunca lança, pra generateMetadata/manifest.ts sempre
 * poderem cair no fallback estático sem precisar de try/catch próprio.
 */
export async function getTenantIconsForHost(host: string | null): Promise<TenantSiteIcons | null> {
  if (!host) return null

  try {
    // Query param só pra diferenciar a chave de cache do fetch do Next.js por
    // tenant — o cache dele é baseado na URL (+ alguns options), NÃO no header
    // Host custom que a gente manda pra rotear pro tenant certo. Sem isso,
    // como a URL de baixo é sempre a mesma pra todos os tenants, o primeiro
    // fetch que caísse aqui ficava em cache por 5min e era servido (errado)
    // pra qualquer outra loja que pedisse depois — bug real, achado testando
    // ao vivo (confirmado: o backend respondia certo via curl/wget direto,
    // só o cache do lado do Next.js estava embaralhando os tenants).
    const res = await fetch(`${INTERNAL_API_URL}/api/site-config?_h=${encodeURIComponent(host)}`, {
      headers: { Host: host },
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
