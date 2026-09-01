'use client'
import Sidebar from '@/components/admin/Sidebar'
import AiChatWidget from '@/components/admin/AiChatWidget'
import KeyboardShortcutsOverlay from '@/components/admin/KeyboardShortcutsOverlay'
import TimerAlarmOverlay from '@/components/admin/TimerAlarmOverlay'
import TenantColorInjector, { BRAND_CACHE_KEY } from '@/components/admin/TenantColorInjector'
import UsageTracker from '@/components/admin/UsageTracker'
import AdminAreaSubnav from '@/components/admin/AdminAreaSubnav'
import { Toaster } from 'react-hot-toast'
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '@/lib/api'
import { saveAuth, clearAuth, getImpersonatingOwnerName, getRole } from '@/lib/auth'
import { ADMIN_PERMISSIONS_EVENT, useAdminPermissions } from '@/hooks/useAdminPermissions'
import { useSiteConfig } from '@/contexts/SiteConfigContext'

// Aplica o último ramp de cor de marca cacheado ANTES da hidratação — evita
// flash da cor default no reload, mesmo padrão já usado pro tema claro/escuro
// em app/layout.tsx.
const BRAND_FOUC_SCRIPT = `(function(){try{var v=JSON.parse(localStorage.getItem('${BRAND_CACHE_KEY}')||'null');if(v){var r=document.documentElement;for(var k in v){r.style.setProperty(k,v[k])}}}catch(e){}})();`

// Renova o token silenciosamente a cada 45 min para evitar desconexão por inatividade.
const REFRESH_INTERVAL_MS = 45 * 60 * 1000

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter()
  const { site } = useSiteConfig()
  const [impersonatingOwner, setImpersonatingOwner] = useState<string | null>(null)
  const { isAdmin, can } = useAdminPermissions()
  const canUseAi = isAdmin || can('ia')

  useEffect(() => {
    setImpersonatingOwner(getImpersonatingOwnerName())
  }, [])

  // O painel da loja é de Admin e Operator. Cliente ou dono da plataforma que
  // digitasse /admin carregava a casca inteira e só via as chamadas de API
  // falhando uma a uma — o /plataforma já tinha essa trava, aqui faltava.
  // Não é segurança (quem barra de verdade são as políticas do backend), é não
  // deixar a pessoa numa tela que nunca vai funcionar pra ela.
  useEffect(() => {
    const role = getRole()
    if (role && role !== 'Admin' && role !== 'Operator') router.replace('/login')
  }, [router])

  // Marca o <body> enquanto o painel admin está montado. Os widgets flutuantes
  // globais (banner de cookies, botão de instalar PWA, lançador da IA) vivem no
  // RootLayout, FORA do .admin-shell — então não têm como saber que existe uma
  // barra de navegação fixa no rodapé do celular e nasciam por cima dela.
  // Mesmo mecanismo já usado por `body.institucional-page` (ver globals.css).
  useEffect(() => {
    document.body.classList.add('admin-route')
    return () => document.body.classList.remove('admin-route')
  }, [])

  useEffect(() => {
    const refresh = async () => {
      try {
        const res = await api.post('/api/auth/refresh', {})
        if (res.data) {
          saveAuth(res.data)
          // A sidebar, a barra do celular e os atalhos montam o menu a partir
          // do cookie. Sem este aviso, um perfil alterado só chegaria na tela
          // depois de recarregar a página.
          window.dispatchEvent(new Event(ADMIN_PERMISSIONS_EVENT))
        }
      } catch {
        // Se falhar, o interceptor cuida do redirect para /login na próxima chamada
      }
    }

    const id = setInterval(refresh, REFRESH_INTERVAL_MS)
    return () => clearInterval(id)
  }, [])

  function sairDaSimulacao() {
    // Sessão de impersonação não tem refresh token — sair é só limpar os
    // cookies locais e voltar pro login, SEM chamar /api/auth/logout (isso
    // revogaria a sessão pelo `sub`, que aqui é o dono da plataforma, não o
    // admin real da loja).
    clearAuth()
    router.push('/login')
  }

  return (
    <div className="admin-shell flex min-h-screen bg-surface-900">
      <script dangerouslySetInnerHTML={{ __html: BRAND_FOUC_SCRIPT }} />
      <TenantColorInjector />
      <UsageTracker />
      <Sidebar />
      {/* `min-w-0` é o que impede uma tabela larga de esticar o flex container e
          empurrar a sidebar pra fora da tela — item flex tem min-width:auto por
          padrão, então ele cresce até caber o conteúdo em vez de rolar dentro
          da própria caixa. */}
      <main className="flex-1 min-w-0 overflow-auto pt-topbar md:pt-0 admin-main">
        {impersonatingOwner && (
          <div className="sticky top-0 z-50 flex items-center justify-center gap-3 bg-amber-500 px-4 py-2 text-sm font-medium text-black">
            <span>Você está visualizando esta loja como {impersonatingOwner} (modo simulação — sessão expira em 20 min)</span>
            <button
              onClick={sairDaSimulacao}
              className="rounded-md bg-black/10 px-3 py-1 font-semibold hover:bg-black/20"
            >
              Sair da simulação
            </button>
          </div>
        )}
        <Toaster
          position="top-right"
          // No celular o toast nascia por cima da barra superior fixa, tapando
          // o título da tela justamente quando algo dava errado. A classe
          // desloca o container abaixo da barra (regra em globals.css).
          containerClassName="admin-toaster"
          toastOptions={{
            style: { background: '#1A1A1F', color: '#fff', border: '1px solid #2D2D36', fontSize: '14px', borderRadius: '12px' },
            success: { iconTheme: { primary: '#00F0A8', secondary: '#000' } },
            error:   { iconTheme: { primary: '#FF3B30', secondary: '#fff' } },
          }}
        />
        <AdminAreaSubnav />
        {children}
      </main>
      {site.enabledModules.includes('ia') && canUseAi ? <AiChatWidget /> : null}
      <KeyboardShortcutsOverlay />
      <TimerAlarmOverlay />
    </div>
  )
}
