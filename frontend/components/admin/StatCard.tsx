'use client'
import { LucideIcon, TrendingUp, TrendingDown } from 'lucide-react'
import clsx from 'clsx'

export type StatTone = 'brand' | 'success' | 'warning' | 'danger' | 'neutral'

interface StatCardProps {
  icon: LucideIcon
  label: string
  value: string | number
  tone?: StatTone
  trend?: { value: number; label?: string }
  sub?: string
  selected?: boolean
  onClick?: () => void
}

const TONE: Record<StatTone, { text: string; bg: string; selectedBorder: string; selectedBg: string }> = {
  brand:   { text: 'text-brand-400',   bg: 'bg-brand-600/15',   selectedBorder: 'border-brand-500/50',   selectedBg: 'bg-brand-600/5' },
  success: { text: 'text-emerald-400', bg: 'bg-emerald-500/15', selectedBorder: 'border-emerald-500/50', selectedBg: 'bg-emerald-500/5' },
  warning: { text: 'text-amber-400',   bg: 'bg-amber-500/15',   selectedBorder: 'border-amber-500/50',   selectedBg: 'bg-amber-500/5' },
  danger:  { text: 'text-red-400',     bg: 'bg-red-500/15',     selectedBorder: 'border-red-500/50',     selectedBg: 'bg-red-500/5' },
  neutral: { text: 'text-gray-300',    bg: 'bg-surface-600/40', selectedBorder: 'border-surface-400',    selectedBg: 'bg-surface-700/50' },
}

/** Card de KPI padrão do admin — substitui as implementações locais
 * (StatCard de reservas, KpiCard de financeiro, JSX inline de estoque). */
export default function StatCard({ icon: Icon, label, value, tone = 'brand', trend, sub, selected, onClick }: StatCardProps) {
  const c = TONE[tone]

  const className = clsx(
    'card flex flex-col gap-2 text-left w-full transition-all',
    onClick && 'hover:border-surface-400 hover:bg-surface-700/50 cursor-pointer active:scale-[0.98]',
    selected && [c.selectedBorder, c.selectedBg],
  )

  const content = (
    <>
      <div className="flex items-center justify-between gap-2">
        <p className="text-[11px] text-gray-500 font-semibold uppercase tracking-wider truncate">{label}</p>
        <div className={clsx('w-8 h-8 rounded-lg flex items-center justify-center shrink-0', c.bg)}>
          <Icon className={clsx('w-4 h-4', c.text)} />
        </div>
      </div>
      <p className={clsx('text-xl font-black font-mono leading-tight', c.text)}>{value}</p>
      {trend && (
        <div className={clsx('flex items-center gap-1 text-xs font-semibold', trend.value >= 0 ? 'text-emerald-400' : 'text-red-400')}>
          {trend.value >= 0 ? <TrendingUp className="w-3 h-3 shrink-0" /> : <TrendingDown className="w-3 h-3 shrink-0" />}
          <span>{trend.value >= 0 ? '+' : ''}{trend.value.toFixed(1)}%</span>
          {trend.label && <span className="text-gray-500 font-normal">{trend.label}</span>}
        </div>
      )}
      {sub && (
        <p className="text-xs text-gray-500 flex items-center gap-1">
          {sub}
          {onClick && <span className="ml-auto text-[10px] text-gray-400">clique para detalhar</span>}
        </p>
      )}
    </>
  )

  if (onClick) {
    return <button onClick={onClick} className={className}>{content}</button>
  }
  return <div className={className}>{content}</div>
}
