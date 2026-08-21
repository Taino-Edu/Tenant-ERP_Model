import type { Metadata } from 'next'
import { headers } from 'next/headers'
import { getTenantIconsForHost, resolveShareImage } from '@/lib/serverSiteConfig'

// A listagem é 'use client' (busca, filtro por categoria, tema), e
// generateMetadata não pode viver num arquivo client — daí este layout, mesmo
// arranjo que app/produtos/[id]/page.tsx já usa.
//
// Sem isto, a página de catálogo herdava o título da loja e mais nada: no
// resultado de busca, a home e o catálogo apareciam com o MESMO título e a
// mesma descrição, competindo entre si por uma busca que devia levar ao
// catálogo.
export async function generateMetadata(): Promise<Metadata> {
  const icons = await getTenantIconsForHost(headers().get('host'))
  const siteName = icons?.siteName || 'Loja'

  return {
    title: 'Produtos',
    description: `Catálogo completo de ${siteName}. Veja preços, disponibilidade e compre pelo site.`,
    alternates: { canonical: '/produtos' },
    openGraph: {
      title: `Produtos — ${siteName}`,
      description: `Catálogo completo de ${siteName}.`,
      type: 'website',
      // Repetido de propósito: declarar `openGraph` aqui substitui o do layout
      // raiz por inteiro, imagem incluída. Ver resolveShareImage.
      images: [{ url: resolveShareImage(icons), alt: siteName }],
    },
  }
}

export default function ProdutosLayout({ children }: { children: React.ReactNode }) {
  return children
}
