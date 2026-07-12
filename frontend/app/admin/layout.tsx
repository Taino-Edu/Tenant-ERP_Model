'use client'
import Sidebar from '@/components/admin/Sidebar'
import AiChatWidget from '@/components/admin/AiChatWidget'
import KeyboardShortcutsOverlay from '@/components/admin/KeyboardShortcutsOverlay'
import TimerAlarmOverlay from '@/components/admin/TimerAlarmOverlay'
import TenantColorInjector, { BRAND_CACHE_KEY } from '@/components/admin/TenantColorInjector'
import { Toaster } from 'react-hot-toast'
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '@/lib/api'
import { saveAuth, clearAuth, getImpersonatingOwnerName } from '@/lib/auth'

// Aplica o último ramp de cor de marca cacheado ANTES da hidratação — evita
// flash da cor default no reload, mesmo padrão já usado pro tema claro/escuro
// em app/layout.tsx.
const BRAND_FOUC_SCRIPT = `(function(){try{var v=JSON.parse(localStorage.getItem('${BRAND_CACHE_KEY}')||'null');if(v){var r=document.documentElement;for(var k in v){r.style.setProperty(k,v[k])}}}catch(e){}})();`

// Renova o token silenciosamente a cada 45 min para evitar desconexão por inatividade.
const REFRESH_INTERVAL_MS = 45 * 60 * 1000

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter()
  const [impersonatingOwner, setImpersonatingOwner] = useState<string | null>(null)

  useEffect(() => {
    setImpersonatingOwner(getImpersonatingOwnerName())
  }, [])

  useEffect(() => {
    const refresh = async () => {
      try {
        const res = await api.post('/api/auth/refresh', {})
        if (res.data) saveAuth(res.data)
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
      <Sidebar />
      <main className="flex-1 overflow-auto pt-14 md:pt-0 admin-main">
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
          toastOptions={{
            style: { background: '#1A1A1F', color: '#fff', border: '1px solid #2D2D36', fontSize: '14px', borderRadius: '12px' },
            success: { iconTheme: { primary: '#00F0A8', secondary: '#000' } },
            error:   { iconTheme: { primary: '#FF3B30', secondary: '#fff' } },
          }}
        />
        {children}
      </main>
      <AiChatWidget />
      <KeyboardShortcutsOverlay />
      <TimerAlarmOverlay />
    </div>
  )
}
