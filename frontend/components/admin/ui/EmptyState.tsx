'use client'
import { ReactNode } from 'react'
import { LucideIcon } from 'lucide-react'

interface EmptyStateProps {
  icon: LucideIcon
  message: string
  action?: ReactNode
  /** Menor (py-8, ícone 8) — usado dentro de listas/modais compactos.
   * Padrão (py-12, ícone 8) — usado em telas/seções principais. */
  compact?: boolean
}

/** Bloco "nenhum item encontrado" — mesmo padrão copiado à mão em ~15 lugares
 * do admin (ícone cinza + texto + ação opcional). */
export default function EmptyState({ icon: Icon, message, action, compact }: EmptyStateProps) {
  return (
    <div className={compact ? 'text-center py-8 text-gray-500' : 'flex flex-col items-center justify-center py-12 text-center text-gray-500'}>
      <Icon className="w-8 h-8 text-gray-600 mx-auto mb-2" />
      <p className="text-sm">{message}</p>
      {action}
    </div>
  )
}
