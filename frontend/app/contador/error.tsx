'use client'

import AreaError from '@/components/AreaError'

export default function ContadorError({
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
      area="contador"
      description="A página foi interrompida, mas sua sessão e as outras empresas continuam disponíveis. Tente novamente ou volte para o portal."
      homeHref="/contador"
      homeLabel="Portal do contador"
    />
  )
}
