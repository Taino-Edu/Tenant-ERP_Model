// =============================================================================
// serverSitemap.ts — Lista os produtos públicos de uma loja para o sitemap.
//
// Mesmo padrão de lib/serverProduct.ts e lib/serverSiteConfig.ts: o Next.js
// não consegue mandar o header Host num fetch() server-side (é "forbidden
// header name" no Fetch spec e o undici ignora em silêncio), então o tenant é
// resolvido pelo SLUG extraído do host, como query param.
//
// Falha SEMPRE cai em lista vazia. Um sitemap sem os produtos ainda é um
// sitemap válido — com `/` e `/produtos`, o rastreador chega no catálogo pelos
// links. Um sitemap que devolve 500 porque a API demorou é uma URL quebrada no
// Search Console, que é bem pior.
// =============================================================================

import { extractSlug, INTERNAL_API_URL } from './serverSiteConfig'

export interface SitemapProduct {
  id: string
  updatedAt: Date
}

export async function getPublicProductsForSitemap(host: string | null): Promise<SitemapProduct[]> {
  const slug = extractSlug(host)
  if (!slug) return []

  try {
    const res = await fetch(
      `${INTERNAL_API_URL}/api/public/sitemap?slug=${encodeURIComponent(slug)}`,
      {
        // Mais folgado que os 2s do serverProduct: aqui a resposta pode ter
        // milhares de linhas, e quem espera é um rastreador, não uma pessoa
        // olhando a página carregar.
        signal: AbortSignal.timeout(5000),
        // 1h: catálogo muda o dia inteiro, mas sitemap relido de hora em hora
        // já é mais frequente do que qualquer buscador vem buscar.
        next: { revalidate: 3600 },
      }
    )

    if (!res.ok) return []

    const data = await res.json()
    if (!Array.isArray(data)) return []

    return data.flatMap((item: unknown) => {
      if (!item || typeof item !== 'object') return []
      const { id, updatedAt } = item as { id?: unknown; updatedAt?: unknown }
      if (typeof id !== 'string') return []
      const data = typeof updatedAt === 'string' ? new Date(updatedAt) : new Date()
      // Data inválida vira "agora": `lastmod` com "Invalid Date" derruba a
      // validação do XML inteiro, e uma data errada custa menos que isso.
      return [{ id, updatedAt: Number.isNaN(data.getTime()) ? new Date() : data }]
    })
  } catch {
    return []
  }
}
