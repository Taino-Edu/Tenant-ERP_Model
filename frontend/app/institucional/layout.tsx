import type { Metadata } from 'next'

const TITLE = '3Esysten — ERP completo para lojas e varejo'
const DESCRIPTION =
  'Plataforma de gestão white-label para lojistas: PDV, estoque, fiscal, crediário e app próprio — tudo em um só sistema.'

export const metadata: Metadata = {
  // "absolute" (não string solta) pra não entrar no template do layout raiz
  // (`%s — ${nome do tenant}`) — bug real achado testando localmente: sem
  // isso o título saía "3Esysten — ... — Minha Loja" (o template do pai
  // grudava o nome genérico/do tenant no final do título institucional).
  title: { absolute: TITLE },
  description: DESCRIPTION,
  openGraph: {
    title: TITLE,
    description: DESCRIPTION,
    siteName: '3Esysten',
    locale: 'pt_BR',
    type: 'website',
  },
  twitter: {
    card: 'summary',
    title: TITLE,
    description: DESCRIPTION,
  },
}

// Layout mínimo — existe só pra carregar os metadados acima, já que a página
// em si é um client component (precisa de estado pro tema claro/escuro).
export default function InstitucionalLayout({ children }: { children: React.ReactNode }) {
  return children
}
