import type { Metadata } from 'next'

// A tela do QR Code da mesa é ponto de entrada de sessão para quem já está
// sentado no restaurante. Não tem nada a oferecer para quem chega pela busca,
// e uma mesa indexada é um link para abrir comanda numa mesa que a pessoa não
// está ocupando.
export const metadata: Metadata = {
  robots: { index: false, follow: false },
}

export default function MesaLayout({ children }: { children: React.ReactNode }) {
  return children
}
