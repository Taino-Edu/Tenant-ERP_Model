import type { Metadata } from 'next'
import { headers } from 'next/headers'
import { getPublicProductForHost } from '@/lib/serverProduct'
import { getTenantIconsForHost, resolveShareImage } from '@/lib/serverSiteConfig'
import ProdutoDetalheClient from './ProdutoDetalheClient'

function formatPriceBRL(cents: number): string {
  return (cents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

// Server Component fino — existe só pra buscar dados do produto no servidor e
// gerar title/OG/JSON-LD (generateMetadata não pode viver num arquivo
// 'use client'). Toda a interação (carrinho, login, lista de espera, tema)
// continua 100% em ProdutoDetalheClient.tsx, sem nenhuma mudança de lógica —
// esse componente não busca o produto de novo pro client, só pro <head>.
export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const [{ id }, requestHeaders] = await Promise.all([params, headers()])
  const host = requestHeaders.get('host')
  const product = await getPublicProductForHost(host, id)

  // O canônico é declarado nos DOIS caminhos, e não por acaso: metadados no
  // App Router são herdados campo a campo do layout acima. O layout de
  // /produtos define `canonical: '/produtos'`, então uma página de produto que
  // não declarasse o próprio herdaria a da listagem — ou seja, diria ao Google
  // "não me indexe, indexe a listagem". Todo o catálogo sumiria da busca.
  const canonical = `/produtos/${id}`

  if (!product) return { title: 'Produto', alternates: { canonical } }

  // Produto sem foto cadastrada não fica sem cartão: cai na imagem da loja.
  // Antes, `images: undefined` aqui apagava também a do layout raiz (metadados
  // substituem o objeto `openGraph` inteiro), e o link ia para o WhatsApp sem
  // imagem nenhuma — justamente o produto que o lojista está divulgando.
  const shareImage = product.imageUrl || resolveShareImage(await getTenantIconsForHost(host))

  const price = formatPriceBRL(product.priceInCents)
  const description = product.description
    ? `${product.description} — ${price}`
    : `Confira: ${product.name} por ${price}.`

  return {
    title: product.name,
    description,
    alternates: { canonical },
    openGraph: {
      title: product.name,
      description,
      type: 'website',
      images: [{ url: shareImage, alt: product.name }],
    },
    twitter: {
      card: 'summary_large_image',
      title: product.name,
      description,
      images: [shareImage],
    },
  }
}

export default async function ProductPage({ params }: { params: Promise<{ id: string }> }) {
  const [{ id }, requestHeaders] = await Promise.all([params, headers()])
  const host = requestHeaders.get('host')
  const product = await getPublicProductForHost(host, id)

  return (
    <>
      {product && (
        <script
          type="application/ld+json"
          // eslint-disable-next-line react/no-danger
          dangerouslySetInnerHTML={{
            __html: JSON.stringify({
              '@context': 'https://schema.org',
              '@type': 'Product',
              name: product.name,
              description: product.description || undefined,
              image: product.imageUrl || undefined,
              offers: {
                '@type': 'Offer',
                priceCurrency: 'BRL',
                price: (product.priceInCents / 100).toFixed(2),
                availability: product.stockQuantity > 0
                  ? 'https://schema.org/InStock'
                  : 'https://schema.org/OutOfStock',
              },
            }),
          }}
        />
      )}
      <ProdutoDetalheClient />
    </>
  )
}
