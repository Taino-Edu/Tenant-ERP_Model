'use client'
import { ReactNode } from 'react'
import clsx from 'clsx'

export type BadgeTone = 'brand' | 'success' | 'warning' | 'danger' | 'neutral'

const TONE_CLASS: Record<BadgeTone, string> = {
  brand:   'bg-brand-500/20 text-brand-400 border-brand-500/30',
  success: 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30',
  warning: 'bg-amber-500/20 text-amber-400 border-amber-500/30',
  danger:  'bg-red-500/20 text-red-400 border-red-500/30',
  neutral: 'bg-surface-600/40 text-gray-300 border-surface-400',
}

interface BadgeProps {
  tone?: BadgeTone
  children: ReactNode
  className?: string
}

/** Pill de status genérica por tom (mesmo `StatTone` do StatCard.tsx). A
 * classe `.badge-*` do globals.css é hardcoded por domínio (status de
 * comanda, admin) — isso aqui é a versão genérica pra telas novas não
 * reinventarem cor cada vez que precisam de uma pill de status. */
export default function Badge({ tone = 'neutral', children, className }: BadgeProps) {
  return (
    <span className={clsx('inline-flex items-center px-2.5 py-0.5 rounded text-xs font-bold border', TONE_CLASS[tone], className)}>
      {children}
    </span>
  )
}
