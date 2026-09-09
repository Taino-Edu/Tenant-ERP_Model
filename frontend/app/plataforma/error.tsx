'use client'

import AreaError from '@/components/AreaError'

export default function PlataformaError({
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
      area="plataforma"
      description="A página foi interrompida, mas o painel gerenciador continua disponível. Tente novamente ou volte para a visão geral."
      homeHref="/plataforma"
      homeLabel="Visão geral"
    />
  )
}
