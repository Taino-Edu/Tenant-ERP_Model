'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { BarChart3, BookOpen, CalendarRange, ChevronDown, Gauge, Lightbulb, PackageSearch, RefreshCcw, Tags } from 'lucide-react'

const ITEMS = [
  { href: '/admin/financeiro', label: 'Visão geral', icon: BarChart3, exact: true },
  { href: '/admin/financeiro/insights', label: 'Insights', icon: Lightbulb },
  { href: '/admin/financeiro/rentabilidade', label: 'Preço e rentabilidade', icon: Tags },
  { href: '/admin/financeiro/ponto-de-equilibrio', label: 'Ponto de equilíbrio', icon: Gauge },
  { href: '/admin/financeiro/projecao-caixa', label: 'Projeção de caixa', icon: CalendarRange },
  { href: '/admin/financeiro/capital-de-giro', label: 'Capital de giro', icon: RefreshCcw },
  { href: '/admin/financeiro/estoque-inteligente', label: 'Estoque inteligente', icon: PackageSearch },
  { href: '/admin/manual#financeiro-rentabilidade', label: 'Manual', icon: BookOpen },
]

export function FinanceiroSubnav() {
  const pathname = usePathname()
  const primaryItems = ITEMS.slice(0, 2)
  const secondaryItems = ITEMS.slice(2)
  const secondaryActive = secondaryItems.some(({ href }) => pathname.startsWith(href.split('#')[0]))

  const renderLink = ({ href, label, icon: Icon, exact }: typeof ITEMS[number], compact = false) => {
    const active = exact ? pathname === href : pathname.startsWith(href.split('#')[0])
    return (
      <Link
        key={href}
        href={href}
        aria-current={active ? 'page' : undefined}
        className={`${compact ? 'flex w-full' : 'inline-flex'} min-h-11 items-center gap-2 rounded-md border px-3 text-sm font-medium transition-colors ${
          active
            ? 'border-brand-500 bg-brand-600/20 text-brand-300'
            : 'border-surface-500 bg-surface-700 text-gray-400 hover:border-surface-400 hover:text-white'
        }`}
      >
        <Icon className="h-4 w-4 shrink-0" />
        {label}
      </Link>
    )
  }

  return (
    <nav aria-label="Áreas do Financeiro" className="flex items-center gap-2 print:hidden">
      {primaryItems.map(item => renderLink(item))}

      <div className="hidden min-w-0 items-center gap-2 2xl:flex">
        {secondaryItems.map(item => renderLink(item))}
      </div>

      <details className="group relative min-w-0 2xl:hidden">
        <summary className={`flex min-h-11 cursor-pointer list-none items-center gap-2 rounded-md border px-3 text-sm font-medium transition-colors [&::-webkit-details-marker]:hidden ${
          secondaryActive
            ? 'border-brand-500 bg-brand-600/20 text-brand-300'
            : 'border-surface-500 bg-surface-700 text-gray-400 hover:border-surface-400 hover:text-white'
        }`}>
          <span className="sm:hidden">Mais</span>
          <span className="hidden sm:inline">Mais análises</span>
          <ChevronDown className="h-4 w-4 shrink-0 transition-transform group-open:rotate-180" />
        </summary>
        <div className="absolute right-0 top-full z-40 mt-2 w-64 max-w-[calc(100vw-2rem)] space-y-1 rounded-xl border border-surface-500 bg-surface-800 p-2 shadow-2xl">
          {secondaryItems.map(item => renderLink(item, true))}
        </div>
      </details>
    </nav>
  )
}
