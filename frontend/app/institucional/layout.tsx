import type { Metadata } from 'next'

const SITE_URL = 'https://3esysten.com.br'
const TITLE = 'Octus ERP para varejo e restaurantes | 3E Systen'
const DESCRIPTION =
  'Conheça o Octus: ERP personalizável com PDV, estoque, fiscal, financeiro, crediário, portal do contador e módulo opcional para restaurantes. Teste grátis por 15 dias.'

export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: { absolute: TITLE },
  description: DESCRIPTION,
  keywords: [
    'ERP para varejo', 'sistema de gestão para lojas', 'PDV com NFC-e',
    'software para restaurante', 'controle de estoque', 'Octus ERP',
  ],
  alternates: { canonical: SITE_URL },
  robots: { index: true, follow: true },
  openGraph: {
    title: TITLE,
    description: DESCRIPTION,
    url: SITE_URL,
    siteName: '3E Systen',
    locale: 'pt_BR',
    type: 'website',
    images: [{ url: '/institutional/octus-hero-waves.png', width: 1672, height: 941, alt: 'Octus ERP para varejo e restaurantes' }],
  },
  twitter: { card: 'summary_large_image', title: TITLE, description: DESCRIPTION, images: ['/institutional/octus-hero-waves.png'] },
}

const softwareApplicationSchema = {
  '@context': 'https://schema.org',
  '@type': 'SoftwareApplication',
  name: 'Octus',
  applicationCategory: 'BusinessApplication',
  operatingSystem: 'Web, Android, iOS',
  description: DESCRIPTION,
  url: SITE_URL,
  provider: {
    '@type': 'Organization',
    name: '3E Systen',
    url: SITE_URL,
    email: '3esysten@gmail.com',
    sameAs: [
      'https://www.instagram.com/3e.systen/',
      'https://www.linkedin.com/company/3e-systen/',
    ],
  },
  offers: [
    { '@type': 'Offer', name: 'Plano Lagoa', price: '129', priceCurrency: 'BRL' },
    { '@type': 'Offer', name: 'Plano Rio', price: '269', priceCurrency: 'BRL' },
    { '@type': 'Offer', name: 'Plano Mar', price: '487', priceCurrency: 'BRL' },
  ],
}

const faqSchema = {
  '@context': 'https://schema.org',
  '@type': 'FAQPage',
  mainEntity: [
    ['Quando começa a cobrança do Octus?', 'Todos os planos têm 15 dias grátis. A primeira mensalidade é cobrada no 16º dia.'],
    ['O Octus substitui a marca da minha loja?', 'Não. Nome, logo, cores e domínio personalizados pelo cliente sempre têm prioridade sobre a identidade padrão Octus.'],
    ['O Octus atende restaurantes?', 'Sim. O módulo de restaurante é opcional e habilitado apenas para os clientes que escolherem utilizá-lo.'],
    ['Como funciona o Programa Clientes Fundadores?', 'Clientes do estado de São Paulo têm 30% de desconto nas quatro primeiras mensalidades. Cada indicação fechada acrescenta 10% no mesmo período, até 100%.'],
  ].map(([name, text]) => ({
    '@type': 'Question', name,
    acceptedAnswer: { '@type': 'Answer', text },
  })),
}

export default function InstitucionalLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      {children}
    </>
  )
}
