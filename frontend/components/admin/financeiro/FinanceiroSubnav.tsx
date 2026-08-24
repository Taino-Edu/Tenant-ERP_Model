'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { BarChart3, BookOpen, CalendarRange, Gauge, Lightbulb, PackageSearch, RefreshCcw, Tags } from 'lucide-react'

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

  return (
    <nav aria-label="Áreas do Financeiro" className="chip-row print:hidden">
      {ITEMS.map(({ href, label, icon: Icon, exact }) => {
        const active = exact ? pathname === href : pathname.startsWith(href)
        return (
          <Link
            key={href}
            href={href}
            aria-current={active ? 'page' : undefined}
            className={`inline-flex min-h-10 items-center gap-2 rounded-md border px-3 text-sm font-medium transition-colors ${
              active
                ? 'border-brand-500 bg-brand-600/20 text-brand-300'
                : 'border-surface-500 bg-surface-700 text-gray-400 hover:border-surface-400 hover:text-white'
            }`}
          >
            <Icon className="h-4 w-4" />
            {label}
          </Link>
        )
      })}
    </nav>
  )
}
