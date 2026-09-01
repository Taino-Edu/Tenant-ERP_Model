'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import clsx from 'clsx'
import { ChevronDown } from 'lucide-react'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import { useAdminPermissions } from '@/hooks/useAdminPermissions'
import { currentNavItem, currentNavSection, type NavVisibilityCtx } from '@/lib/adminNav'

/**
 * Segundo nível contextual da navegação.
 *
 * A sidebar responde apenas "em qual área estou?". Esta barra responde
 * "o que posso fazer dentro dela?" e só mostra opções permitidas para o papel
 * atual e módulos realmente ativos no tenant.
 */
export default function AdminAreaSubnav() {
  const pathname = usePathname()
  const { site } = useSiteConfig()
  const { mounted, isAdmin, can } = useAdminPermissions()

  if (!mounted) return null

  const ctx: NavVisibilityCtx = {
    isAdmin,
    enabledModules: site.enabledModules,
    hasPerm: can,
  }
  const section = currentNavSection(pathname, ctx)
  const current = currentNavItem(pathname)

  if (!section || section.items.length < 2) return null

  const exactItem = section.items.find(item => item.href === pathname)
  const ExactItemIcon = exactItem?.icon

  const renderItems = (mobile = false) => section.items.map(({ href, label, icon: Icon, badge }) => {
    // Uma rota interna, como /financeiro/insights, não deve fazer a
    // entrada raiz parecer a página atual. O subnav próprio daquela área
    // continua responsável pelo terceiro nível.
    const active = pathname === href || (current?.href === href && !pathname.startsWith(href + '/'))
    return (
      <Link
        key={href}
        href={href}
        aria-current={active ? 'page' : undefined}
        className={clsx(
          mobile
            ? 'flex min-h-11 w-full items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors'
            : 'flex min-h-10 shrink-0 items-center gap-2 rounded-xl border px-3 py-2 text-xs font-medium transition-colors',
          active
            ? mobile
              ? 'bg-brand-500/15 text-brand-400'
              : 'border-brand-500/40 bg-brand-500/15 text-brand-400'
            : mobile
              ? 'text-gray-300 hover:bg-surface-700 hover:text-white'
              : 'border-surface-500 bg-surface-800 text-gray-400 hover:border-surface-400 hover:text-white',
        )}
      >
        <Icon className="h-4 w-4 shrink-0" />
        <span className="flex-1">{label}</span>
        {badge && (
          <span className="rounded-full border border-accent-green/30 bg-accent-green/15 px-1.5 py-0.5 text-[9px] font-bold text-accent-green">
            {badge}
          </span>
        )}
      </Link>
    )
  })

  return (
    <div className="relative z-30 border-b border-surface-500 bg-surface-900/95 px-4 py-3 backdrop-blur md:px-6">
      <div className="mx-auto flex max-w-[1440px] items-center gap-3">
        <p className="hidden shrink-0 text-[10px] font-bold uppercase tracking-wider text-gray-500 lg:block">
          {section.label}
        </p>

        <details className="group relative w-full sm:hidden">
          <summary className="flex min-h-11 cursor-pointer list-none items-center gap-2 rounded-xl border border-surface-500 bg-surface-800 px-3 text-sm font-semibold text-white [&::-webkit-details-marker]:hidden">
            {ExactItemIcon ? <ExactItemIcon className="h-4 w-4 text-brand-400" /> : null}
            <span className="flex-1">{exactItem?.label ?? section.label}</span>
            <span className="text-xs font-medium text-gray-500">Trocar página</span>
            <ChevronDown className="h-4 w-4 text-gray-500 transition-transform group-open:rotate-180" />
          </summary>
          <div className="absolute inset-x-0 top-full z-50 mt-2 space-y-1 rounded-xl border border-surface-500 bg-surface-800 p-2 shadow-2xl">
            {renderItems(true)}
          </div>
        </details>

        <nav
          aria-label={`Subpáginas de ${section.label}`}
          className="scrollbar-none hidden min-w-0 flex-1 gap-2 overflow-x-auto sm:flex"
        >
          {renderItems()}
        </nav>
      </div>
    </div>
  )
}
