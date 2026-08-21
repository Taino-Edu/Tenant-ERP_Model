import type { Metadata } from 'next'
import { headers } from 'next/headers'
import { getTenantIconsForHost, resolveShareImage } from '@/lib/serverSiteConfig'

// A página é 'use client' (formulário de solicitação e consulta de protocolo),
// então o metadata vive aqui.
//
// Sem isto ela herdava o título do layout raiz — "Octus", exatamente o mesmo da
// home. Duas URLs no sitemap com o mesmo título e a mesma descrição competem
// entre si na busca, e é o tipo de coisa que o Search Console reporta como
// título duplicado depois que já está indexado.
//
// O nome da loja entra no título porque esta tela é POR TENANT: quem exerce
// direito de titular exerce contra o estabelecimento onde comprou, não contra
// "o Octus". Quem procura no Google procura pelo nome da loja.
export async function generateMetadata(): Promise<Metadata> {
  const icons = await getTenantIconsForHost(headers().get('host'))
  const siteName = icons?.siteName || 'Octus'
  const description = `Solicite acesso, correção, exclusão ou portabilidade dos seus dados pessoais em ${siteName}, e acompanhe o protocolo. Direitos garantidos pela LGPD.`

  return {
    title: 'Seus direitos sobre seus dados (LGPD)',
    description,
    alternates: { canonical: '/lgpd' },
    openGraph: {
      title: `Seus direitos sobre seus dados — ${siteName}`,
      description,
      type: 'website',
      // Repetido de propósito: declarar `openGraph` substitui o do layout raiz
      // por inteiro, imagem incluída. Ver resolveShareImage.
      images: [{ url: resolveShareImage(icons), alt: siteName }],
    },
  }
}

export default function LgpdLayout({ children }: { children: React.ReactNode }) {
  return children
}
