'use client'
import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { useState } from 'react'
import { clearAuth, getUserName } from '@/lib/auth'
import { authApi } from '@/lib/api'
import {
  LayoutDashboard, Package, Trophy, Search, QrCode,
  LogOut, Gamepad2, User, ShoppingBag, Users, Megaphone, Loader2, Wallet
} from 'lucide-react'
import clsx from 'clsx'

const sections = [
  {
    label: 'Operacional',
    items: [
      { href: '/admin/dashboard',    label: 'Painel Geral',   icon: LayoutDashboard, badge: 'LIVE' },
      { href: '/admin/venda-avulsa', label: 'Frente de Caixa', icon: ShoppingBag },
      { href: '/admin/qrcodes',      label: 'Gatilhos QR Code', icon: QrCode },
    ],
  },
  {
    label: 'Gestão & Loja',
    items: [
      { href: '/admin/usuarios',    label: 'Clientes',      icon: Users },
      { href: '/admin/estoque',     label: 'Estoque',       icon: Package },
      { href: '/admin/anuncios',    label: 'Anúncios',      icon: Megaphone },
      { href: '/admin/cartas',      label: 'Cartas TCG',    icon: Search },
      { href: '/admin/campeonatos', label: 'Campeonatos',   icon: Trophy },
    ],
  },
]

export default function Sidebar() {
  const pathname      = usePathname()
  const router        = useRouter()
  const [loggingOut, setLoggingOut] = useState(false)

  async function handleLogout() {
    if (loggingOut) return
    setLoggingOut(true)
    try { await authApi.logout() } catch {}
    clearAuth()
    router.push('/login')
  }

  return (
    <aside className="w-[260px] min-h-screen bg-surface-900 border-r border-surface-500 flex flex-col shrink-0">

      {/* Logo */}
      <div className="px-6 py-7">
        <div className="flex items-center gap-3 font-bold text-2xl">
          <Gamepad2 className="w-7 h-7 text-brand-500" />
          <span className="text-white">softNerd</span>
        </div>
      </div>

      {/* Navegação */}
      <nav className="flex-1 flex flex-col gap-1 px-3 pb-6 overflow-y-auto">
        {sections.map(({ label, items }) => (
          <div key={label} className="mb-2">
            <p className="text-[10px] uppercase text-gray-500 font-bold mt-3 mb-1 px-4 tracking-wider">
              {label}
            </p>
            {items.map(({ href, label: itemLabel, icon: Icon, badge }) => {
              const active = pathname.startsWith(href)
              return (
                <Link
                  key={href}
                  href={href}
                  className={clsx(
                    'flex items-center gap-4 w-full px-4 py-3 rounded-xl font-medium text-sm transition-all duration-150 group',
                    active
                      ? 'bg-[#1E1E2D] text-brand-500'
                      : 'text-gray-500 hover:bg-surface-700 hover:text-white'
                  )}
                >
                  <Icon className={clsx('w-5 h-5 shrink-0', active ? 'text-brand-500' : 'text-gray-500 group-hover:text-gray-300')} />
                  <span className={clsx('flex-1', active && 'text-white font-semibold')}>{itemLabel}</span>
                  {badge && (
                    <span className="text-[10px] bg-accent-green/20 text-accent-green border border-accent-green/30 px-1.5 py-0.5 rounded-full font-bold animate-pulse-slow">
                      {badge}
                    </span>
                  )}
                </Link>
              )
            })}
          </div>
        ))}
      </nav>

      {/* Usuário + logout */}
      <div className="px-3 py-4 border-t border-surface-500">
        <div className="flex items-center gap-3 bg-surface-700 p-3 rounded-xl border border-surface-500 mb-2">
          <div className="w-10 h-10 rounded-full bg-brand-500/20 border border-brand-500/30 flex items-center justify-center shrink-0">
            <User className="w-5 h-5 text-brand-400" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold text-white truncate">{getUserName() || 'Admin'}</p>
            <span className="badge-admin text-[10px]">Admin</span>
          </div>
        </div>
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
    </aside>
  )
}
