import type { Metadata } from 'next'
import { CNPJ_DIGITOS, CONTACTS, SOCIAL_PROFILES } from '@/lib/contatos'

const SITE_URL = 'https://3esysten.com.br'

/** Endereços dos perfis sociais, na mesma lista que o rodapé renderiza. É o que
 *  o Google usa para ligar o site à marca nas redes. */
const SAME_AS = SOCIAL_PROFILES.map(p => p.url)
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

// A empresa, declarada uma vez e referenciada por `@id` pelos outros blocos.
// Antes o único Organization era um objeto solto dentro de `provider`, sem
// identidade própria: o Google via o dado, mas não tinha como saber que aquele
// fornecedor é a mesma entidade dona do site. É esse nó que sustenta resultado
// de marca e painel de conhecimento, e é aqui que os perfis sociais entram.
const organizationSchema = {
  '@context': 'https://schema.org',
  '@type': 'Organization',
  '@id': `${SITE_URL}/#organization`,
  name: '3E Systen',
  alternateName: 'Octus',
  url: SITE_URL,
  logo: `${SITE_URL}/logo-octus.png`,
  // `taxID` só com dígitos, que é o formato esperado. É o campo que permite ao
  // Google amarrar este site a uma pessoa jurídica real em vez de a um nome —
  // e é o que separa "existe um site chamado 3E Systen" de "existe a empresa
  // 3E Systen, e este é o site dela".
  taxID: CNPJ_DIGITOS,
  email: CONTACTS.email,
  telephone: CONTACTS.marketingPhone,
  sameAs: SAME_AS,
  areaServed: { '@type': 'Country', name: 'Brasil' },
  address: {
    '@type': 'PostalAddress',
    addressLocality: 'São José do Rio Preto',
    addressRegion: 'SP',
    addressCountry: 'BR',
  },
  contactPoint: [{
    '@type': 'ContactPoint',
    contactType: 'sales',
    telephone: CONTACTS.marketingPhone,
    email: CONTACTS.email,
    areaServed: 'BR',
    availableLanguage: ['pt-BR'],
  }],
}

const websiteSchema = {
  '@context': 'https://schema.org',
  '@type': 'WebSite',
  '@id': `${SITE_URL}/#website`,
  url: SITE_URL,
  name: '3E Systen',
  inLanguage: 'pt-BR',
  publisher: { '@id': `${SITE_URL}/#organization` },
}

const softwareApplicationSchema = {
  '@context': 'https://schema.org',
  '@type': 'SoftwareApplication',
  name: 'Octus',
  applicationCategory: 'BusinessApplication',
  operatingSystem: 'Web, Android, iOS',
  description: DESCRIPTION,
  url: SITE_URL,
  provider: { '@id': `${SITE_URL}/#organization` },
  // `price` sozinho é lido como preço à vista: o resultado de busca mostraria
  // "R$ 129" para um plano que custa R$ 129 POR MÊS. O `priceSpecification`
  // com `unitCode: 'MON'` (mês, código UN/CEFACT) é o que diz a recorrência.
  // A taxa de implantação não entra: ela é negociada por contrato e não tem
  // valor único para anunciar — está respondida no FAQPage abaixo.
  offers: [
    ['Plano Lagoa', '129'],
    ['Plano Rio', '269'],
    ['Plano Mar', '487'],
  ].map(([name, price]) => ({
    '@type': 'Offer', name, price, priceCurrency: 'BRL',
    priceSpecification: {
      '@type': 'UnitPriceSpecification',
      price, priceCurrency: 'BRL', unitCode: 'MON', billingIncrement: 1,
    },
  })),
}

const faqSchema = {
  '@context': 'https://schema.org',
  '@type': 'FAQPage',
  mainEntity: [
    ['Quando começa a cobrança do Octus?', 'Todos os planos têm 15 dias grátis. A primeira mensalidade é cobrada no 16º dia.'],
    ['O Octus substitui a marca da minha loja?', 'Não. Nome, logo, cores e domínio personalizados pelo cliente sempre têm prioridade sobre a identidade padrão Octus.'],
    ['O Octus atende restaurantes?', 'Sim. O módulo de restaurante é opcional e habilitado apenas para os clientes que escolherem utilizá-lo.'],
    // Entra aqui porque é a primeira pergunta de quem lê "+ taxa de implantação"
    // na tabela e não vê valor. Sem valor no schema de propósito: a taxa é
    // negociada por contrato, e número no resultado de busca vira promessa.
    ['O Octus tem taxa de implantação?', 'Sim. Todos os planos têm taxa de implantação, cobrada uma única vez. O valor é definido na contratação, conforme o porte da operação, e confirmado pelo Marketing.'],
    ['Como funciona o Programa Clientes Fundadores?', 'Clientes do estado de São Paulo têm 30% de desconto nas quatro primeiras mensalidades. Cada indicação fechada acrescenta 10% no mesmo período, até 100%.'],
  ].map(([name, text]) => ({
    '@type': 'Question', name,
    acceptedAnswer: { '@type': 'Answer', text },
  })),
}

export default function InstitucionalLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(organizationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(websiteSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      {children}
    </>
  )
}
