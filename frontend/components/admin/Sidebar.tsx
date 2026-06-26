'use client'
import React from 'react'
import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { useState } from 'react'
import { clearAuth, getUserName, getRole, hasPermission } from '@/lib/auth'
import { authApi } from '@/lib/api'
import {
  LayoutDashboard, Package, Trophy, Search, QrCode,
  LogOut, User, ShoppingBag, Users, Megaphone,
  Loader2, X, Menu, CreditCard, Store, Shield, TrendingUp, Tag, BarChart2, Info, UserCog, Settings,
} from 'lucide-react'
import clsx from 'clsx'
import ThemeToggle from '@/components/ThemeToggle'
import { SIDEBAR_SHORTCUT_KEYS } from '@/components/admin/KeyboardShortcutsOverlay'

const sections = [
  {
    label: 'Operacional',
    items: [
      { href: '/admin/dashboard',    label: 'Painel Geral',    icon: LayoutDashboard, badge: 'LIVE', perm: 'dashboard' },
      { href: '/admin/venda-avulsa', label: 'Frente de Caixa', icon: ShoppingBag,                    perm: 'pdv' },
      { href: '/admin/qrcodes',      label: 'Gatilhos QR Code', icon: QrCode,                         perm: 'qrcodes' },
    ],
  },
  {
    label: 'Gestão & Loja',
    items: [
      { href: '/admin/usuarios',    label: 'Clientes',     icon: Users,       perm: 'usuarios' },
      { href: '/admin/crediario',   label: 'Crediário',    icon: CreditCard,  perm: 'crediario' },
      { href: '/admin/estoque',     label: 'Estoque',      icon: Package,     perm: 'estoque' },
      { href: '/admin/financeiro',  label: 'Financeiro',   icon: TrendingUp,  perm: 'financeiro' },
      { href: '/admin/relatorios',  label: 'Relatórios',   icon: BarChart2,   perm: 'relatorios' },
      { href: '/admin/categorias',  label: 'Categorias',   icon: Tag,         perm: 'categorias' },
      { href: '/admin/anuncios',    label: 'Anúncios',     icon: Megaphone,   perm: 'anuncios' },
      { href: '/admin/cartas',      label: 'Cartas TCG',   icon: Search,      perm: 'cartas' },
      { href: '/admin/campeonatos', label: 'Campeonatos',  icon: Trophy,      perm: 'campeonatos' },
    ],
  },
  {
    label: 'Administração',
    adminOnly: true,
    items: [
      { href: '/admin/perfis', label: 'Perfis de Acesso', icon: UserCog, perm: null },
    ],
  },
  {
    label: 'Compliance',
    items: [
      { href: '/admin/lgpd',  label: 'LGPD & Auditoria', icon: Shield, perm: 'lgpd' },
      { href: '/admin/sobre', label: 'Sobre o Sistema',   icon: Info,   perm: null },
    ],
  },
  {
    label: 'Pessoal',
    items: [
      { href: '/admin/configuracoes', label: 'Configurações', icon: Settings, perm: null },
    ],
  },
]

function NavItems({ pathname, onClose }: { pathname: string; onClose?: () => void }) {
  const role = getRole()
  const isAdmin = role === 'Admin'

  return (
    <nav className="flex-1 flex flex-col gap-1 px-3 pb-6 overflow-y-auto">
      {sections.map(({ label, items, adminOnly }) => {
        if (adminOnly && !isAdmin) return null
        const visibleItems = items.filter(({ perm }) => perm === null || hasPermission(perm))
        if (visibleItems.length === 0) return null
        return (
          <div key={label} className="mb-2">
            <p className="text-[10px] uppercase text-gray-500 font-bold mt-3 mb-1 px-4 tracking-wider">
              {label}
            </p>
            {visibleItems.map(({ href, label: itemLabel, icon: Icon, badge }: { href: string; label: string; icon: React.ElementType; perm: string | null; badge?: string }) => {
              const active  = pathname.startsWith(href)
              const shortcut = SIDEBAR_SHORTCUT_KEYS[href]
              return (
                <Link
                  key={href}
                  href={href}
                  onClick={onClose}
                  className={clsx(
                    'flex items-center gap-4 w-full px-4 py-3 rounded-xl font-medium text-sm transition-all duration-150 group nav-item',
                    active ? 'nav-item-active' : 'text-gray-500 hover:bg-surface-700 hover:text-white'
                  )}
                >
                  <Icon className={clsx('w-5 h-5 shrink-0', active ? 'text-brand-500' : 'text-gray-500 group-hover:text-gray-300')} />
                  <span className={clsx('flex-1 nav-item-label', active && 'font-semibold')}>{itemLabel}</span>
                  {badge && (
                    <span className="text-[10px] bg-accent-green/20 text-accent-green border border-accent-green/30 px-1.5 py-0.5 rounded-full font-bold animate-pulse-slow">
                      {badge}
                    </span>
                  )}
                  {shortcut && !active && (
                    <kbd className="hidden md:inline-block text-[9px] text-gray-600 bg-surface-800 border border-surface-600 rounded px-1.5 py-0.5 font-mono font-bold leading-none opacity-0 group-hover:opacity-100 transition-opacity">
                      {shortcut}
                    </kbd>
                  )}
                </Link>
              )
            })}
          </div>
        )
      })}
    </nav>
  )
}

export default function Sidebar() {
  const pathname      = usePathname()
  const router        = useRouter()
  const [loggingOut, setLoggingOut] = useState(false)
  const [mobileOpen,  setMobileOpen]  = useState(false)

  async function handleLogout() {
    if (loggingOut) return
    setLoggingOut(true)
    try { await authApi.logout() } catch {}
    clearAuth()
    router.push('/login')
  }

  const role = getRole()
  const roleLabel = role === 'Admin' ? 'Admin' : role === 'Operator' ? 'Operador' : role

  const footer = (
    <div className="px-3 py-4 border-t border-surface-500">
      <div className="flex items-center gap-3 bg-surface-700 p-3 rounded-xl border border-surface-500 mb-2">
        <div className="w-10 h-10 rounded-full bg-brand-500/20 border border-brand-500/30 flex items-center justify-center shrink-0">
          <User className="w-5 h-5 text-brand-400" />
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-sm font-semibold text-white truncate">{getUserName() || 'Admin'}</p>
          <span className="badge-admin text-[10px]">{roleLabel}</span>
        </div>
      </div>
      {/* Link para a página pública da loja */}
      <a
        href="/"
        target="_blank"
        rel="noopener noreferrer"
        className="flex items-center gap-2 w-full px-3 py-2.5 rounded-xl text-sm font-medium text-gray-500 hover:bg-surface-700 hover:text-white transition-all duration-150 mb-1 group"
      >
        <Store className="w-4 h-4 text-gray-500 group-hover:text-brand-400 shrink-0" />
        <span>Ver Loja</span>
        <svg className="w-3 h-3 ml-auto opacity-40" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
        </svg>
      </a>
      {/* Toggle de tema */}
      <ThemeToggle />
      <button
        onClick={handleLogout}
        disabled={loggingOut}
        className="btn-secondary w-full justify-center text-sm py-2.5"
      >
        {loggingOut
          ? <><Loader2 className="w-4 h-4 animate-spin" /> Saindo...</>
          : <><LogOut className="w-4 h-4" /> Sair</>}
      </button>
    </div>
  )

  return (
    <>
      {/* Mobile top bar */}
      <div className="md:hidden fixed top-0 left-0 right-0 z-30 flex items-center justify-between bg-surface-800 border-b border-surface-500 px-4 py-3">
        <div className="flex items-center gap-2">
          <img src="/logo-maikon.png" alt="Santuário Nerd" className="h-9" />
          <span className="text-xs text-brand-400 font-bold">Admin</span>
        </div>
        <button onClick={() => setMobileOpen(true)} className="text-gray-400 hover:text-white p-1">
          <Menu className="w-6 h-6" />
        </button>
      </div>

      {/* Mobile overlay */}
      {mobileOpen && (
        <div
          className="md:hidden fixed inset-0 z-40 bg-black/70"
          onClick={() => setMobileOpen(false)}
        />
      )}

      {/* Mobile drawer */}
      <aside className={clsx(
        'md:hidden fixed inset-y-0 left-0 z-50 w-[260px] bg-surface-900 border-r border-surface-500 flex flex-col transition-transform duration-300',
        mobileOpen ? 'translate-x-0' : '-translate-x-full'
      )}>
        <div className="flex items-center justify-between px-6 py-6 shrink-0">
          <div className="flex items-center gap-3">
            <img src="/logo-maikon.png" alt="Santuário Nerd" className="h-10 w-10 object-contain shrink-0" />
            <div>
              <p className="text-white text-base leading-tight">Santuário Nerd</p>
              <p className="text-[10px] text-brand-400 font-semibold tracking-wider uppercase">Admin</p>
            </div>
          </div>
          <button onClick={() => setMobileOpen(false)} className="text-gray-500 hover:text-white">
            <X className="w-5 h-5" />
          </button>
        </div>
        <NavItems pathname={pathname} onClose={() => setMobileOpen(false)} />
        {footer}
      </aside>

      {/* Desktop sidebar */}
      <aside className="hidden md:flex w-[260px] min-h-screen bg-surface-900 border-r border-surface-500 flex-col shrink-0">
        <div className="px-6 py-7 shrink-0 flex items-center gap-3">
          <img src="/logo-maikon.png" alt="Santuário Nerd" className="h-10 w-10 object-contain shrink-0" />
          <div>
            <p className="text-white text-base leading-tight">Santuário Nerd</p>
            <p className="text-[10px] text-brand-400 font-semibold tracking-wider uppercase">Admin</p>
          </div>
        </div>
        <NavItems pathname={pathname} />
        {footer}
      </aside>
    </>
  )
}
