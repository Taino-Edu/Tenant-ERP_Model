'use client'
import Sidebar from '@/components/admin/Sidebar'
import AiChatWidget from '@/components/admin/AiChatWidget'
import KeyboardShortcutsOverlay from '@/components/admin/KeyboardShortcutsOverlay'
import { Toaster } from 'react-hot-toast'
import { useEffect } from 'react'
import { api } from '@/lib/api'
import { saveAuth } from '@/lib/auth'

// Renova o token silenciosamente a cada 45 min para evitar desconexão por inatividade.
const REFRESH_INTERVAL_MS = 45 * 60 * 1000

export default function AdminLayout({ children }: { children: React.ReactNode }) {
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

  return (
    <div className="flex min-h-screen bg-surface-900">
      <Sidebar />
      <main className="flex-1 overflow-auto pt-14 md:pt-0 admin-main">
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
    </div>
  )
}
