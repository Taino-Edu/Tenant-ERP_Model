'use client'
import { useEffect, useState } from 'react'
import { useRouter, usePathname } from 'next/navigation'
import Link from 'next/link'
import { isPlatformOwner, clearAuth } from '@/lib/auth'
import { Toaster } from 'react-hot-toast'
import { LogOut, ShieldCheck, LayoutDashboard, Building2, UserPlus, LifeBuoy, History } from 'lucide-react'
import clsx from 'clsx'
import ThemeToggle from '@/components/ThemeToggle'

const NAV_ITEMS = [
  { href: '/plataforma',         label: 'Visão Geral', icon: LayoutDashboard },
  { href: '/plataforma/tenants', label: 'Tenants',      icon: Building2 },
  { href: '/plataforma/leads',   label: 'Leads',        icon: UserPlus },
  { href: '/plataforma/suporte', label: 'Suporte',      icon: LifeBuoy },
  { href: '/plataforma/logs',    label: 'Logs',         icon: History },
]

export default function PlataformaShell({ children }: { children: React.ReactNode }) {
  const router = useRouter()
  const pathname = usePathname()
  const [checked, setChecked] = useState(false)

  useEffect(() => {
    if (!isPlatformOwner()) {
      router.push('/login')
      return
    }
    setChecked(true)
  }, [router])

  function handleLogout() {
    clearAuth()
    router.push('/login')
  }

  if (!checked) return null

  return (
    <div className="admin-shell min-h-screen bg-surface-900">
      <Toaster
        position="top-right"
        toastOptions={{
          style: { background: '#1A1A1F', color: '#fff', border: '1px solid #2D2D36', fontSize: '14px', borderRadius: '12px' },
          success: { iconTheme: { primary: '#00F0A8', secondary: '#000' } },
          error:   { iconTheme: { primary: '#FF3B30', secondary: '#fff' } },
        }}
      />
      <header className="border-b border-surface-600 bg-surface-800">
        <div className="max-w-5xl mx-auto px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-2 text-white font-bold">
            <ShieldCheck className="w-5 h-5 text-brand-400" />
            Painel Gerenciador Octus
          </div>
          <div className="flex items-center gap-2">
            <ThemeToggle compact />
            <button onClick={handleLogout} className="btn-secondary text-sm py-1.5">
              <LogOut className="w-4 h-4" /> Sair
            </button>
          </div>
        </div>
        <nav className="max-w-5xl mx-auto px-6 flex items-center gap-1 overflow-x-auto">
          {NAV_ITEMS.map(({ href, label, icon: Icon }) => {
            const active = href === '/plataforma' ? pathname === href : pathname.startsWith(href)
            return (
              <Link
                key={href}
                href={href}
                className={clsx(
                  'flex items-center gap-1.5 px-3 py-2.5 text-sm font-medium border-b-2 whitespace-nowrap transition-colors',
                  active
                    ? 'border-brand-400 text-white'
                    : 'border-transparent text-gray-400 hover:text-white hover:border-surface-400',
                )}
              >
                <Icon className="w-4 h-4" /> {label}
              </Link>
            )
          })}
        </nav>
      </header>
      <main className="max-w-5xl mx-auto px-6 py-8">
        {children}
      </main>
    </div>
  )
}
