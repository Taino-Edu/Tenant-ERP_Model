import OperadorDaPlataforma from '@/components/OperadorDaPlataforma'

// Layout fino só para injetar a declaração de operador no HTML DO SERVIDOR.
// A página é 'use client' e o nome da loja só chega depois da hidratação — ver
// o comentário em components/OperadorDaPlataforma.tsx para por que isso
// importa aqui e não em qualquer outra tela.
export default function PrimeiroAcessoLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      {children}
      <OperadorDaPlataforma />
    </>
  )
}
