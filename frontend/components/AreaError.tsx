'use client'

import { useEffect } from 'react'
import Link from 'next/link'
import { AlertTriangle, Home, RotateCw } from 'lucide-react'

type AreaErrorProps = {
  error: Error & { digest?: string }
  reset: () => void
  area: string
  description: string
  homeHref: string
  homeLabel: string
  variant?: 'portal' | 'cliente'
}

export default function AreaError({
  error,
  reset,
  area,
  description,
  homeHref,
  homeLabel,
  variant = 'portal',
}: AreaErrorProps) {
  useEffect(() => {
    console.error(`[${area} error boundary]`, error)
  }, [area, error])

  const cliente = variant === 'cliente'

  return (
    <div className={`flex flex-col items-center justify-center px-6 py-20 text-center ${cliente ? 'min-h-screen bg-[#EBF7FD]' : ''}`}>
      <div className={`mb-6 flex h-16 w-16 items-center justify-center rounded-2xl border ${cliente ? 'border-[#28b0d6]/20 bg-white' : 'border-surface-500 bg-surface-700'}`}>
        <AlertTriangle className={`h-8 w-8 ${cliente ? 'text-[#16728c]' : 'text-gray-400'}`} aria-hidden="true" />
      </div>

      <h1 className={`mb-2 text-xl font-bold ${cliente ? 'text-[#0C3D5A]' : 'text-white'}`}>
        Esta tela não carregou
      </h1>
      <p className={`mb-6 max-w-md text-sm ${cliente ? 'text-[#356f89]' : 'text-gray-400'}`}>
        {description}
      </p>

      <div className="flex flex-wrap items-center justify-center gap-2">
        <button type="button" onClick={reset} className="btn-primary">
          <RotateCw className="h-4 w-4" aria-hidden="true" />
          Tentar novamente
        </button>
        <Link href={homeHref} className="btn-secondary">
          <Home className="h-4 w-4" aria-hidden="true" />
          {homeLabel}
        </Link>
      </div>

      {error.digest && (
        <p className={`mt-6 text-xs ${cliente ? 'text-[#527f94]' : 'text-gray-600'}`}>
          Informe este código ao suporte: <code>{error.digest}</code>
        </p>
      )}
    </div>
  )
}
