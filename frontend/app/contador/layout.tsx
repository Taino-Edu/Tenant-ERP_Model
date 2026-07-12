'use client'
import { useEffect, useState } from 'react'
import { useRouter, usePathname } from 'next/navigation'
import { isContador, clearAuth } from '@/lib/auth'
import { Toaster } from 'react-hot-toast'
import { LogOut, Calculator } from 'lucide-react'

// /contador/cadastro precisa ficar acessível sem sessão — é onde o contador cria
// a própria conta pela primeira vez. Fica dentro da mesma árvore de rotas (só
// pra reaproveitar o header), mas o layout não pode aplicar o guard aqui.
const PUBLIC_PATHS = ['/contador/cadastro']

export default function ContadorLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter()
  const pathname = usePathname()
  const isPublic = PUBLIC_PATHS.includes(pathname)
  const [checked, setChecked] = useState(isPublic)

  useEffect(() => {
    if (isPublic) { setChecked(true); return }
    if (!isContador()) {
      router.push('/login')
      return
    }
    setChecked(true)
  }, [router, pathname, isPublic])

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
            <Calculator className="w-5 h-5 text-brand-400" />
            Portal do Contador
          </div>
          {!isPublic && (
            <button onClick={handleLogout} className="btn-secondary text-sm py-1.5">
              <LogOut className="w-4 h-4" /> Sair
            </button>
          )}
        </div>
      </header>
      <main className="max-w-5xl mx-auto px-6 py-8">
        {children}
      </main>
    </div>
  )
}
