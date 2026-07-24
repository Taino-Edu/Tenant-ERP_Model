import type { Metadata } from 'next'
import PlataformaShell from './PlataformaShell'

// Título fixo do painel do dono da plataforma — nunca deve herdar o nome de
// nenhuma loja/tenant (o default do app/layout.tsx é "Minha Loja", que é
// especifico de tenant).
export const metadata: Metadata = {
  title: { absolute: 'Painel Gerenciador Octus' },
}

export default function PlataformaLayout({ children }: { children: React.ReactNode }) {
  return <PlataformaShell>{children}</PlataformaShell>
}
