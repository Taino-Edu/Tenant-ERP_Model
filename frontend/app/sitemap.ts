import type { MetadataRoute } from 'next'
import { headers } from 'next/headers'
import { canonicalBaseForHost, isPlatformHost } from '@/lib/seo'
import { getPublicProductsForSitemap } from '@/lib/serverSitemap'

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const host = (await headers()).get('host')
  const siteUrl = canonicalBaseForHost(host)
  const updated = new Date('2026-08-21T00:00:00-03:00')

  // Vitrine de loja: só o que existe naquele host. As páginas comerciais
  // (/parceiros, /cadastro) são da plataforma e não abrem numa loja; as
  // jurídicas são idênticas em todo host e têm canônico apontando para a
  // plataforma, então listá-las aqui só criaria concorrência entre cópias.
  if (!isPlatformHost(host)) {
    const produtos = await getPublicProductsForSitemap(host)

    return [
      { url: siteUrl, lastModified: updated, changeFrequency: 'daily', priority: 1 },
      { url: `${siteUrl}/produtos`, lastModified: updated, changeFrequency: 'daily', priority: 0.9 },
      // O `lastModified` real de cada produto é o que faz o buscador voltar
      // depois de uma mudança de preço em vez de reindexar no ritmo dele.
      ...produtos.map(p => ({
        url: `${siteUrl}/produtos/${p.id}`,
        lastModified: p.updatedAt,
        changeFrequency: 'weekly' as const,
        priority: 0.7,
      })),
    ]
  }

  return [
    { url: siteUrl, lastModified: updated, changeFrequency: 'weekly', priority: 1 },
    { url: `${siteUrl}/parceiros`, lastModified: updated, changeFrequency: 'monthly', priority: 0.8 },
    // `/cadastro` saiu daqui: parece a inscrição no teste da plataforma, mas é
    // a criação de conta do CLIENTE de uma loja ("acompanhar seus pontos e
    // comandas"). Quem quer testar o Octus vai para o formulário de contato da
    // própria landing (#contato). Anunciar essa URL como página comercial
    // levava quem viesse da busca para um cadastro que não é o dele.
    { url: `${siteUrl}/privacidade`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
    { url: `${siteUrl}/termos`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
    { url: `${siteUrl}/cookies`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
    { url: `${siteUrl}/lgpd`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
  ]
}
