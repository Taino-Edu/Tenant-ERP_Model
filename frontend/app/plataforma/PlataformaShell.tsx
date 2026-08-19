'use client'
import { useEffect, useMemo, useState } from 'react'
import { useRouter, usePathname } from 'next/navigation'
import Link from 'next/link'
import { isPlatformOwner, clearAuth, saveAuth, hasPlatformPermission } from '@/lib/auth'
import { api } from '@/lib/api'
import { PLATFORM_PERMISSIONS_EVENT } from '@/hooks/usePlatformPermissions'
import { Toaster } from 'react-hot-toast'
import { LogOut, ShieldCheck, LayoutDashboard, Building2, UserPlus, LifeBuoy, History, Search, Wallet, Users, HandCoins } from 'lucide-react'
import clsx from 'clsx'
import ThemeToggle from '@/components/ThemeToggle'

const NAV_ITEMS = [
  { href: '/plataforma',            label: 'Visão Geral', icon: LayoutDashboard, permission: 'platform.dashboard' },
  { href: '/plataforma/tenants',    label: 'Tenants',      icon: Building2, permission: 'platform.tenants.read' },
  { href: '/plataforma/financeiro', label: 'Financeiro',   icon: Wallet, permission: 'platform.finance.read' },
  { href: '/plataforma/leads',      label: 'Leads',        icon: UserPlus, permission: 'platform.leads' },
  { href: '/plataforma/prospeccao', label: 'Prospecção',   icon: Search, permission: 'platform.leads' },
  { href: '/plataforma/indicacoes', label: 'Indicações',    icon: HandCoins, permission: 'platform.referrals.read' },
  { href: '/plataforma/suporte',    label: 'Suporte',      icon: LifeBuoy, permission: 'platform.support' },
  { href: '/plataforma/logs',       label: 'Logs',         icon: History, permission: 'platform.logs' },
  { href: '/plataforma/equipe',     label: 'Equipe',       icon: Users, permission: 'platform.team' },
]

// Mesma cadência do painel da loja: renova o token antes que a inatividade
// derrube a sessão.
const REFRESH_INTERVAL_MS = 45 * 60 * 1000

export default function PlataformaShell({ children }: { children: React.ReactNode }) {
  const router = useRouter()
  const pathname = usePathname()
  const [checked, setChecked] = useState(false)
  // As abas saem do cookie de permissões, gravado no login. Sem isso, quem tem
  // o perfil alterado (ou pega uma mudança na definição do perfil) continua
  // vendo o menu antigo até deslogar e logar de novo.
  const [permissionsVersion, setPermissionsVersion] = useState(0)

  useEffect(() => {
    if (!isPlatformOwner()) {
      router.push('/login')
      return
    }
    setChecked(true)
  }, [router])

  useEffect(() => {
    const refresh = async () => {
      try {
        const res = await api.post('/api/auth/refresh', {})
        if (res.data) {
          saveAuth(res.data)
          setPermissionsVersion(current => current + 1)
          // As telas de dentro escondem botões pelas mesmas permissões e não
          // re-renderizam junto com o shell (`children` mantém a identidade do
          // elemento). O evento é o que faz elas relerem o cookie novo.
          window.dispatchEvent(new Event(PLATFORM_PERMISSIONS_EVENT))
        }
      } catch {
        // Falhou: o interceptor de /lib/api manda pro login na próxima chamada.
      }
    }

    refresh()
    const id = setInterval(refresh, REFRESH_INTERVAL_MS)
    return () => clearInterval(id)
  }, [])

  // `checked` e `permissionsVersion` entram nas dependências de propósito: o
  // cookie não notifica ninguém quando muda, então recalcular junto com eles é
  // o que mantém as abas coerentes com o perfil vigente.
  const visibleItems = useMemo(
    () => (checked ? NAV_ITEMS.filter(item => hasPlatformPermission(item.permission)) : []),
    [checked, permissionsVersion],
  )

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
      {/* Cabeçalho fixo: a navegação da plataforma é uma faixa de abas que rola
          de lado, e ela precisa continuar alcançável depois de rolar uma lista
          longa de tenants ou de logs — no celular voltar ao topo pra trocar de
          seção é o gesto mais repetido dessa área. */}
      <header className="sticky top-0 z-30 border-b border-surface-600 bg-surface-800">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-3 sm:py-4 flex items-center justify-between gap-2">
          <div className="flex items-center gap-2 text-white font-bold min-w-0">
            <ShieldCheck className="w-5 h-5 text-brand-400 shrink-0" />
            {/* No celular o nome completo empurraria o botão Sair pra fora. */}
            <span className="truncate">
              <span className="sm:hidden">Gerenciador</span>
              <span className="hidden sm:inline">Painel Gerenciador Octus</span>
            </span>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <ThemeToggle compact />
            <button onClick={handleLogout} aria-label="Sair" className="btn-secondary text-sm py-1.5">
              <LogOut className="w-4 h-4" /> <span className="hidden xs:inline">Sair</span>
            </button>
          </div>
        </div>
        <nav className="chip-row max-w-7xl mx-auto px-4 sm:px-6 items-center !gap-1">
          {visibleItems.map(({ href, label, icon: Icon }) => {
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
      <main className="max-w-7xl mx-auto px-4 sm:px-6 py-5 sm:py-8">
        {children}
      </main>
    </div>
  )
}
