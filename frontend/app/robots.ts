import type { MetadataRoute } from 'next'
import { headers } from 'next/headers'
import { canonicalBaseForHost } from '@/lib/seo'

export default function robots(): MetadataRoute.Robots {
  // Resolvido por host: cada loja tem o SEU sitemap. Fixo no domínio da
  // plataforma, o `robots.txt` de toda vitrine mandava o buscador para um
  // sitemap que não fala das páginas daquela loja.
  const siteUrl = canonicalBaseForHost(headers().get('host'))

  return {
    rules: {
      userAgent: '*',
      allow: '/',
      // `/parceiros` (a landing do programa) fica indexável; só a tela de aceite
      // do convite nominal sai do índice — ela só faz sentido com um token válido.
      //
      // `/perfil/` traz nome, histórico e pontos de uma pessoa; `/mesa/` é
      // entrada de sessão por QR Code dentro do restaurante. Nenhuma das duas
      // tem público vindo da busca, e a primeira é dado pessoal de cliente de
      // loja. As duas também declaram `noindex` na própria página — ver o
      // comentário em app/perfil/[id]/layout.tsx para por que os dois.
      disallow: [
        '/admin/', '/plataforma/', '/contador/', '/cliente/', '/login', '/api/',
        '/parceiros/convite', '/perfil/', '/mesa/',
      ],
    },
    sitemap: `${siteUrl}/sitemap.xml`,
    host: siteUrl,
  }
}
