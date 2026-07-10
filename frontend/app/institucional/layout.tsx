import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: '2Esysten — ERP completo para lojas e varejo',
  description:
    'Plataforma de gestão white-label para lojistas: PDV, estoque, fiscal, crediário e app próprio — tudo em um só sistema.',
}

// Layout mínimo — existe só pra carregar os metadados acima, já que a página
// em si é um client component (precisa de estado pro tema claro/escuro).
export default function InstitucionalLayout({ children }: { children: React.ReactNode }) {
  return children
}
