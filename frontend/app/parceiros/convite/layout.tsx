import type { Metadata } from 'next'

// A tela de aceite é aberta por um convite nominal com token na URL. Ela herda
// o layout de /parceiros, que é uma landing indexável — sem este arquivo, o
// buscador guardaria a página de assinatura com o título e a descrição do
// programa de afiliados, e o visitante chegaria nela sem token nenhum.
export const metadata: Metadata = {
  title: { absolute: 'Aceite de parceria | 3E Systen' },
  description: 'Confirmação do aceite do regulamento do Programa de Parcerias 3E Systen.',
  robots: { index: false, follow: false },
  alternates: { canonical: null },
}

export default function ConviteLayout({ children }: { children: React.ReactNode }) {
  return children
}
