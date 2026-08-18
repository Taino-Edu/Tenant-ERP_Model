import type { Metadata } from 'next'

const SITE_URL = 'https://3esysten.com.br'
const PAGE_URL = `${SITE_URL}/parceiros`
const TITLE = 'Programa de Afiliados Octus | Indique e receba comissão recorrente'
const DESCRIPTION =
  'Indique o Octus ERP e receba 30% da taxa de implantação e 5% de cada mensalidade paga, enquanto a indicação seguir ativa. Sem meta, sem exclusividade e com contrato assinado eletronicamente.'

// Este layout também embrulha /parceiros/convite, que é a tela de aceite de um
// convite nominal. Lá o `metadata` é sobrescrito pelo layout daquela rota
// (inclusive o noindex) — o JSON-LD do FAQ, por sua vez, é renderizado DENTRO
// de page.tsx justamente para não vazar para a tela de assinatura.
export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: { absolute: TITLE },
  description: DESCRIPTION,
  keywords: [
    'programa de afiliados ERP', 'indicar sistema de gestão', 'comissão recorrente software',
    'parceria contador ERP', 'renda extra indicação', 'Octus afiliados',
  ],
  alternates: { canonical: PAGE_URL },
  robots: { index: true, follow: true },
  openGraph: {
    title: TITLE,
    description: DESCRIPTION,
    url: PAGE_URL,
    siteName: '3E Systen',
    locale: 'pt_BR',
    type: 'website',
    images: [{ url: '/institutional/octus-hero-waves.png', width: 1672, height: 941, alt: 'Programa de Afiliados Octus' }],
  },
  twitter: { card: 'summary_large_image', title: TITLE, description: DESCRIPTION, images: ['/institutional/octus-hero-waves.png'] },
}

export default function ParceirosLayout({ children }: { children: React.ReactNode }) {
  return children
}
