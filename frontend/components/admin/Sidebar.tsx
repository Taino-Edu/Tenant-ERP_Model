'use client'
import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { clearAuth, getUserName } from '@/lib/auth'
import { authApi } from '@/lib/api'
import {
  LayoutDashboard, Package, Trophy, Search, QrCode,
  LogOut, Sword, ChevronRight, User, ShoppingBag
} from 'lucide-react'
import clsx from 'clsx'

const navItems = [
  { href: '/admin/dashboard',    label: 'Dashboard',    icon: LayoutDashboard, badge: 'LIVE' },
  { href: '/admin/venda-avulsa', label: 'Venda Avulsa', icon: ShoppingBag },
  { href: '/admin/estoque',      label: 'Estoque',      icon: Package },
  { href: '/admin/cartas',       label: 'Cartas TCG',   icon: Search },
  { href: '/admin/campeonatos',  label: 'Campeonatos',  icon: Trophy },
  { href: '/admin/qrcodes',      label: 'QR Codes',     icon: QrCode },
]

export default function Sidebar() {
  const pathname = usePathname()
  const router   = useRouter()

  async function handleLogout() {
    try { await authApi.logout() } catch {}
    clearAuth()
    router.push('/login')
  }

  return (
    <aside className="w-64 min-h-screen bg-surface-800 border-r border-surface-500 flex flex-col">
      {/* Logo */}
      <div className="px-5 py-6 border-b border-surface-500">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 bg-brand-600/30 border border-brand-500/30 rounded-xl flex items-center justify-center">
            <Sword className="w-5 h-5 text-brand-400" />
          </div>
          <div>
            <p className="font-bold text-white text-sm leading-none">CardGameStore</p>
            <p className="text-xs text-brand-400 mt-0.5">Painel Admin</p>
          </div>
        </div>
      </div>

      {/* Navegação */}
      <nav className="flex-1 px-3 py-4 space-y-1">
        {navItems.map(({ href, label, icon: Icon, badge }) => {
          const active = pathname.startsWith(href)
          return (
            <Link
              key={href} href={href}
              className={clsx(
                'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-150 group',
                active
                  ? 'bg-brand-600/20 text-brand-300 border border-brand-500/30'
                  : 'text-gray-400 hover:text-gray-200 hover:bg-surface-600'
              )}
            >
              <Icon className={clsx('w-4.5 h-4.5', active ? 'text-brand-400' : 'text-gray-500 group-hover:text-gray-300')} />
              <span className="flex-1">{label}</span>
              {badge && (
                <span className="text-[10px] bg-accent-green/20 text-accent-green border border-accent-green/30 px-1.5 py-0.5 rounded-full font-bold animate-pulse-slow">
                  {badge}
                </span>
              )}
              {active && <ChevronRight className="w-3.5 h-3.5 text-brand-400" />}
            </Link>
          )
        })}
      </nav>

      {/* Usuário + logout */}
      <div className="px-4 py-4 border-t border-surface-500">
        <div className="flex items-center gap-3 mb-3 px-2">
          <div className="w-8 h-8 bg-brand-600/30 rounded-full flex items-center justify-center">
            <User className="w-4 h-4 text-brand-400" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium text-white truncate">{getUserName() || 'Admin'}</p>
            <span className="badge-admin text-[10px]">Admin</span>
          </div>
        </div>
        <button onClick={handleLogout} className="btn-secondary w-full justify-center text-sm py-2">
          <LogOut className="w-4 h-4" /> Sair
        </button>
      </div>
    </aside>
  )
}
