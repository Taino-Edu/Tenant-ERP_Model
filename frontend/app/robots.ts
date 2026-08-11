import type { MetadataRoute } from 'next'

const SITE_URL = 'https://3esysten.com.br'

export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: '*',
      allow: '/',
      disallow: ['/admin/', '/plataforma/', '/contador/', '/cliente/', '/login', '/api/'],
    },
    sitemap: `${SITE_URL}/sitemap.xml`,
    host: SITE_URL,
  }
}
