// =============================================================================
// serverProduct.ts — Busca dados de um produto do backend, do lado do
// servidor, pro tenant resolvido pelo Host da requisição atual. Usado só por
// generateMetadata em app/produtos/[id]/page.tsx pra montar title/OG/JSON-LD
// com nome, preço e imagem reais — mesmo padrão de lib/serverSiteConfig.ts
// (mesma limitação de Host "forbidden header" do fetch(), resolvido via slug
// como query param em vez de header).
// =============================================================================

import { extractSlug, INTERNAL_API_URL } from './serverSiteConfig'

export interface PublicProductSeo {
  name: string
  description?: string | null
  priceInCents: number
  imageUrl?: string | null
  stockQuantity: number
}

/**
 * Busca nome/descrição/preço/imagem do produto pro tenant resolvido pelo Host
 * informado. Retorna null em QUALQUER falha (rede, timeout, status != 200,
 * JSON inesperado, ou host sem tenant) — nunca lança, pra generateMetadata
 * sempre poder cair no fallback genérico sem precisar de try/catch próprio.
 */
export async function getPublicProductForHost(
  host: string | null,
  productId: string
): Promise<PublicProductSeo | null> {
  const slug = extractSlug(host)
  if (!slug) return null

  try {
    const res = await fetch(
      `${INTERNAL_API_URL}/api/public/product?slug=${encodeURIComponent(slug)}&id=${encodeURIComponent(productId)}`,
      {
        signal: AbortSignal.timeout(2000),
        next: { revalidate: 60 },
      }
    )

    if (!res.ok) return null

    const data = await res.json()
    if (!data || typeof data !== 'object' || typeof data.name !== 'string') return null

    return {
      name:          data.name,
      description:   typeof data.description  === 'string' ? data.description  : null,
      priceInCents:  typeof data.priceInCents  === 'number' ? data.priceInCents  : 0,
      imageUrl:      typeof data.imageUrl      === 'string' ? data.imageUrl      : null,
      stockQuantity: typeof data.stockQuantity === 'number' ? data.stockQuantity : 0,
    }
  } catch {
    return null
  }
}
