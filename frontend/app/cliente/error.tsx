'use client'

import AreaError from '@/components/AreaError'

export default function ClienteError({
  error,
  reset,
}: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  return (
    <AreaError
      error={error}
      reset={reset}
      area="cliente"
      description="Não conseguimos carregar seus dados agora. Tente novamente; sua comanda e seus pontos continuam salvos."
      homeHref="/cliente"
      homeLabel="Minha comanda"
      variant="cliente"
    />
  )
}
