import type { MetadataRoute } from 'next'

const SITE_URL = 'https://3esysten.com.br'

export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: '*',
      allow: '/',
      // `/parceiros` (a landing do programa) fica indexável; só a tela de aceite
      // do convite nominal sai do índice — ela só faz sentido com um token válido.
      disallow: ['/admin/', '/plataforma/', '/contador/', '/cliente/', '/login', '/api/', '/parceiros/convite'],
    },
    sitemap: `${SITE_URL}/sitemap.xml`,
    host: SITE_URL,
  }
}
