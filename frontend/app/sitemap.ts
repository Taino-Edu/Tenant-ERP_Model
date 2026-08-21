import type { MetadataRoute } from 'next'
import { headers } from 'next/headers'
import { canonicalBaseForHost, isPlatformHost } from '@/lib/seo'

export default function sitemap(): MetadataRoute.Sitemap {
  const host = headers().get('host')
  const siteUrl = canonicalBaseForHost(host)
  const updated = new Date('2026-08-21T00:00:00-03:00')

  // Vitrine de loja: só o que existe naquele host. As páginas comerciais
  // (/parceiros, /cadastro) são da plataforma e não abrem numa loja; as
  // jurídicas são idênticas em todo host e têm canônico apontando para a
  // plataforma, então listá-las aqui só criaria concorrência entre cópias.
  //
  // O catálogo de produtos ainda não entra: exige um endpoint público que
  // devolva os produtos com ShowOnSite de cada loja (item 1 do PLANO-SEO).
  // Enquanto isso, `/produtos` linka todos eles e o rastreador chega lá.
  if (!isPlatformHost(host)) {
    return [
      { url: siteUrl, lastModified: updated, changeFrequency: 'daily', priority: 1 },
      { url: `${siteUrl}/produtos`, lastModified: updated, changeFrequency: 'daily', priority: 0.9 },
    ]
  }

  return [
    { url: siteUrl, lastModified: updated, changeFrequency: 'weekly', priority: 1 },
    { url: `${siteUrl}/parceiros`, lastModified: updated, changeFrequency: 'monthly', priority: 0.8 },
    { url: `${siteUrl}/cadastro`, lastModified: updated, changeFrequency: 'monthly', priority: 0.8 },
    { url: `${siteUrl}/privacidade`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
    { url: `${siteUrl}/termos`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
    { url: `${siteUrl}/cookies`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
    { url: `${siteUrl}/lgpd`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
  ]
}
