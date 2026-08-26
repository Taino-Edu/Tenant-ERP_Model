'use client'
import React from 'react'
import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { useState, useEffect, useRef } from 'react'
import { clearAuth, getUserName, getRole } from '@/lib/auth'
import { authApi, notificationsApi, fiscalApi } from '@/lib/api'
import {
  LogOut, User, Loader2, X, Menu, Store,
  ChevronsLeft, ChevronsRight,
} from 'lucide-react'
import clsx from 'clsx'
import ThemeToggle from '@/components/ThemeToggle'
import MobileTabBar from '@/components/admin/MobileTabBar'
import { SIDEBAR_SHORTCUT_KEYS } from '@/lib/adminKeyboardShortcuts'
import { NAV_SECTIONS, isItemVisible, currentNavTitle, type NavItem } from '@/lib/adminNav'
import { useScrollLock } from '@/hooks/useMediaQuery'
import { useAdminPermissions } from '@/hooks/useAdminPermissions'
import { useSiteConfig } from '@/contexts/SiteConfigContext'

// A lista de seções mora em lib/adminNav.ts — a MobileTabBar precisa das
// mesmas regras de permissão/módulo e não pode importar de um componente.

/** Ordem de tabulação do drawer, em ordem de documento. `:not([disabled])`
 * importa: o botão "Sair" fica desabilitado durante o logout e sairia da fila,
 * levando junto a âncora do trap se ele fosse o último. */
const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

function NavItems({ pathname, onClose, unreadCount, fiscalAlerta, enabledModules, collapsed = false }: { pathname: string; onClose?: () => void; unreadCount: number; fiscalAlerta: boolean; enabledModules: string[]; collapsed?: boolean }) {
  // O guard de hidratação (cookie não existe no SSR) mora no hook, junto com a
  // escuta da renovação de sessão — ver hooks/useAdminPermissions.ts.
  const { isAdmin, can } = useAdminPermissions()
  const checkPerm = (perm: string | null) => perm === null || can(perm)

  return (
    <nav className="flex-1 flex flex-col gap-1 px-3 pb-6 overflow-y-auto">
      {NAV_SECTIONS.map(({ label, items, adminOnly }) => {
        if (adminOnly && !isAdmin) return null
        const visibleItems = items.filter(item =>
          isItemVisible(item, { isAdmin, enabledModules, hasPerm: checkPerm }),
        )
        if (visibleItems.length === 0) return null
        return (
          <div key={label} className="mb-2">
            {/* Recolhida: sem cabeçalho de seção (texto pequeno demais pra caber) —
                só uma linha divisória sutil pra ainda separar os grupos visualmente. */}
            {collapsed ? (
              <div className="h-px bg-surface-700 mx-2 mt-3 mb-2" />
            ) : (
              <p className="text-[10px] uppercase text-gray-500 font-bold mt-3 mb-1 px-4 tracking-wider">
                {label}
              </p>
            )}
            {visibleItems.map(({ href, label: itemLabel, icon: Icon, badge }: NavItem) => {
              const active   = pathname.startsWith(href)
              const shortcut = SIDEBAR_SHORTCUT_KEYS[href]
              const hasDot   = (href === '/admin/mensageria' && unreadCount > 0)
                             || (href === '/admin/fiscal' && fiscalAlerta)
              return (
                <Link
                  key={href}
                  href={href}
                  onClick={onClose}
                  // A MobileTabBar já marcava a tela atual; a sidebar só trocava
                  // a classe CSS, então quem navega por leitor de tela percorria
                  // os 27 links sem nenhum deles dizer "você está aqui".
                  aria-current={active ? 'page' : undefined}
                  title={collapsed ? itemLabel : undefined}
                  className={clsx(
                    'flex items-center w-full py-3 rounded-xl font-medium text-sm transition-all duration-150 group',
                    collapsed ? 'justify-center px-0' : 'gap-4 px-4',
                    active ? 'nav-item-active' : 'text-gray-500 hover:bg-surface-700 hover:text-white'
                  )}
                >
                  <div className="relative shrink-0">
                    <Icon className={clsx('w-5 h-5', active ? 'text-brand-500' : 'text-gray-500 group-hover:text-gray-300')} />
                    {hasDot && (
                      <span className="absolute -top-0.5 -right-0.5 w-2 h-2 rounded-full bg-red-500 animate-pulse" />
                    )}
                  </div>
                  {!collapsed && (
                    <>
                      <span className={clsx('flex-1 nav-item-label', active && 'font-semibold')}>{itemLabel}</span>
                      {badge && (
                        <span className="text-[10px] bg-accent-green/20 text-accent-green border border-accent-green/30 px-1.5 py-0.5 rounded-full font-bold animate-pulse-slow">
                          {badge}
                        </span>
                      )}
                      {/* gray-400 e não gray-600: a 9px o atalho é o menor texto
                          da tela, e no gray-600 saía a 2,8:1 no tema claro e
                          2,3:1 no escuro. Não é enfeite — é a única pista de que
                          o atalho existe. */}
                      {shortcut && !active && (
                        <kbd className="hidden md:inline-block text-[9px] text-gray-400 bg-surface-800 border border-surface-600 rounded px-1.5 py-0.5 font-mono font-bold leading-none opacity-0 group-hover:opacity-100 transition-opacity">
                          {shortcut}
                        </kbd>
                      )}
                    </>
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
  const { site }       = useSiteConfig()
  const logoSrc         = site.adminIconUrl || site.logoUrl || '/logo-placeholder.svg'
  const [loggingOut,   setLoggingOut]   = useState(false)
  const [mobileOpen,   setMobileOpen]   = useState(false)
  const [unreadCount,  setUnreadCount]  = useState(0)
  const [fiscalAlerta, setFiscalAlerta] = useState(false)
  // Cookies de metadados só existem no navegador. O primeiro render precisa
  // repetir o SSR; nome/role reais entram depois da hidratação — mesmo guard
  // que o hook já aplica às permissões.
  const { mounted } = useAdminPermissions()
  // Sempre começa expandida (igual no server e no primeiro render do client) —
  // ler localStorage direto no initializer causaria mismatch de hidratação
  // sempre que o valor salvo fosse "recolhida". O valor real só é aplicado
  // depois, via useEffect (client-only) — mesmo padrão de usePersistentPanel.
  const [collapsed,    setCollapsed]    = useState(false)
  const drawerRef     = useRef<HTMLElement | null>(null)
  const menuButtonRef = useRef<HTMLButtonElement | null>(null)

  useEffect(() => {
    try {
      if (localStorage.getItem('admin-sidebar-collapsed') === 'true') setCollapsed(true)
    } catch {}
  }, [])

  // Drawer aberto trava o scroll do fundo — sem isso o gesto de rolar dentro do
  // menu "vaza" para a página atrás dele assim que a lista chega ao fim.
  useScrollLock(mobileOpen)

  // Abrir e fechar o drawer é uma transição de FOCO, não só de CSS — e as duas
  // metades moram neste efeito porque a ordem entre elas importa.
  //
  // Fechado: `inert`. Só `aria-hidden` era o pior dos mundos — o leitor de tela
  // pulava o drawer, mas os links continuavam na ordem de tabulação, e quem
  // navega por teclado caía dentro de um menu invisível (`aria-hidden` sobre
  // conteúdo focável já é violação por si só). Vai como atributo no DOM, e não
  // como prop: o react-dom 18 não conhece `inert` (só o 19 conhece) e descarta
  // um valor booleano, enquanto o @types/react 18 o tipa como boolean e recusa
  // a string que o runtime aceitaria — nenhum valor passa nos dois. Com React 19
  // isto vira `inert={!mobileOpen}` e o efeito guarda só o foco.
  //
  // Aberto: o foco entra pelo botão de fechar. Sem isso o teclado continua na
  // página atrás e o `Tab` percorre o conteúdo de baixo com o menu por cima.
  useEffect(() => {
    const el = drawerRef.current
    if (!el) return
    if (mobileOpen) {
      el.removeAttribute('inert')
      el.querySelector<HTMLElement>(FOCUSABLE)?.focus()
      return
    }
    // A ordem aqui não é estética: `inert` desfoca na hora quem estiver dentro,
    // então a pergunta "o foco estava no menu?" precisa vir ANTES. Depois de
    // aplicá-lo, activeElement já é o body e a resposta seria sempre não.
    const focoEstavaDentro = el.contains(document.activeElement)
    el.setAttribute('inert', '')
    // Só devolve o foco se ele estava lá dentro. Na primeira montagem o drawer
    // já nasce fechado, e sem essa guarda o menu roubaria o foco no load.
    if (focoEstavaDentro) menuButtonRef.current?.focus()
  }, [mobileOpen])

  // Esc fecha; Tab circula dentro do drawer. O trap é manual porque o conteúdo
  // da página não é irmão deste componente — não dá para marcá-lo `inert` daqui
  // e deixar o navegador resolver, que seria o caminho curto.
  useEffect(() => {
    if (!mobileOpen) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { setMobileOpen(false); return }
      if (e.key !== 'Tab') return
      const el = drawerRef.current
      if (!el) return
      const focusables = Array.from(el.querySelectorAll<HTMLElement>(FOCUSABLE))
      if (focusables.length === 0) return
      const first = focusables[0]
      const last  = focusables[focusables.length - 1]
      const atual = document.activeElement
      // Foco fora do drawer (clique na página atrás, por exemplo): traz de volta
      // pela ponta certa em vez de deixar seguir para o conteúdo de baixo.
      if (!el.contains(atual)) {
        e.preventDefault()
        ;(e.shiftKey ? last : first).focus()
      } else if (e.shiftKey && atual === first) {
        e.preventDefault()
        last.focus()
      } else if (!e.shiftKey && atual === last) {
        e.preventDefault()
        first.focus()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [mobileOpen])

  // Fecha ao navegar. Cada <Link> do menu já chama onClose, mas o drawer também
  // precisa sumir quando a navegação parte de outro lugar (botão voltar do
  // aparelho, redirect de sessão expirada) — senão ele reaparece sobreposto à
  // tela nova.
  useEffect(() => { setMobileOpen(false) }, [pathname])

  function toggleCollapsed() {
    setCollapsed(v => {
      const next = !v
      try { localStorage.setItem('admin-sidebar-collapsed', String(next)) } catch {}
      return next
    })
  }

  useEffect(() => {
    let mounted = true
    const poll = async () => {
      try {
        const { data } = await notificationsApi.unreadCount()
        if (mounted) setUnreadCount(data.count)
      } catch {}
    }
    poll()
    const id = setInterval(poll, 30_000)
    return () => { mounted = false; clearInterval(id) }
  }, [])

  // Dot do Fiscal usa um sinal próprio (validade do certificado), não o unreadCount
  // genérico de notificações — evita acender junto com o dot de Mensageria.
  useEffect(() => {
    if (getRole() !== 'Admin' || !site.enabledModules.includes('fiscal')) return
    let mounted = true
    const poll = async () => {
      try {
        const { data } = await fiscalApi.getConfig()
        const dias = data.diasParaVencer
        if (mounted) setFiscalAlerta(data.certificadoConfigurado && dias !== undefined && dias !== null && dias <= 30)
      } catch {}
    }
    poll()
    const id = setInterval(poll, 5 * 60_000)
    return () => { mounted = false; clearInterval(id) }
  }, [site.enabledModules])

  async function handleLogout() {
    if (loggingOut) return
    setLoggingOut(true)
    try { await authApi.logout() } catch {}
    clearAuth()
    router.push('/login')
  }

  const role = mounted ? getRole() : ''
  const userName = mounted ? getUserName() : 'Admin'
  const roleLabel = role === 'Admin' ? 'Admin' : role === 'Operator' ? 'Operador' : role

  function renderFooter(isCollapsed: boolean) {
    if (isCollapsed) {
      // Recolhida: só ícones empilhados, cada um com tooltip nativo (title) —
      // navegação continua clara sem o texto.
      return (
        <div className="px-3 py-4 border-t border-surface-500 flex flex-col items-center gap-2">
          <div
            title={`${userName} (${roleLabel})`}
            className="w-10 h-10 rounded-full bg-brand-500/20 border border-brand-500/30 flex items-center justify-center shrink-0"
          >
            <User className="w-5 h-5 text-brand-400" />
          </div>
          <a
            href="/" target="_blank" rel="noopener noreferrer" title="Ver Loja"
            className="w-9 h-9 rounded-lg flex items-center justify-center text-gray-500 hover:bg-surface-700 hover:text-brand-400 transition-colors"
          >
            <Store className="w-4 h-4" />
          </a>
          <ThemeToggle compact />
          <button
            onClick={handleLogout} disabled={loggingOut} title="Sair"
            className="w-9 h-9 rounded-lg flex items-center justify-center text-gray-500 hover:bg-red-500/10 hover:text-red-400 transition-colors disabled:opacity-50"
          >
            {loggingOut ? <Loader2 className="w-4 h-4 animate-spin" /> : <LogOut className="w-4 h-4" />}
          </button>
        </div>
      )
    }

    return (
      <div className="px-3 py-4 border-t border-surface-500">
        <div className="flex items-center gap-3 bg-surface-700 p-3 rounded-xl border border-surface-500 mb-2">
          <div className="w-10 h-10 rounded-full bg-brand-500/20 border border-brand-500/30 flex items-center justify-center shrink-0">
            <User className="w-5 h-5 text-brand-400" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold text-white truncate">{userName}</p>
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
  }

  return (
    <>
      {/* ── Mobile: barra superior ──────────────────────────────────────────
          Mostra o TÍTULO DA TELA ATUAL, não só a marca. Sem sidebar visível, o
          celular perde a única indicação de "onde estou" — e a marca da loja,
          que o usuário já conhece, não responde a essa pergunta. A logo fica
          reduzida a um selo de canto. */}
      <header className="md:hidden fixed top-0 left-0 right-0 z-30 flex items-center gap-3 bg-surface-800 border-b border-surface-500 px-4 h-topbar">
        <img src={logoSrc} alt={site.siteName} className="h-8 w-8 object-contain shrink-0" />
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-white truncate leading-tight">
            {currentNavTitle(pathname) ?? site.siteName}
          </p>
          <p className="text-[10px] text-brand-400 font-semibold tracking-wider uppercase leading-tight">Admin</p>
        </div>
        <button
          ref={menuButtonRef}
          onClick={() => setMobileOpen(true)}
          aria-label="Abrir menu"
          aria-expanded={mobileOpen}
          className="touch-target -mr-2 flex items-center justify-center text-gray-400 hover:text-white"
        >
          <Menu className="w-6 h-6" />
        </button>
      </header>

      {/* Mobile overlay */}
      {mobileOpen && (
        <div
          className="md:hidden fixed inset-0 z-40 bg-black/70 animate-fade-in"
          onClick={() => setMobileOpen(false)}
        />
      )}

      {/* ── Mobile: drawer com o menu completo ──────────────────────────────
          Largura em `max-w` (e não fixa em 260px) porque em aparelhos de 320px
          um drawer de 260px deixa só 60px de overlay — alvo pequeno demais pra
          fechar tocando fora, que é o gesto esperado. */}
      <aside
        id="admin-mobile-drawer"
        ref={drawerRef}
        aria-hidden={!mobileOpen}
        className={clsx(
          'md:hidden fixed inset-y-0 left-0 z-50 w-[85vw] max-w-[300px] bg-surface-900 border-r border-surface-500 flex flex-col transition-transform duration-300 pt-safe',
          mobileOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex items-center justify-between gap-2 px-5 py-5 shrink-0">
          <div className="flex items-center gap-3 min-w-0">
            <img src={logoSrc} alt={site.siteName} className="h-10 w-10 object-contain shrink-0" />
            <div className="min-w-0">
              <p className="text-white text-base leading-tight truncate">{site.siteName}</p>
              <p className="text-[10px] text-brand-400 font-semibold tracking-wider uppercase">Admin</p>
            </div>
          </div>
          <button
            onClick={() => setMobileOpen(false)}
            aria-label="Fechar menu"
            className="touch-target flex items-center justify-center text-gray-500 hover:text-white shrink-0"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
        <NavItems pathname={pathname} onClose={() => setMobileOpen(false)} unreadCount={unreadCount} fiscalAlerta={fiscalAlerta} enabledModules={site.enabledModules} />
        {renderFooter(false)}
      </aside>

      {/* ── Mobile: barra inferior ──────────────────────────────────────────
          Fica FORA do drawer de propósito: é a navegação de uso diário, sempre
          visível, e o drawer passa a ser só o "resto do menu". */}
      <MobileTabBar onOpenMenu={() => setMobileOpen(true)} hasAlert={unreadCount > 0 || fiscalAlerta} />

      {/* Desktop sidebar */}
      <aside className={clsx(
        'hidden md:flex h-screen sticky top-0 bg-surface-900 border-r border-surface-500 flex-col shrink-0 relative transition-[width] duration-200',
        collapsed ? 'w-[76px]' : 'w-[260px]',
      )}>
        <div className={clsx('py-7 shrink-0 flex items-center gap-3', collapsed ? 'px-0 justify-center' : 'px-6')}>
          <img src={logoSrc} alt={site.siteName} className="h-10 w-10 object-contain shrink-0" />
          {!collapsed && (
            <div className="min-w-0">
              <p className="text-white text-base leading-tight truncate">{site.siteName}</p>
              <p className="text-[10px] text-brand-400 font-semibold tracking-wider uppercase">Admin</p>
            </div>
          )}
        </div>
        <NavItems pathname={pathname} unreadCount={unreadCount} fiscalAlerta={fiscalAlerta} enabledModules={site.enabledModules} collapsed={collapsed} />
        {renderFooter(collapsed)}

        {/* Botão de recolher/expandir — preso na borda direita, sempre visível
            independente do scroll do menu. */}
        <button
          onClick={toggleCollapsed}
          title={collapsed ? 'Expandir menu' : 'Recolher menu'}
          className="absolute top-9 -right-3 w-6 h-6 rounded-full bg-surface-700 border border-surface-500 flex items-center justify-center text-gray-400 hover:text-white hover:border-brand-500/50 hover:bg-surface-600 transition-colors shadow-md"
        >
          {collapsed ? <ChevronsRight className="w-3.5 h-3.5" /> : <ChevronsLeft className="w-3.5 h-3.5" />}
        </button>
      </aside>
    </>
  )
}
