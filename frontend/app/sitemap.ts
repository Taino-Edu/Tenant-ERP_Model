import type { MetadataRoute } from 'next'

const SITE_URL = 'https://3esysten.com.br'

export default function sitemap(): MetadataRoute.Sitemap {
  const updated = new Date('2026-08-11T00:00:00-03:00')
  return [
    { url: SITE_URL, lastModified: updated, changeFrequency: 'weekly', priority: 1 },
    { url: `${SITE_URL}/parceiros`, lastModified: updated, changeFrequency: 'monthly', priority: 0.8 },
    { url: `${SITE_URL}/cadastro`, lastModified: updated, changeFrequency: 'monthly', priority: 0.8 },
    { url: `${SITE_URL}/privacidade`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
    { url: `${SITE_URL}/termos`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
    { url: `${SITE_URL}/cookies`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
    { url: `${SITE_URL}/lgpd`, lastModified: updated, changeFrequency: 'yearly', priority: 0.3 },
  ]
}
