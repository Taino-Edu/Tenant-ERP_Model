'use client'
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { isPlatformOwner, clearAuth } from '@/lib/auth'
import { Toaster } from 'react-hot-toast'
import { LogOut, ShieldCheck } from 'lucide-react'

export default function PlataformaLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter()
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
    <div className="min-h-screen bg-surface-900">
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
            Painel da Plataforma
          </div>
          <button onClick={handleLogout} className="btn-secondary text-sm py-1.5">
            <LogOut className="w-4 h-4" /> Sair
          </button>
        </div>
      </header>
      <main className="max-w-5xl mx-auto px-6 py-8">
        {children}
      </main>
    </div>
  )
}
