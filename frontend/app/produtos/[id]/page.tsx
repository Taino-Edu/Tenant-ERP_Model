import type { Metadata } from 'next'
import { headers } from 'next/headers'
import { getPublicProductForHost } from '@/lib/serverProduct'
import ProdutoDetalheClient from './ProdutoDetalheClient'

function formatPriceBRL(cents: number): string {
  return (cents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

// Server Component fino — existe só pra buscar dados do produto no servidor e
// gerar title/OG/JSON-LD (generateMetadata não pode viver num arquivo
// 'use client'). Toda a interação (carrinho, login, lista de espera, tema)
// continua 100% em ProdutoDetalheClient.tsx, sem nenhuma mudança de lógica —
// esse componente não busca o produto de novo pro client, só pro <head>.
export async function generateMetadata({ params }: { params: { id: string } }): Promise<Metadata> {
  const host = headers().get('host')
  const product = await getPublicProductForHost(host, params.id)

  if (!product) return { title: 'Produto' }

  const price = formatPriceBRL(product.priceInCents)
  const description = product.description
    ? `${product.description} — ${price}`
    : `Confira: ${product.name} por ${price}.`

  return {
    title: product.name,
    description,
    openGraph: {
      title: product.name,
      description,
      type: 'website',
      images: product.imageUrl ? [{ url: product.imageUrl }] : undefined,
    },
    twitter: {
      card: product.imageUrl ? 'summary_large_image' : 'summary',
      title: product.name,
      description,
      images: product.imageUrl ? [product.imageUrl] : undefined,
    },
  }
}

export default async function ProductPage({ params }: { params: { id: string } }) {
  const host = headers().get('host')
  const product = await getPublicProductForHost(host, params.id)

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
