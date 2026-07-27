'use client'

// =============================================================================
// error.tsx (/admin) — Boundary do painel. Renderiza como children do <main>
// em app/admin/layout.tsx, então a Sidebar continua na tela: o lojista perde a
// página que quebrou, não o sistema inteiro — dá pra navegar pra outra tela sem
// recarregar nada.
//
// Usa text-white/text-gray-* de propósito (mesmo padrão do PageHeader): dentro
// de .admin-shell essas classes têm override de tema claro no globals.css.
// =============================================================================

import { useEffect } from 'react'
import Link from 'next/link'
import { AlertTriangle, RotateCw, LayoutDashboard } from 'lucide-react'
import Button from '@/components/admin/ui/Button'

export default function AdminError({
  error,
  reset,
}: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  useEffect(() => {
    console.error('[admin error boundary]', error)
  }, [error])

  return (
    <div className="flex flex-col items-center justify-center px-6 py-20 text-center">
      <div className="w-16 h-16 rounded-2xl bg-surface-700 border border-surface-500 flex items-center justify-center mb-6">
        <AlertTriangle className="w-8 h-8 text-gray-400" />
      </div>

      <h1 className="text-xl font-bold text-white mb-2">Esta tela não carregou</h1>

      <p className="text-gray-400 text-sm max-w-md mb-6">
        Um erro inesperado interrompeu o carregamento. As outras telas do painel
        continuam funcionando — use o menu ao lado para seguir trabalhando.
      </p>

      <div className="flex flex-wrap items-center justify-center gap-2">
        <Button onClick={reset}>
          <RotateCw className="w-4 h-4" />
          Tentar novamente
        </Button>
        <Link href="/admin/dashboard" className="btn-secondary">
          <LayoutDashboard className="w-4 h-4" />
          Ir para o painel
        </Link>
      </div>

      {error.digest && (
        <p className="text-xs text-gray-600 mt-6">
          Informe este código ao suporte: <code>{error.digest}</code>
        </p>
      )}
    </div>
  )
}
